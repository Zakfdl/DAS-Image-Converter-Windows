using DasImageConverter.Core.Services;
using DasImageConverter.Core.ViewModels;
using DasImageConverter_App.Services;
using Microsoft.UI.Xaml;

namespace DasImageConverter_App;

/// <summary>
/// Application entry point configuring offline local services and the primary converter window.
/// </summary>
public partial class App : Application
{
    public static Window Window { get; private set; } = null!;

    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    public static nint WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(Window);

    public static MainConverterViewModel ViewModel { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DASImageConverter");
                Directory.CreateDirectory(logDir);
                File.WriteAllText(
                    Path.Combine(logDir, "last-error.log"),
                    $"{DateTimeOffset.Now:O}\n{e.Message}\n{e.Exception}");
            }
            catch
            {
            }
        };
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        var themeService = new ThemeService();
        var engine = new MagickImageProcessingEngine();
        var dialogs = new WinUIFileDialogService();

        ViewModel = new MainConverterViewModel(
            engine,
            dialogs,
            themeService,
            action =>
            {
                if (DispatcherQueue.HasThreadAccess)
                {
                    action();
                }
                else
                {
                    DispatcherQueue.TryEnqueue(() => action());
                }
            });

        Window = new MainWindow();
        themeService.ApplyTheme(ViewModel.ThemePreference);
        Window.Closed += (_, _) => ViewModel.Dispose();
        Window.Activate();

        var initialFiles = Environment.GetCommandLineArgs()
            .Skip(1)
            .Where(arg => !arg.StartsWith('-') && File.Exists(arg))
            .ToList();
        if (initialFiles.Count > 0)
        {
            _ = ViewModel.AddFilesAsync(initialFiles);
        }
    }
}
