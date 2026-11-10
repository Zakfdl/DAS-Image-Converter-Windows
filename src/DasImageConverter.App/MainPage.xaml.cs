using System.ComponentModel;
using DasImageConverter.Core.Models;
using DasImageConverter.Core.ViewModels;
using DasImageConverter_App.Converters;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;

namespace DasImageConverter_App;

/// <summary>
/// Primary workspace page for DAS Image Converter.
/// </summary>
public sealed partial class MainPage : Page
{
    private WriteableBitmap? _lightCheckerboard;
    private WriteableBitmap? _darkCheckerboard;

    public MainConverterViewModel ViewModel => App.ViewModel;

    public MainPage()
    {
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private WriteableBitmap GetCheckerboardBitmap(ElementTheme theme)
    {
        if (theme == ElementTheme.Dark)
        {
            return _darkCheckerboard ??= CheckerboardHelper.CreateCheckerboardBitmap(isDark: true);
        }

        return _lightCheckerboard ??= CheckerboardHelper.CreateCheckerboardBitmap(isDark: false);
    }

    private void CheckerboardImage_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Image image)
        {
            image.Source = GetCheckerboardBitmap(image.ActualTheme);
        }
    }

    private void CheckerboardImage_ActualThemeChanged(FrameworkElement sender, object args)
    {
        if (sender is Image image)
        {
            image.Source = GetCheckerboardBitmap(image.ActualTheme);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainConverterViewModel.IsBusy))
        {
            DropZoneCard.Opacity = ViewModel.IsBusy ? 0.66 : 1.0;
            if (!ViewModel.IsBusy && ViewModel.HasResults)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ResultsCard.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true });
                });
            }
        }
    }

    private void DropZone_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!ViewModel.IsBusy && ViewModel.BrowseFilesCommand.CanExecute(null))
        {
            ViewModel.BrowseFilesCommand.Execute(null);
        }
    }

    private void DropZone_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if ((e.Key == VirtualKey.Enter || e.Key == VirtualKey.Space) &&
            !ViewModel.IsBusy &&
            ViewModel.BrowseFilesCommand.CanExecute(null))
        {
            e.Handled = true;
            ViewModel.BrowseFilesCommand.Execute(null);
        }
    }

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        if (ViewModel.IsBusy)
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            if (e.DragUIOverride is not null)
            {
                e.DragUIOverride.Caption = "Add images";
                e.DragUIOverride.IsCaptionVisible = true;
                e.DragUIOverride.IsGlyphVisible = true;
            }
            ViewModel.IsDragOver = true;
        }
    }

    private void Root_DragLeave(object sender, DragEventArgs e)
    {
        ViewModel.IsDragOver = false;
    }

    private async void Root_Drop(object sender, DragEventArgs e)
    {
        ViewModel.IsDragOver = false;
        if (ViewModel.IsBusy || !e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var storageItems = await e.DataView.GetStorageItemsAsync();
        var filePaths = storageItems
            .OfType<StorageFile>()
            .Select(f => f.Path)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (filePaths.Count > 0)
        {
            await ViewModel.AddFilesAsync(filePaths);
        }
    }

    private void RemoveItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: QueueItemViewModel item })
        {
            ViewModel.RemoveItemCommand.Execute(item);
        }
    }

    private void SaveResultButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ResultItemViewModel result })
        {
            ViewModel.SaveResultCommand.Execute(result);
        }
    }

    private void PresetColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string hex } && RgbColor.TryParseHex(hex, out var rgb))
        {
            ViewModel.JpegBackground = rgb;
        }
    }

    private void CycleThemeButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CycleThemeCommand.Execute(null);
    }

    private async void OpenNotices_Click(object sender, RoutedEventArgs e)
    {
        var noticesPath = Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.txt");
        var fullNoticePath = Path.Combine(AppContext.BaseDirectory, "Notice.txt");
        var text = File.Exists(noticesPath)
            ? await File.ReadAllTextAsync(noticesPath)
            : "DAS Image Converter — Offline Local Image Processing\nPowered by Magick.NET (Apache 2.0), ImageMagick, and Windows App SDK.";

        var dialog = new ContentDialog
        {
            Title = "Third-Party Notices",
            Content = new ScrollViewer
            {
                MaxHeight = 360,
                Content = new TextBlock
                {
                    Text = text,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                    FontSize = 12,
                },
            },
            SecondaryButtonText = File.Exists(fullNoticePath) ? "Full Notice.txt" : string.Empty,
            CloseButtonText = "Close",
            XamlRoot = XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Secondary && File.Exists(fullNoticePath))
        {
            var fullText = await File.ReadAllTextAsync(fullNoticePath);
            var noticeBox = new TextBox
            {
                Text = fullText,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Height = 380,
                MinWidth = 460,
            };
            ScrollViewer.SetVerticalScrollBarVisibility(noticeBox, ScrollBarVisibility.Auto);

            var fullDialog = new ContentDialog
            {
                Title = "ImageMagick & Codec Notices (Notice.txt)",
                Content = noticeBox,
                CloseButtonText = "Close",
                XamlRoot = XamlRoot,
            };
            await fullDialog.ShowAsync();
        }
    }
}
