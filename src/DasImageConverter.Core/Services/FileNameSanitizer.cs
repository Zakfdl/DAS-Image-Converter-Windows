using System.Globalization;
using System.Text.RegularExpressions;
using DasImageConverter.Core.Models;

namespace DasImageConverter.Core.Services;

/// <summary>
/// Sanitizes output filenames, guarantees collision-free batch names, and formats file sizes consistently.
/// </summary>
public static partial class FileNameSanitizer
{
    private static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    [GeneratedRegex(@"[^\p{L}\p{N}_\s-]+")]
    private static partial Regex DisallowedCharsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRunRegex();

    /// <summary>
    /// Normalizes a source filename into a safe base filename without extension.
    /// </summary>
    public static string SanitizeBaseName(string? fileName)
    {
        var nameOnly = Path.GetFileName(fileName ?? string.Empty);
        var withoutExtension = Path.GetFileNameWithoutExtension(nameOnly);
        var cleaned = DisallowedCharsRegex().Replace(withoutExtension, string.Empty).Trim();
        var collapsed = WhitespaceRunRegex().Replace(cleaned, "-").Trim('-');

        if (string.IsNullOrWhiteSpace(collapsed))
        {
            collapsed = "image";
        }

        if (WindowsReservedNames.Contains(collapsed))
        {
            collapsed = $"{collapsed}-image";
        }

        return collapsed;
    }

    /// <summary>
    /// Generates a collision-free output filename for the target format and records it in <paramref name="usedNames"/>.
    /// </summary>
    public static string MakeUniqueOutputFileName(
        string? sourceFileName,
        ImageFormatKind targetFormat,
        ISet<string> usedNames)
    {
        ArgumentNullException.ThrowIfNull(usedNames);

        var baseName = SanitizeBaseName(sourceFileName);
        var extension = targetFormat.GetExtension();
        var candidate = $"{baseName}{extension}";

        if (usedNames.Add(candidate))
        {
            return candidate;
        }

        var suffix = 2;
        while (true)
        {
            var nextCandidate = $"{baseName} ({suffix}){extension}";
            if (usedNames.Add(nextCandidate))
            {
                return nextCandidate;
            }
            suffix++;
        }
    }

    /// <summary>
    /// Formats a byte count matching the web application's display convention (B / KB / MB).
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        if (bytes < 0)
        {
            return "—";
        }

        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        const double kb = 1024.0;
        const double mb = kb * 1024.0;

        if (bytes < mb)
        {
            var valueKb = bytes / kb;
            var format = bytes < 10 * 1024 ? "0.0" : "0";
            return $"{valueKb.ToString(format, CultureInfo.InvariantCulture)} KB";
        }

        var valueMb = bytes / mb;
        var mbFormat = bytes < 10 * 1024 * 1024 ? "0.0" : "0";
        return $"{valueMb.ToString(mbFormat, CultureInfo.InvariantCulture)} MB";
    }

    /// <summary>
    /// Formats pixel dimensions (e.g., "1,920 × 1,080 px").
    /// </summary>
    public static string FormatDimensions(int width, int height) =>
        $"{width.ToString("N0", CultureInfo.InvariantCulture)} × {height.ToString("N0", CultureInfo.InvariantCulture)} px";

    /// <summary>
    /// Formats the percentage size difference between original and converted output.
    /// </summary>
    public static (string Text, bool IsLarger) FormatSizeDelta(long originalBytes, long outputBytes)
    {
        var delta = outputBytes - originalBytes;
        var percent = originalBytes > 0
            ? (int)Math.Round(Math.Abs(delta) / (double)originalBytes * 100.0, MidpointRounding.AwayFromZero)
            : 0;

        return delta <= 0
            ? ($"{percent}% smaller", false)
            : ($"{percent}% larger", true);
    }
}
