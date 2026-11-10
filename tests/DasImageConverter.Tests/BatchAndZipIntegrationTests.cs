using System.IO.Compression;
using DasImageConverter.Core.Models;
using DasImageConverter.Core.Services;
using ImageMagick;

namespace DasImageConverter.Tests;

public sealed class BatchAndZipIntegrationTests
{
    private readonly MagickImageProcessingEngine _engine = new();

    [Fact]
    public async Task ConvertBatchAsync_IsolatesPerItemErrorAndPreservesValidOutputs()
    {
        using var workspace = new TempWorkspace();
        var validPath1 = workspace.CreateGradientImage("one.png", 100, 80, MagickFormat.Png);
        var validPath2 = workspace.CreateGradientImage("two.jpg", 100, 80, MagickFormat.Jpeg);

        var item1 = await _engine.InspectAsync(validPath1);
        var item2 = await _engine.InspectAsync(validPath2);
        var brokenItem = new QueueImageItem
        {
            SourcePath = workspace.GetPath("deleted-after-add.png"),
            FileName = "deleted-after-add.png",
            FileSizeBytes = 123,
            Width = 50,
            Height = 50,
            DetectedFormat = ImageFormatKind.Png,
        };

        var batchService = new BatchConversionService(_engine);
        var batchDir = workspace.GetPath("batch-out");
        var outcome = await batchService.ConvertBatchAsync(
            [item1, brokenItem, item2],
            new ConversionSettings { OutputFormat = ImageFormatKind.WebP, Quality = 80 },
            batchDir);

        Assert.False(outcome.WasCancelled);
        Assert.Equal(2, outcome.Results.Count);
        Assert.Single(outcome.Failures);
        Assert.Equal(QueueItemStatus.Done, item1.Status);
        Assert.Equal(QueueItemStatus.Failed, brokenItem.Status);
        Assert.Equal(QueueItemStatus.Done, item2.Status);
    }

    [Fact]
    public async Task ConvertBatchAsync_SupportsCancellationWhileKeepingCompletedOutputs()
    {
        using var workspace = new TempWorkspace();
        var items = new List<QueueImageItem>();
        for (var i = 0; i < 8; i++)
        {
            var path = workspace.CreateGradientImage($"img-{i}.png", 400, 300, MagickFormat.Png);
            items.Add(await _engine.InspectAsync(path));
        }

        using var cts = new CancellationTokenSource();
        var batchService = new BatchConversionService(_engine);
        var produced = 0;
        var outcome = await batchService.ConvertBatchAsync(
            items,
            new ConversionSettings { OutputFormat = ImageFormatKind.Avif, Quality = 75 },
            workspace.GetPath("cancel-batch"),
            onResultProduced: _ =>
            {
                if (Interlocked.Increment(ref produced) >= 1)
                {
                    cts.Cancel();
                }
            },
            cancellationToken: cts.Token);

        Assert.True(outcome.WasCancelled);
        Assert.NotEmpty(outcome.Results);
        Assert.True(outcome.Results.Count < items.Count);
    }

    [Fact]
    public async Task ZipExportService_WritesValidArchiveWithUniqueEntryNames()
    {
        using var workspace = new TempWorkspace();
        var path1 = workspace.CreateGradientImage("photo.png", 64, 64, MagickFormat.Png);
        var path2 = workspace.CreateGradientImage("photo.jpg", 64, 64, MagickFormat.Jpeg);

        var item1 = await _engine.InspectAsync(path1);
        var item2 = await _engine.InspectAsync(path2);

        var batchService = new BatchConversionService(_engine);
        var outcome = await batchService.ConvertBatchAsync(
            [item1, item2],
            new ConversionSettings { OutputFormat = ImageFormatKind.WebP, Quality = 82 },
            workspace.GetPath("zip-batch"));

        Assert.Equal("photo.webp", outcome.Results[0].OutputFileName);
        Assert.Equal("photo (2).webp", outcome.Results[1].OutputFileName);

        var zipPath = workspace.GetPath(ZipExportService.BuildDefaultZipFileName(new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero)));
        Assert.EndsWith("DAS-converted-2026-11-10.zip", zipPath, StringComparison.Ordinal);

        var zipService = new ZipExportService();
        await zipService.CreateZipAsync(outcome.Results, zipPath);

        using var archive = ZipFile.OpenRead(zipPath);
        Assert.Equal(2, archive.Entries.Count);
        Assert.Equal("photo.webp", archive.Entries[0].FullName);
        Assert.Equal("photo (2).webp", archive.Entries[1].FullName);

        using var entryStream = archive.Entries[0].Open();
        using var ms = new MemoryStream();
        await entryStream.CopyToAsync(ms);
        Assert.Equal(await File.ReadAllBytesAsync(outcome.Results[0].OutputFilePath), ms.ToArray());
    }
}
