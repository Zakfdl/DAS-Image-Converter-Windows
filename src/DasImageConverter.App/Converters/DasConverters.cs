using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using DasImageConverter.Core.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;

namespace DasImageConverter_App.Converters;

/// <summary>
/// Converts raw thumbnail PNG/JPEG bytes into cached WinUI <see cref="BitmapImage"/> instances.
/// </summary>
public sealed class BytesToImageConverter : IValueConverter
{
    private static readonly ConditionalWeakTable<byte[], BitmapImage> Cache = new();

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not byte[] bytes || bytes.Length == 0)
        {
            return null;
        }

        if (Cache.TryGetValue(bytes, out var cached))
        {
            return cached;
        }

        var bitmap = new BitmapImage();
        using var stream = new MemoryStream(bytes);
        bitmap.SetSource(stream.AsRandomAccessStream());
        Cache.AddOrUpdate(bytes, bitmap);
        return bitmap;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility.Visible;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is not Visibility.Visible;
}

public sealed class RgbToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is RgbColor rgb ? Color.FromArgb(255, rgb.R, rgb.G, rgb.B) : Colors.White;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Color color ? new RgbColor(color.R, color.G, color.B) : RgbColor.White;
}

public sealed class RgbToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is RgbColor rgb ? new SolidColorBrush(Color.FromArgb(255, rgb.R, rgb.G, rgb.B)) : new SolidColorBrush(Colors.White);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>
/// Generates a 62x54 pixel-exact checkerboard ImageSource for transparent preview backdrops.
/// </summary>
public static class CheckerboardHelper
{
    public static WriteableBitmap CreateCheckerboardBitmap(bool isDark, int width = 62, int height = 54, int cellSize = 6)
    {
        var bmp = new WriteableBitmap(width, height);
        var pixels = new byte[width * height * 4];

        // BGRA byte order for WriteableBitmap
        var (lR, lG, lB) = isDark ? ((byte)27, (byte)36, (byte)48) : ((byte)240, (byte)242, (byte)245);
        var (dR, dG, dB) = isDark ? ((byte)37, (byte)49, (byte)66) : ((byte)231, (byte)235, (byte)239);

        for (var y = 0; y < height; y++)
        {
            var rowOdd = (y / cellSize) % 2 == 1;
            for (var x = 0; x < width; x++)
            {
                var colOdd = (x / cellSize) % 2 == 1;
                var useDark = rowOdd ^ colOdd;
                var idx = (y * width + x) * 4;
                pixels[idx] = useDark ? dB : lB;
                pixels[idx + 1] = useDark ? dG : lG;
                pixels[idx + 2] = useDark ? dR : lR;
                pixels[idx + 3] = 255;
            }
        }

        using var stream = bmp.PixelBuffer.AsStream();
        stream.Write(pixels, 0, pixels.Length);
        return bmp;
    }
}
