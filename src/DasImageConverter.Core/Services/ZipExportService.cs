using System.IO.Compression;
using System.Text;
using DasImageConverter.Core.Models;

namespace DasImageConverter.Core.Services;

/// <summary>
/// Builds local ZIP archives containing converted images without network dependencies.
/// </summary>
public sealed class ZipExportService
{
    public static string BuildDefaultZipFileName(DateTimeOffset? date = null)
    {
        var stamp = (date ?? DateTimeOffset.Now).ToString("yyyy-MM-dd");
        return $"DAS-converted-{stamp}.zip";
    }

    public async Task CreateZipAsync(
        IEnumerable<ConvertedImageResult> results,
        string destinationZipPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationZipPath);

        var parentDir = Path.GetDirectoryName(destinationZipPath);
        if (!string.IsNullOrEmpty(parentDir))
        {
            Directory.CreateDirectory(parentDir);
        }

        var tempZipPath = destinationZipPath + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                tempZipPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await CreateZipToStreamAsync(results, stream, cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempZipPath, destinationZipPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempZipPath))
            {
                try { File.Delete(tempZipPath); } catch (IOException) { }
            }
            throw;
        }
    }

    public async Task CreateZipToStreamAsync(
        IEnumerable<ConvertedImageResult> results,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(outputStream);

        var resultList = results.ToList();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var archive = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8);
        foreach (var result in resultList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(result.OutputFilePath))
            {
                throw new FileNotFoundException(
                    $"Converted file “{result.OutputFileName}” could not be found.",
                    result.OutputFilePath);
            }

            var entryName = EnsureUniqueEntryName(result.OutputFileName, result.OutputFormat, usedNames);
            var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);

            await using var entryStream = entry.Open();
            await using var sourceStream = new FileStream(
                result.OutputFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

            await sourceStream.CopyToAsync(entryStream, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string EnsureUniqueEntryName(
        string candidateName,
        ImageFormatKind format,
        ISet<string> usedNames)
    {
        if (usedNames.Add(candidateName))
        {
            return candidateName;
        }

        return FileNameSanitizer.MakeUniqueOutputFileName(candidateName, format, usedNames);
    }
}
