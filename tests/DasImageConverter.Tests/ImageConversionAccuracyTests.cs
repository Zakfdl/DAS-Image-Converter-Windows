using DasImageConverter.Core.Models;
using DasImageConverter.Core.Services;
using ImageMagick;

namespace DasImageConverter.Tests;

public sealed class ImageConversionAccuracyTests
{
    private readonly MagickImageProcessingEngine _engine = new();

    [Theory]
    [InlineData(ImageFormatKind.Jpeg, ImageFormatKind.Png)]
    [InlineData(ImageFormatKind.Jpeg, ImageFormatKind.WebP)]
    [InlineData(ImageFormatKind.Jpeg, ImageFormatKind.Avif)]
    [InlineData(ImageFormatKind.Png, ImageFormatKind.Jpeg)]
    [InlineData(ImageFormatKind.Png, ImageFormatKind.WebP)]
    [InlineData(ImageFormatKind.Png, ImageFormatKind.Avif)]
    [InlineData(ImageFormatKind.WebP, ImageFormatKind.Jpeg)]
    [InlineData(ImageFormatKind.WebP, ImageFormatKind.Png)]
    [InlineData(ImageFormatKind.WebP, ImageFormatKind.Avif)]
    [InlineData(ImageFormatKind.Avif, ImageFormatKind.Jpeg)]
    [InlineData(ImageFormatKind.Avif, ImageFormatKind.Png)]
    [InlineData(ImageFormatKind.Avif, ImageFormatKind.WebP)]
    public async Task ConvertAsync_SupportsFullFormatMatrixOffline(
        ImageFormatKind sourceFormat,
        ImageFormatKind targetFormat)
    {
        using var workspace = new TempWorkspace();
        var magickInputFormat = sourceFormat switch
        {
            ImageFormatKind.Jpeg => MagickFormat.Jpeg,
            ImageFormatKind.Png => MagickFormat.Png,
            ImageFormatKind.WebP => MagickFormat.WebP,
            ImageFormatKind.Avif => MagickFormat.Avif,
            _ => throw new ArgumentOutOfRangeException(nameof(sourceFormat)),
        };

        var inputPath = workspace.CreateGradientImage($"source{sourceFormat.GetExtension()}", 180, 120, magickInputFormat);
        var item = await _engine.InspectAsync(inputPath);
        Assert.Equal(sourceFormat, item.DetectedFormat);
        Assert.Equal(180, item.Width);
        Assert.Equal(120, item.Height);
        Assert.NotEmpty(item.ThumbnailBytes);

        var outName = $"converted{targetFormat.GetExtension()}";
        var outPath = workspace.GetPath(outName);
        var settings = new ConversionSettings { OutputFormat = targetFormat, Quality = 82 };

        var result = await _engine.ConvertAsync(item, outName, outPath, settings);

        Assert.True(File.Exists(result.OutputFilePath));
        Assert.True(result.OutputSizeBytes > 0);
        Assert.Equal(180, result.Width);
        Assert.Equal(120, result.Height);
        Assert.NotEmpty(result.PreviewBytes);

        using var verified = new MagickImage(result.OutputFilePath);
        Assert.Equal(180u, verified.Width);
        Assert.Equal(120u, verified.Height);
    }

    [Fact]
    public async Task ConvertAsync_ResizesDownAndNeverUpscales()
    {
        using var workspace = new TempWorkspace();
        var inputPath = workspace.CreateGradientImage("wide.jpg", 300, 200, MagickFormat.Jpeg);
        var item = await _engine.InspectAsync(inputPath);

        var resizedPath = workspace.GetPath("resized.webp");
        var downscale = new ConversionSettings { OutputFormat = ImageFormatKind.WebP, Quality = 82, ResizeEnabled = true, MaxWidth = 80 };
        var downResult = await _engine.ConvertAsync(item, "resized.webp", resizedPath, downscale);

        Assert.Equal(80, downResult.Width);
        Assert.Equal(53, downResult.Height);

        var noUpscalePath = workspace.GetPath("noupscale.png");
        var upscaleGuard = new ConversionSettings { OutputFormat = ImageFormatKind.Png, ResizeEnabled = true, MaxWidth = 1920 };
        var sameResult = await _engine.ConvertAsync(item, "noupscale.png", noUpscalePath, upscaleGuard);

        Assert.Equal(300, sameResult.Width);
        Assert.Equal(200, sameResult.Height);
    }

    [Fact]
    public async Task ConvertAsync_CompositesTransparentAlphaOntoSelectedJpegBackground()
    {
        using var workspace = new TempWorkspace();
        var pngPath = workspace.CreateTransparentBadge("badge.png", 100, 100, MagickFormat.Png);
        var item = await _engine.InspectAsync(pngPath);

        var jpgOutPath = workspace.GetPath("badge.jpg");
        var settings = new ConversionSettings
        {
            OutputFormat = ImageFormatKind.Jpeg,
            Quality = 95,
            JpegBackgroundColor = new RgbColor(255, 220, 180),
        };

        await _engine.ConvertAsync(item, "badge.jpg", jpgOutPath, settings);

        using var resultJpeg = new MagickImage(jpgOutPath);
        Assert.False(resultJpeg.HasAlpha);
        using var pixels = resultJpeg.GetPixels();
        var topLeft = pixels.GetPixel(5, 5).ToColor()!;
        Assert.InRange(topLeft.R, (byte)245, (byte)255);
        Assert.InRange(topLeft.G, (byte)210, (byte)230);
        Assert.InRange(topLeft.B, (byte)170, (byte)190);
    }

    [Fact]
    public async Task ConvertAsync_PreservesTransparencyForPngWebpAndAvif()
    {
        using var workspace = new TempWorkspace();
        var pngPath = workspace.CreateTransparentBadge("badge.png", 80, 80, MagickFormat.Png);
        var item = await _engine.InspectAsync(pngPath);

        foreach (var format in new[] { ImageFormatKind.Png, ImageFormatKind.WebP, ImageFormatKind.Avif })
        {
            var outPath = workspace.GetPath($"alpha{format.GetExtension()}");
            await _engine.ConvertAsync(item, Path.GetFileName(outPath), outPath, new ConversionSettings { OutputFormat = format, Quality = 90 });

            using var output = new MagickImage(outPath);
            Assert.True(output.HasAlpha, $"Expected alpha channel to be preserved for {format}");
        }
    }

    [Fact]
    public async Task InspectAndConvert_AutoOrientsExifRotatedPhotos()
    {
        using var workspace = new TempWorkspace();
        var jpegPath = workspace.CreateExifRotatedJpeg("portrait-exif.jpg", 300, 200, Orientation.RightTop);

        var item = await _engine.InspectAsync(jpegPath);
        Assert.Equal(200, item.Width);
        Assert.Equal(300, item.Height);

        var outPath = workspace.GetPath("oriented.png");
        var result = await _engine.ConvertAsync(item, "oriented.png", outPath, new ConversionSettings { OutputFormat = ImageFormatKind.Png });

        Assert.Equal(200, result.Width);
        Assert.Equal(300, result.Height);
    }

    [Theory]
    [InlineData(ImageFormatKind.Jpeg)]
    [InlineData(ImageFormatKind.WebP)]
    [InlineData(ImageFormatKind.Avif)]
    public async Task ConvertAsync_HigherQualityProducesLargerOrEqualPayloadThanLowQuality(ImageFormatKind format)
    {
        using var workspace = new TempWorkspace();
        var inputPath = workspace.CreateGradientImage("rich.png", 240, 240, MagickFormat.Png);
        var item = await _engine.InspectAsync(inputPath);

        var lowPath = workspace.GetPath($"low{format.GetExtension()}");
        var highPath = workspace.GetPath($"high{format.GetExtension()}");

        var lowResult = await _engine.ConvertAsync(item, Path.GetFileName(lowPath), lowPath, new ConversionSettings { OutputFormat = format, Quality = 15 });
        var highResult = await _engine.ConvertAsync(item, Path.GetFileName(highPath), highPath, new ConversionSettings { OutputFormat = format, Quality = 95 });

        Assert.True(highResult.OutputSizeBytes > lowResult.OutputSizeBytes, $"Expected Q95 > Q15 for {format}");
    }

    [Fact]
    public async Task InspectAsync_RejectsCorruptedAndSpoofedImages()
    {
        using var workspace = new TempWorkspace();
        var fakeJpg = workspace.GetPath("not-actually-a-jpg.jpg");
        File.WriteAllText(fakeJpg, "This is plain text pretending to be JPEG.");

        var truncatedPng = workspace.GetPath("truncated.png");
        File.WriteAllBytes(truncatedPng, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00]);

        var exFake = await Assert.ThrowsAsync<InvalidOperationException>(() => _engine.InspectAsync(fakeJpg));
        var exTruncated = await Assert.ThrowsAsync<InvalidOperationException>(() => _engine.InspectAsync(truncatedPng));

        Assert.Contains("could not be decoded", exFake.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("could not be decoded", exTruncated.Message, StringComparison.OrdinalIgnoreCase);
    }
}
