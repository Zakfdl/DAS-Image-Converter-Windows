using DasImageConverter.Core.Models;

namespace DasImageConverter.Core.Services;

/// <summary>
/// Application theme preference options.
/// </summary>
public enum AppThemePreference
{
    System,
    Light,
    Dark,
}

/// <summary>
/// Abstraction for native file open and save pickers.
/// </summary>
public interface IFileDialogService
{
    Task<IReadOnlyList<string>> PickImageFilesAsync();

    Task<string?> PickSaveImagePathAsync(string suggestedFileName, ImageFormatKind format);

    Task<string?> PickSaveZipPathAsync(string suggestedFileName);
}

/// <summary>
/// Abstraction for reading/applying and persisting the app theme.
/// </summary>
public interface IThemeService
{
    AppThemePreference CurrentTheme { get; }

    void ApplyTheme(AppThemePreference preference);

    AppThemePreference LoadSavedTheme();
}
