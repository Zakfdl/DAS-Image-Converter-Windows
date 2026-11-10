using DasImageConverter.Core.Models;

namespace DasImageConverter.Core.Services;

/// <summary>
/// Abstraction for local image inspection, decoding, resizing, compositing, and encoding.
/// </summary>
public interface IImageProcessingEngine
{
    /// <summary>
    /// Validates and inspects an input image file, generating a compact preview thumbnail.
    /// </summary>
    Task<QueueImageItem> InspectAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a queued image according to <paramref name="settings"/> and writes it to <paramref name="outputPath"/>.
    /// </summary>
    Task<ConvertedImageResult> ConvertAsync(
        QueueImageItem item,
        string outputFileName,
        string outputPath,
        ConversionSettings settings,
        CancellationToken cancellationToken = default);
}
