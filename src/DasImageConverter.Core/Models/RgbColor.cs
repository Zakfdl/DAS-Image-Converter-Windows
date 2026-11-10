using System.Globalization;

namespace DasImageConverter.Core.Models;

/// <summary>
/// Immutable 24-bit RGB color used for opaque compositing (e.g., JPEG transparency background).
/// </summary>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public static RgbColor White => new(255, 255, 255);

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public static bool TryParseHex(string? hex, out RgbColor color)
    {
        color = White;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        var trimmed = hex.Trim();
        if (trimmed.StartsWith('#'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.Length != 6 ||
            !byte.TryParse(trimmed[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
            !byte.TryParse(trimmed[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
            !byte.TryParse(trimmed[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }

        color = new RgbColor(r, g, b);
        return true;
    }
}
