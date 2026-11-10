using System.Collections.Concurrent;
using DasImageConverter.Core.Models;

namespace DasImageConverter.Core.Services;

/// <summary>
/// Coordinates bounded-concurrency batch conversion, cooperative cancellation, and error isolation.
/// </summary>
public sealed class BatchConversionService
{
    private readonly IImageProcessingEngine _engine;

    public BatchConversionService(IImageProcessingEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <summary>
    /// Converts all ready items in <paramref name="items"/> into a dedicated temporary batch folder.
    /// </summary>
    public async Task<BatchConversionOutcome> ConvertBatchAsync(
        IReadOnlyList<QueueImageItem> items,
        ConversionSettings settings,
        string? batchDirectory = null,
        IProgress<BatchProgress>? progress = null,
        Action<QueueImageItem>? onItemStateChanged = null,
        Action<ConvertedImageResult>? onResultProduced = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(settings);

        var targetDirectory = batchDirectory
            ?? Path.Combine(Path.GetTempPath(), "DASImageConverter", "Batches", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(targetDirectory);

        var readyItems = items.Where(i => i.Status != QueueItemStatus.Failed).ToList();
        if (readyItems.Count == 0)
        {
            return new BatchConversionOutcome(targetDirectory, [], [], false);
        }

        // Pre-assign collision-free filenames deterministically in queue order.
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var assignments = readyItems
            .Select((item, index) => (
                Index: index,
                Item: item,
                OutputFileName: FileNameSanitizer.MakeUniqueOutputFileName(item.FileName, settings.OutputFormat, usedNames)))
            .ToList();

        // AVIF encoding is CPU/memory heavy, so bound concurrency tighter than JPEG/PNG/WebP.
        var maxWorkers = settings.OutputFormat == ImageFormatKind.Avif
            ? Math.Clamp(Environment.ProcessorCount / 4, 1, 2)
            : Math.Clamp(Environment.ProcessorCount / 2, 1, 4);

        using var semaphore = new SemaphoreSlim(maxWorkers, maxWorkers);
        var resultsByIndex = new ConcurrentDictionary<int, ConvertedImageResult>();
        var failuresByIndex = new ConcurrentDictionary<int, BatchItemFailure>();
        var completedCount = 0;
        var cancelledFlag = 0;

        var tasks = assignments.Select(async entry =>
        {
            var (index, item, outputFileName) = entry;
            var acquired = false;
            try
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                acquired = true;
                cancellationToken.ThrowIfCancellationRequested();

                item.Status = QueueItemStatus.Processing;
                item.ErrorMessage = null;
                onItemStateChanged?.Invoke(item);
                progress?.Report(new BatchProgress(Volatile.Read(ref completedCount), readyItems.Count, item.FileName, item.Id));

                var outputPath = Path.Combine(targetDirectory, outputFileName);
                var result = await _engine.ConvertAsync(item, outputFileName, outputPath, settings, cancellationToken)
                    .ConfigureAwait(false);

                item.Status = QueueItemStatus.Done;
                onItemStateChanged?.Invoke(item);

                resultsByIndex[index] = result;
                onResultProduced?.Invoke(result);
            }
            catch (OperationCanceledException)
            {
                Interlocked.Exchange(ref cancelledFlag, 1);
                if (item.Status == QueueItemStatus.Processing)
                {
                    item.Status = QueueItemStatus.Ready;
                    onItemStateChanged?.Invoke(item);
                }
            }
            catch (Exception ex)
            {
                item.Status = QueueItemStatus.Failed;
                item.ErrorMessage = ex.Message;
                onItemStateChanged?.Invoke(item);
                failuresByIndex[index] = new BatchItemFailure(item.Id, item.FileName, ex.Message);
            }
            finally
            {
                if (acquired)
                {
                    semaphore.Release();
                }

                if (!cancellationToken.IsCancellationRequested && Volatile.Read(ref cancelledFlag) == 0)
                {
                    var doneNow = Interlocked.Increment(ref completedCount);
                    progress?.Report(new BatchProgress(doneNow, readyItems.Count, item.FileName, item.Id));
                }
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);

        var orderedResults = resultsByIndex
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => kvp.Value)
            .ToList();

        var orderedFailures = failuresByIndex
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => kvp.Value)
            .ToList();

        return new BatchConversionOutcome(
            targetDirectory,
            orderedResults,
            orderedFailures,
            cancellationToken.IsCancellationRequested || Volatile.Read(ref cancelledFlag) == 1);
    }

    /// <summary>
    /// Best-effort cleanup of a temporary batch output folder.
    /// </summary>
    public static void TryCleanupBatchDirectory(string? batchDirectory)
    {
        if (string.IsNullOrWhiteSpace(batchDirectory) || !Directory.Exists(batchDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(batchDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
