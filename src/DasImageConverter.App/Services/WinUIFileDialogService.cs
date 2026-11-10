using DasImageConverter.Core.Models;
using DasImageConverter.Core.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace DasImageConverter_App.Services;

/// <summary>
/// WinUI 3 implementation of file open and save pickers bound to the active application HWND.
/// </summary>
public sealed class WinUIFileDialogService : IFileDialogService
{
    public async Task<IReadOnlyList<string>> PickImageFilesAsync()
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".webp");
        picker.FileTypeFilter.Add(".avif");

        InitializeWithWindow.Initialize(picker, App.WindowHandle);
        var files = await picker.PickMultipleFilesAsync();
        if (files is null || files.Count == 0)
        {
            return [];
        }

        return files.Select(f => f.Path).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
    }

    public async Task<string?> PickSaveImagePathAsync(string suggestedFileName, ImageFormatKind format)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedFileName),
            DefaultFileExtension = format.GetExtension(),
        };
        picker.FileTypeChoices.Add($"{format.GetDisplayName()} Image", [format.GetExtension()]);

        InitializeWithWindow.Initialize(picker, App.WindowHandle);
        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    public async Task<string?> PickSaveZipPathAsync(string suggestedFileName)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.Downloads,
            SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedFileName),
            DefaultFileExtension = ".zip",
        };
        picker.FileTypeChoices.Add("ZIP Archive", [".zip"]);

        InitializeWithWindow.Initialize(picker, App.WindowHandle);
        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }
}
