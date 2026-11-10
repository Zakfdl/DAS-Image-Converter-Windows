using DasImageConverter_App.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace DasImageConverter_App;

/// <summary>
/// Main application window hosting the DAS Image Converter workspace.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }
        ConfigureWindowBounds();

        if (Content is FrameworkElement root)
        {
            root.ActualThemeChanged += (s, _) => ThemeService.UpdateTitleBarColors(s.ActualTheme);
        }

        RootFrame.Navigate(typeof(MainPage));
    }

    private void ConfigureWindowBounds()
    {
        var display = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var targetWidth = Math.Min(1120, Math.Max(860, (int)(display.WorkArea.Width * 0.72)));
        var targetHeight = Math.Min(880, Math.Max(680, (int)(display.WorkArea.Height * 0.84)));

        AppWindow.Resize(new SizeInt32(targetWidth, targetHeight));

        var x = display.WorkArea.X + (display.WorkArea.Width - targetWidth) / 2;
        var y = display.WorkArea.Y + (display.WorkArea.Height - targetHeight) / 2;
        AppWindow.Move(new PointInt32(x, y));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 640;
            presenter.PreferredMinimumHeight = 560;
        }
    }
}
