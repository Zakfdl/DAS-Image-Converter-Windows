using System.IO.Compression;
using DasImageConverter.Core.Models;
using DasImageConverter.Core.Services;
using DasImageConverter.Core.ViewModels;
using ImageMagick;

namespace DasImageConverter.Tests;

public sealed class MainConverterViewModelTests
{
    private sealed class StubFileDialogService : IFileDialogService
    {
        public IReadOnlyList<string> FilesToPick { get; set; } = [];
        public string? SaveImagePath { get; set; }
        public string? SaveZipPath { get; set; }

        public Task<IReadOnlyList<string>> PickImageFilesAsync() => Task.FromResult(FilesToPick);

        public Task<string?> PickSaveImagePathAsync(string suggestedFileName, ImageFormatKind format) =>
            Task.FromResult(SaveImagePath);

        public Task<string?> PickSaveZipPathAsync(string suggestedFileName) =>
            Task.FromResult(SaveZipPath);
    }

    private sealed class StubThemeService : IThemeService
    {
        public AppThemePreference CurrentTheme { get; private set; } = AppThemePreference.System;

        public void ApplyTheme(AppThemePreference preference) => CurrentTheme = preference;

        public AppThemePreference LoadSavedTheme() => CurrentTheme;
    }

    [Fact]
    public void FormatSelection_TogglesQualityAndJpegNoteVisibility()
    {
        using var vm = new MainConverterViewModel(
            new MagickImageProcessingEngine(),
            new StubFileDialogService(),
            new StubThemeService());

        Assert.Equal(ImageFormatKind.Jpeg, vm.SelectedOutputFormat);
        Assert.True(vm.IsQualityVisible);
        Assert.True(vm.IsJpegNoteVisible);

        vm.SelectedFormatIndex = 1; // PNG
        Assert.Equal(ImageFormatKind.Png, vm.SelectedOutputFormat);
        Assert.False(vm.IsQualityVisible);
        Assert.False(vm.IsJpegNoteVisible);

        vm.SelectedFormatIndex = 2; // WebP
        Assert.True(vm.IsQualityVisible);
        Assert.False(vm.IsJpegNoteVisible);

        vm.SelectedFormatIndex = 3; // AVIF
        Assert.True(vm.IsQualityVisible);
        Assert.False(vm.IsJpegNoteVisible);
    }

    [Fact]
    public async Task AddFilesAndConvertAndExport_ExecutesFullEndToEndWorkflow()
    {
        using var workspace = new TempWorkspace();
        var pngPath = workspace.CreateTransparentBadge("icon.png", 120, 120, MagickFormat.Png);
        var jpgPath = workspace.CreateGradientImage("banner.jpg", 300, 150, MagickFormat.Jpeg);
        var badPath = workspace.GetPath("broken.jpg");
        File.WriteAllText(badPath, "not a jpeg");

        var dialogs = new StubFileDialogService
        {
            FilesToPick = [pngPath, jpgPath, badPath],
            SaveImagePath = workspace.GetPath("saved-single.webp"),
            SaveZipPath = workspace.GetPath("exported-batch.zip"),
        };
        var themes = new StubThemeService();

        using var vm = new MainConverterViewModel(
            new MagickImageProcessingEngine(),
            dialogs,
            themes);

        await vm.BrowseFilesCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Items.Count);
        Assert.True(vm.HasItems);
        Assert.True(vm.HasNotice);
        Assert.Equal("2 images ready", vm.ReadyCountText);
        Assert.True(vm.CanConvert);

        vm.SelectedFormatIndex = 2; // WebP
        vm.ResizeEnabled = true;
        vm.MaxWidth = 100;

        await vm.ConvertCommand.ExecuteAsync(null);

        Assert.False(vm.IsBusy);
        Assert.Equal(2, vm.Results.Count);
        Assert.True(vm.HasResults);
        Assert.Equal(100, vm.Results[0].Model.Width);
        Assert.Equal(100, vm.Results[1].Model.Width);
        Assert.Equal(50, vm.Results[1].Model.Height);

        await vm.SaveResultCommand.ExecuteAsync(vm.Results[0]);
        Assert.True(File.Exists(dialogs.SaveImagePath));

        await vm.ExportZipCommand.ExecuteAsync(null);
        Assert.True(File.Exists(dialogs.SaveZipPath));
        using var zip = ZipFile.OpenRead(dialogs.SaveZipPath!);
        Assert.Equal(2, zip.Entries.Count);

        vm.CycleThemeCommand.Execute(null);
        Assert.Equal(AppThemePreference.Light, vm.ThemePreference);
        vm.CycleThemeCommand.Execute(null);
        Assert.Equal(AppThemePreference.Dark, vm.ThemePreference);
        Assert.Equal(AppThemePreference.Dark, themes.CurrentTheme);

        vm.ClearAllCommand.Execute(null);
        Assert.Empty(vm.Items);
        Assert.Empty(vm.Results);
        Assert.False(vm.HasNotice);
    }

    [Fact]
    public async Task AddFilesAsync_Enforces50FileMaximumAcrossDrops()
    {
        using var workspace = new TempWorkspace();
        var master = workspace.CreateGradientImage("base.png", 32, 32, MagickFormat.Png);
        var files = new List<string>();
        for (var i = 0; i < 53; i++)
        {
            var copy = workspace.GetPath($"item-{i:D2}.png");
            File.Copy(master, copy);
            files.Add(copy);
        }

        using var vm = new MainConverterViewModel(
            new MagickImageProcessingEngine(),
            new StubFileDialogService(),
            new StubThemeService());

        await vm.AddFilesAsync(files);

        Assert.Equal(50, vm.Items.Count);
        Assert.Contains("Only 50 files", vm.NoticeMessage, StringComparison.OrdinalIgnoreCase);

        await vm.AddFilesAsync([files[51]]);
        Assert.Equal(50, vm.Items.Count);
        Assert.Contains("up to 50 files", vm.NoticeMessage, StringComparison.OrdinalIgnoreCase);
    }
}
