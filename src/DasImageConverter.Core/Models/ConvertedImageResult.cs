namespace DasImageConverter.Core.Models;

/// <summary>
/// Successfully converted output image written to the batch working directory.
/// </summary>
public sealed record ConvertedImageResult
{
    public required Guid SourceItemId { get; init; }

    public required string SourceFileName { get; init; }

    public required string OutputFileName { get; init; }

    public required ImageFormatKind OutputFormat { get; init; }

    public required string OutputFilePath { get; init; }

    public required long OriginalSizeBytes { get; init; }

    public required long OutputSizeBytes { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    public required byte[] PreviewBytes { get; init; }
}

/// <summary>
/// Per-item error captured when a file fails during batch conversion.
/// </summary>
public sealed record BatchItemFailure(Guid SourceItemId, string FileName, string ErrorMessage);

/// <summary>
/// Live batch progress snapshot reported to the UI.
/// </summary>
public sealed record BatchProgress(int CompletedCount, int TotalCount, string? CurrentFileName, Guid? CurrentItemId);

/// <summary>
/// Aggregate outcome of a batch conversion run.
/// </summary>
public sealed record BatchConversionOutcome(
    string BatchDirectory,
    IReadOnlyList<ConvertedImageResult> Results,
    IReadOnlyList<BatchItemFailure> Failures,
    bool WasCancelled);
