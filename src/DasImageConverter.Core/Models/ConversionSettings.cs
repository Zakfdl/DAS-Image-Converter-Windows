namespace DasImageConverter.Core.Models;

/// <summary>
/// User-configured conversion options applied across a batch.
/// </summary>
public sealed record ConversionSettings
{
    public const int MinQuality = 1;
    public const int MaxQuality = 100;
    public const int DefaultQuality = 82;
    public const int MinResizeWidth = 1;
    public const int MaxResizeWidth = 30_000;
    public const int DefaultResizeWidth = 1920;

    public ImageFormatKind OutputFormat { get; init; } = ImageFormatKind.Jpeg;

    public int Quality { get; init; } = DefaultQuality;

    public bool ResizeEnabled { get; init; }

    public int MaxWidth { get; init; } = DefaultResizeWidth;

    public RgbColor JpegBackgroundColor { get; init; } = RgbColor.White;

    public int NormalizedQuality => Math.Clamp(Quality, MinQuality, MaxQuality);

    public int NormalizedMaxWidth => Math.Clamp(MaxWidth, MinResizeWidth, MaxResizeWidth);

    /// <summary>
    /// Computes target dimensions matching the website's downscale-only proportional resize behavior.
    /// </summary>
    public (int Width, int Height) ComputeTargetDimensions(int sourceWidth, int sourceHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);

        if (!ResizeEnabled)
        {
            return (sourceWidth, sourceHeight);
        }

        var targetWidth = Math.Min(sourceWidth, NormalizedMaxWidth);
        var targetHeight = Math.Max(
            1,
            (int)Math.Round((double)sourceHeight * targetWidth / sourceWidth, MidpointRounding.AwayFromZero));

        return (targetWidth, targetHeight);
    }
}
