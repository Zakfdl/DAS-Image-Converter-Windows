namespace DasImageConverter.Core.Models;

/// <summary>
/// Processing lifecycle state for a queued input image.
/// </summary>
public enum QueueItemStatus
{
    Ready,
    Processing,
    Done,
    Failed,
}

/// <summary>
/// Validated input image entry held in the conversion queue.
/// </summary>
public sealed class QueueImageItem
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string SourcePath { get; init; }

    public required string FileName { get; init; }

    public long FileSizeBytes { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public ImageFormatKind DetectedFormat { get; init; }

    public byte[] ThumbnailBytes { get; init; } = [];

    public QueueItemStatus Status { get; set; } = QueueItemStatus.Ready;

    public string? ErrorMessage { get; set; }
}
