using DasImageConverter.Core.Models;
using DasImageConverter.Core.Services;

namespace DasImageConverter.Core.Validation;

/// <summary>
/// Input file acceptance rules matching the DAS Image Converter specification.
/// </summary>
public static class ImageInputValidator
{
    public const int MaxFiles = 50;
    public const long MaxPixels = 60_000_000L;
    public const long MaxFileBytes = 100L * 1024 * 1024;

    /// <summary>
    /// Validates basic file metadata before attempting image decoding.
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateFileCandidate(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return (false, "The selected file could not be found.");
        }

        var fileInfo = new FileInfo(filePath);
        var fileName = fileInfo.Name;

        if (fileInfo.Length > MaxFileBytes)
        {
            return (false, $"“{fileName}” is larger than 100 MB.");
        }

        if (fileInfo.Length == 0)
        {
            return (false, $"“{fileName}” is empty and cannot be decoded.");
        }

        if (!ImageFormatExtensions.TryParseExtension(fileName, out _))
        {
            return (false, $"“{fileName}” is not a supported image file.");
        }

        return (true, null);
    }

    /// <summary>
    /// Validates decoded image dimensions against zero/overflow limits.
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateDimensions(string fileName, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return (false, $"“{fileName}” does not contain usable image dimensions.");
        }

        if ((long)width * height > MaxPixels)
        {
            return (false, $"“{fileName}” is too large to process safely (more than 60 megapixels).");
        }

        return (true, null);
    }

    /// <summary>
    /// Checks whether an incoming file duplicates an item already in the queue.
    /// </summary>
    public static bool IsDuplicate(IEnumerable<QueueImageItem> existingItems, string filePath, long fileSizeBytes)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        var fileName = Path.GetFileName(normalizedPath);

        return existingItems.Any(existing =>
            string.Equals(Path.GetFullPath(existing.SourcePath), normalizedPath, StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(existing.FileName, fileName, StringComparison.OrdinalIgnoreCase) &&
             existing.FileSizeBytes == fileSizeBytes));
    }

    /// <summary>
    /// Formats a concise multi-error summary notice matching the website UX.
    /// </summary>
    public static string SummarizeErrors(IReadOnlyList<string> errors)
    {
        if (errors.Count == 0)
        {
            return string.Empty;
        }

        if (errors.Count == 1)
        {
            return errors[0];
        }

        var remaining = errors.Count - 1;
        return $"{errors[0]} {remaining} more file{(remaining > 1 ? "s" : string.Empty)} were skipped.";
    }
}
