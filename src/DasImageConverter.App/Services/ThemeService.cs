using DasImageConverter.Core.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Storage;
using Windows.UI;

namespace DasImageConverter_App.Services;

/// <summary>
/// Persists and applies Light / Dark / System theme preferences across the root window and titlebar.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private const string SettingsKey = "AppThemePreference";
    private static readonly string FallbackSettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DASImageConverter",
        "theme.txt");

    public AppThemePreference CurrentTheme { get; private set; } = AppThemePreference.System;

    public AppThemePreference LoadSavedTheme()
    {
        string? raw = null;
        try
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(SettingsKey, out var value))
            {
                raw = value as string;
            }
        }
        catch
        {
            if (File.Exists(FallbackSettingsFile))
            {
                raw = File.ReadAllText(FallbackSettingsFile).Trim();
            }
        }

        CurrentTheme = Enum.TryParse<AppThemePreference>(raw, ignoreCase: true, out var parsed)
            ? parsed
            : AppThemePreference.System;

        return CurrentTheme;
    }

    public void ApplyTheme(AppThemePreference preference)
    {
        CurrentTheme = preference;
        SaveTheme(preference);

        if (App.Window?.Content is FrameworkElement root)
        {
            root.RequestedTheme = preference switch
            {
                AppThemePreference.Light => ElementTheme.Light,
                AppThemePreference.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };

            UpdateTitleBarColors(root.ActualTheme);
        }
    }

    public static void UpdateTitleBarColors(ElementTheme actualTheme)
    {
        if (App.Window is null || !AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = App.Window.AppWindow.TitleBar;
        var isDark = actualTheme == ElementTheme.Dark;
        var fg = isDark ? Color.FromArgb(255, 241, 245, 249) : Color.FromArgb(255, 24, 33, 44);
        var hoverBg = isDark ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(20, 24, 33, 44);
        var pressedBg = isDark ? Color.FromArgb(45, 255, 255, 255) : Color.FromArgb(35, 24, 33, 44);
        var inactiveFg = isDark ? Color.FromArgb(140, 148, 163, 184) : Color.FromArgb(140, 104, 115, 131);

        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = fg;
        titleBar.ButtonHoverForegroundColor = fg;
        titleBar.ButtonPressedForegroundColor = fg;
        titleBar.ButtonInactiveForegroundColor = inactiveFg;
        titleBar.ButtonHoverBackgroundColor = hoverBg;
        titleBar.ButtonPressedBackgroundColor = pressedBg;
    }

    private static void SaveTheme(AppThemePreference preference)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values[SettingsKey] = preference.ToString();
        }
        catch
        {
            var dir = Path.GetDirectoryName(FallbackSettingsFile);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(FallbackSettingsFile, preference.ToString());
        }
    }
}
