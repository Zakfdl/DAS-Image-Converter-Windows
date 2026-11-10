using DasImageConverter.Core.Models;
using DasImageConverter.Core.Validation;
using ImageMagick;

namespace DasImageConverter.Core.Services;

/// <summary>
/// Offline local image codec implementation backed by Magick.NET (ImageMagick + libjpeg/libpng/libwebp/libavif).
/// </summary>
public sealed class MagickImageProcessingEngine : IImageProcessingEngine
{
    private const int ThumbnailMaxWidth = 160;
    private static readonly object InitLock = new();
    private static bool _initialized;

    public MagickImageProcessingEngine()
    {
        EnsureInitialized();
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (InitLock)
        {
            if (_initialized)
            {
                return;
            }

            var tempRoot = Path.Combine(Path.GetTempPath(), "DASImageConverter", "MagickTemp");
            Directory.CreateDirectory(tempRoot);
            MagickNET.SetTempDirectory(tempRoot);
            MagickNET.Initialize();

            // Cap ImageMagick memory area so parallel 60 MP decodes cannot exhaust system RAM.
            ResourceLimits.LimitMemory(new Percentage(45));
            _initialized = true;
        }
    }

    public Task<QueueImageItem> InspectAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (isValidCandidate, candidateError) = ImageInputValidator.ValidateFileCandidate(filePath);
        if (!isValidCandidate)
        {
            throw new InvalidOperationException(candidateError);
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileInfo = new FileInfo(filePath);
            var fileName = fileInfo.Name;

            try
            {
                // Header-only Ping prevents allocating huge pixel buffers for >60 MP decompression bombs.
                using (var ping = new MagickImage())
                {
                    ping.Ping(filePath);
                    var detectedFormat = MapMagickFormat(ping.Format);
                    if (detectedFormat is null)
                    {
                        throw new InvalidOperationException($"“{fileName}” is not a supported image file.");
                    }

                    var (headerWidth, headerHeight) = GetOrientedDimensions(ping);
                    var (dimValid, dimError) = ImageInputValidator.ValidateDimensions(fileName, headerWidth, headerHeight);
                    if (!dimValid)
                    {
                        throw new InvalidOperationException(dimError);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                using var image = new MagickImage(filePath);
                var actualFormat = MapMagickFormat(image.Format)
                    ?? throw new InvalidOperationException($"“{fileName}” is not a supported image file.");

                image.AutoOrient();

                var width = (int)image.Width;
                var height = (int)image.Height;
                var (validAfterOrient, orientError) = ImageInputValidator.ValidateDimensions(fileName, width, height);
                if (!validAfterOrient)
                {
                    throw new InvalidOperationException(orientError);
                }

                var thumbBytes = CreateThumbnailBytes(image, MagickFormat.Jpeg, quality: 72, compositeWhiteForOpaque: true);

                return new QueueImageItem
                {
                    SourcePath = Path.GetFullPath(filePath),
                    FileName = fileName,
                    FileSizeBytes = fileInfo.Length,
                    Width = width,
                    Height = height,
                    DetectedFormat = actualFormat,
                    ThumbnailBytes = thumbBytes,
                    Status = QueueItemStatus.Ready,
                };
            }
            catch (MagickException)
            {
                throw new InvalidOperationException(
                    $"“{fileName}” could not be decoded. It may be damaged or unsupported.");
            }
        }, cancellationToken);
    }

    public Task<ConvertedImageResult> ConvertAsync(
        QueueImageItem item,
        string outputFileName,
        string outputPath,
        ConversionSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parentDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            try
            {
                using var image = new MagickImage(item.SourcePath);
                image.AutoOrient();

                NormalizeToSrgbIfNeeded(image);

                var sourceWidth = (int)image.Width;
                var sourceHeight = (int)image.Height;
                var (targetWidth, targetHeight) = settings.ComputeTargetDimensions(sourceWidth, sourceHeight);

                if (targetWidth != sourceWidth || targetHeight != sourceHeight)
                {
                    image.FilterType = FilterType.Lanczos;
                    image.Resize(new MagickGeometry((uint)targetWidth, (uint)targetHeight)
                    {
                        IgnoreAspectRatio = true,
                    });
                }

                cancellationToken.ThrowIfCancellationRequested();

                ConfigureTargetEncoding(image, settings);

                image.Write(outputPath);

                // Generate PNG preview matching web behavior so transparency renders cleanly over checkerboard.
                var previewBytes = CreateThumbnailBytes(image, MagickFormat.Png, quality: 100, compositeWhiteForOpaque: false);
                var outputLength = new FileInfo(outputPath).Length;

                return new ConvertedImageResult
                {
                    SourceItemId = item.Id,
                    SourceFileName = item.FileName,
                    OutputFileName = outputFileName,
                    OutputFormat = settings.OutputFormat,
                    OutputFilePath = outputPath,
                    OriginalSizeBytes = item.FileSizeBytes,
                    OutputSizeBytes = outputLength,
                    Width = targetWidth,
                    Height = targetHeight,
                    PreviewBytes = previewBytes,
                };
            }
            catch (OperationCanceledException)
            {
                TryDeleteFile(outputPath);
                throw;
            }
            catch (MagickException ex)
            {
                TryDeleteFile(outputPath);
                throw new InvalidOperationException(
                    $"Failed to convert “{item.FileName}”: {ex.Message}",
                    ex);
            }
        }, cancellationToken);
    }

    private static void ConfigureTargetEncoding(MagickImage image, ConversionSettings settings)
    {
        var quality = (uint)settings.NormalizedQuality;

        switch (settings.OutputFormat)
        {
            case ImageFormatKind.Jpeg:
                var bg = settings.JpegBackgroundColor;
                image.BackgroundColor = new MagickColor(bg.R, bg.G, bg.B);
                image.Alpha(AlphaAction.Remove);
                image.Format = MagickFormat.Jpeg;
                image.Quality = quality;
                image.Settings.SetDefine(
                    MagickFormat.Jpeg,
                    "sampling-factor",
                    quality >= 90 ? "1x1,1x1,1x1" : "2x2,1x1,1x1");
                break;

            case ImageFormatKind.Png:
                image.Format = MagickFormat.Png;
                break;

            case ImageFormatKind.WebP:
                image.Format = MagickFormat.WebP;
                image.Quality = quality;
                break;

            case ImageFormatKind.Avif:
                image.Format = MagickFormat.Avif;
                image.Quality = quality;
                image.Settings.SetDefine(MagickFormat.Avif, "speed", 6);
                image.Settings.SetDefine(MagickFormat.Avif, "chroma-subsampling", quality >= 90 ? "444" : "420");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(settings.OutputFormat));
        }
    }

    private static void NormalizeToSrgbIfNeeded(MagickImage image)
    {
        var profile = image.GetColorProfile();
        if (profile is not null)
        {
            if (profile.ColorSpace != ColorSpace.sRGB ||
                profile.Description?.Contains("sRGB", StringComparison.OrdinalIgnoreCase) != true)
            {
                try
                {
                    image.TransformColorSpace(ColorProfiles.SRGB);
                    return;
                }
                catch (MagickException)
                {
                    // Fall through to direct ColorSpace assignment when embedded ICC is malformed.
                }
            }
        }

        if (image.ColorSpace is not (ColorSpace.sRGB or ColorSpace.RGB or ColorSpace.Gray))
        {
            image.ColorSpace = ColorSpace.sRGB;
        }
    }

    private static byte[] CreateThumbnailBytes(
        MagickImage source,
        MagickFormat outputFormat,
        uint quality,
        bool compositeWhiteForOpaque)
    {
        using var clone = source.Clone();
        var targetWidth = Math.Min((int)clone.Width, ThumbnailMaxWidth);
        var targetHeight = Math.Max(
            1,
            (int)Math.Round((double)clone.Height * targetWidth / clone.Width, MidpointRounding.AwayFromZero));

        if (targetWidth != clone.Width || targetHeight != clone.Height)
        {
            clone.FilterType = FilterType.Lanczos;
            clone.Resize(new MagickGeometry((uint)targetWidth, (uint)targetHeight)
            {
                IgnoreAspectRatio = true,
            });
        }

        if (compositeWhiteForOpaque && clone.HasAlpha)
        {
            clone.BackgroundColor = MagickColors.White;
            clone.Alpha(AlphaAction.Remove);
        }

        clone.Format = outputFormat;
        if (outputFormat != MagickFormat.Png)
        {
            clone.Quality = quality;
        }

        return clone.ToByteArray();
    }

    private static ImageFormatKind? MapMagickFormat(MagickFormat format) => format switch
    {
        MagickFormat.Jpeg or MagickFormat.Jpg or MagickFormat.Pjpeg => ImageFormatKind.Jpeg,
        MagickFormat.Png or MagickFormat.Png00 or MagickFormat.Png8 or MagickFormat.Png24 or MagickFormat.Png32 or MagickFormat.Png48 or MagickFormat.Png64 => ImageFormatKind.Png,
        MagickFormat.WebP => ImageFormatKind.WebP,
        MagickFormat.Avif => ImageFormatKind.Avif,
        _ => null,
    };

    private static (int Width, int Height) GetOrientedDimensions(MagickImage image)
    {
        var width = (int)image.Width;
        var height = (int)image.Height;

        return image.Orientation switch
        {
            Orientation.LeftTop or Orientation.RightTop or Orientation.RightBottom or Orientation.LeftBottom => (height, width),
            _ => (width, height),
        };
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}
