using System.Diagnostics;
using System.IO.Compression;
using DasImageConverter.Core.Models;
using DasImageConverter.Core.Services;
using DasImageConverter.Core.Validation;
using ImageMagick;

namespace DasImageConverter.Tests;

public sealed class BatchPerformanceAndOfflineTests
{
    [Fact]
    public async Task BatchConversion_Processes50ImagesAndBuildsZipWithinHealthyBudget()
    {
        using var workspace = new TempWorkspace();
        var engine = new MagickImageProcessingEngine();

        // Create 3 distinct source masters and copy into 50 unique inputs to keep fixture setup fast.
        var masterJpg = workspace.CreateGradientImage("master.jpg", 1200, 900, MagickFormat.Jpeg);
        var masterPng = workspace.CreateTransparentBadge("master.png", 1200, 900, MagickFormat.Png);
        var masterWebp = workspace.CreateGradientImage("master.webp", 1200, 900, MagickFormat.WebP);

        var inputs = new List<string>(ImageInputValidator.MaxFiles);
        for (var i = 0; i < ImageInputValidator.MaxFiles; i++)
        {
            var src = (i % 3) switch
            {
                0 => masterJpg,
                1 => masterPng,
                _ => masterWebp,
            };
            var dest = workspace.GetPath($"batch-{i:D2}{Path.GetExtension(src)}");
            File.Copy(src, dest);
            inputs.Add(dest);
        }

        var items = new List<QueueImageItem>(ImageInputValidator.MaxFiles);
        foreach (var path in inputs)
        {
            items.Add(await engine.InspectAsync(path));
        }
        Assert.Equal(50, items.Count);

        var stopwatch = Stopwatch.StartNew();
        var batchService = new BatchConversionService(engine);
        var progressUpdates = 0;
        var outcome = await batchService.ConvertBatchAsync(
            items,
            new ConversionSettings
            {
                OutputFormat = ImageFormatKind.WebP,
                Quality = 82,
                ResizeEnabled = true,
                MaxWidth = 800,
            },
            workspace.GetPath("perf-out"),
            new Progress<BatchProgress>(_ => Interlocked.Increment(ref progressUpdates)));

        var zipPath = workspace.GetPath("batch-50.zip");
        var zipService = new ZipExportService();
        await zipService.CreateZipAsync(outcome.Results, zipPath);
        stopwatch.Stop();

        Assert.False(outcome.WasCancelled);
        Assert.Empty(outcome.Failures);
        Assert.Equal(50, outcome.Results.Count);
        Assert.All(outcome.Results, r =>
        {
            Assert.Equal(800, r.Width);
            Assert.Equal(600, r.Height);
            Assert.True(File.Exists(r.OutputFilePath));
        });
        Assert.True(progressUpdates >= 50);

        using var archive = ZipFile.OpenRead(zipPath);
        Assert.Equal(50, archive.Entries.Count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(45), $"50-image batch took too long: {stopwatch.Elapsed}");
    }

    [Fact]
    public void CoreAssemblyAndAppManifest_HaveZeroNetworkDependencies()
    {
        var repoRoot = FindRepoRoot();
        var manifestPath = Path.Combine(repoRoot, "src", "DasImageConverter.App", "Package.appxmanifest");
        var manifestXml = File.ReadAllText(manifestPath);

        Assert.DoesNotContain("internetClient", manifestXml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ads.js", manifestXml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Windows.Universal", manifestXml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Windows.Desktop", manifestXml, StringComparison.Ordinal);
        Assert.DoesNotContain("CN=DAS", manifestXml, StringComparison.Ordinal);
        Assert.Contains("Berrizak.DASImageConverter", manifestXml, StringComparison.Ordinal);

        var appCsprojPath = Path.Combine(repoRoot, "src", "DasImageConverter.App", "DasImageConverter.App.csproj");
        var appCsproj = File.ReadAllText(appCsprojPath);
        Assert.Contains("<SelfContained>true</SelfContained>", appCsproj, StringComparison.Ordinal);
        Assert.DoesNotContain("Version=\"*\"", appCsproj, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.WindowsAppSDK.ML", appCsproj, StringComparison.Ordinal);

        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "Notice.txt")), "Expected Magick.NET Notice.txt in output directory.");

        var coreDir = Path.Combine(repoRoot, "src", "DasImageConverter.Core");
        foreach (var csFile in Directory.GetFiles(coreDir, "*.cs", SearchOption.AllDirectories))
        {
            var code = File.ReadAllText(csFile);
            Assert.DoesNotContain("HttpClient", code, StringComparison.Ordinal);
            Assert.DoesNotContain("img.berrizak.com", code, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("googlesyndication", code, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DASImageConverter.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate DASImageConverter.sln");
    }
}
