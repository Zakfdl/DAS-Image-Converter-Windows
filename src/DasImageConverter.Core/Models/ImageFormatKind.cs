namespace DasImageConverter.Core.Models;

/// <summary>
/// Supported input and output image formats for DAS Image Converter.
/// </summary>
public enum ImageFormatKind
{
    Jpeg,
    Png,
    WebP,
    Avif,
}

/// <summary>
/// Metadata and mapping helpers for <see cref="ImageFormatKind"/>.
/// </summary>
public static class ImageFormatExtensions
{
    public static string GetExtension(this ImageFormatKind format) => format switch
    {
        ImageFormatKind.Jpeg => ".jpg",
        ImageFormatKind.Png => ".png",
        ImageFormatKind.WebP => ".webp",
        ImageFormatKind.Avif => ".avif",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static string GetDisplayName(this ImageFormatKind format) => format switch
    {
        ImageFormatKind.Jpeg => "JPEG",
        ImageFormatKind.Png => "PNG",
        ImageFormatKind.WebP => "WebP",
        ImageFormatKind.Avif => "AVIF",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static string GetMimeType(this ImageFormatKind format) => format switch
    {
        ImageFormatKind.Jpeg => "image/jpeg",
        ImageFormatKind.Png => "image/png",
        ImageFormatKind.WebP => "image/webp",
        ImageFormatKind.Avif => "image/avif",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static bool SupportsQuality(this ImageFormatKind format) => format != ImageFormatKind.Png;

    public static bool SupportsTransparency(this ImageFormatKind format) => format != ImageFormatKind.Jpeg;

    public static bool TryParseExtension(string? pathOrExtension, out ImageFormatKind format)
    {
        var ext = Path.GetExtension(pathOrExtension ?? string.Empty);
        if (string.IsNullOrEmpty(ext) && !string.IsNullOrWhiteSpace(pathOrExtension) && !pathOrExtension.Contains('.'))
        {
            ext = "." + pathOrExtension.Trim();
        }

        switch (ext.ToLowerInvariant())
        {
            case ".jpg":
            case ".jpeg":
                format = ImageFormatKind.Jpeg;
                return true;
            case ".png":
                format = ImageFormatKind.Png;
                return true;
            case ".webp":
                format = ImageFormatKind.WebP;
                return true;
            case ".avif":
                format = ImageFormatKind.Avif;
                return true;
            default:
                format = default;
                return false;
        }
    }
}
