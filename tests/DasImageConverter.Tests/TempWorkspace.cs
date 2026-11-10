using DasImageConverter.Core.Models;
using ImageMagick;

namespace DasImageConverter.Tests;

internal sealed class TempWorkspace : IDisposable
{
    public string RootPath { get; }

    public TempWorkspace()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "DASImageConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    public string GetPath(string fileName) => Path.Combine(RootPath, fileName);

    public string CreateGradientImage(string fileName, int width, int height, MagickFormat format, uint quality = 90)
    {
        var path = GetPath(fileName);
        using var image = new MagickImage(new MagickColor("#ef6b4d"), (uint)width, (uint)height);
        using var overlay = new MagickImage("gradient:#ef6b4d-#18212c", (uint)width, (uint)height);
        image.Composite(overlay, CompositeOperator.Over);
        image.Format = format;
        if (format != MagickFormat.Png)
        {
            image.Quality = quality;
        }
        image.Write(path);
        return path;
    }

    public string CreateTransparentBadge(string fileName, int width, int height, MagickFormat format)
    {
        var path = GetPath(fileName);
        using var image = new MagickImage(MagickColors.Transparent, (uint)width, (uint)height);
        // Solid teal square in the bottom-right quadrant while top-left stays fully transparent.
        using var patch = new MagickImage(new MagickColor("#47a786"), (uint)(width / 2), (uint)(height / 2));
        image.Composite(patch, width / 2, height / 2, CompositeOperator.Over);
        image.Format = format;
        if (format != MagickFormat.Png)
        {
            image.Quality = 92;
        }
        image.Write(path);
        return path;
    }

    public string CreateExifRotatedJpeg(string fileName, int rawWidth, int rawHeight, Orientation orientation)
    {
        var path = GetPath(fileName);
        using var image = new MagickImage("gradient:#ef6b4d-#47a786", (uint)rawWidth, (uint)rawHeight);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Orientation, (ushort)orientation);
        image.SetProfile(exif);
        image.Orientation = orientation;
        image.Format = MagickFormat.Jpeg;
        image.Quality = 90;
        image.Write(path);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            try
            {
                Directory.Delete(RootPath, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
