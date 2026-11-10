using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DasImageConverter.Core.Models;
using DasImageConverter.Core.Services;
using DasImageConverter.Core.Validation;

namespace DasImageConverter.Core.ViewModels;

/// <summary>
/// Primary application ViewModel coordinating queue state, settings, conversion, theme, and exports.
/// </summary>
public sealed partial class MainConverterViewModel : ObservableObject, IDisposable
{
    private readonly IImageProcessingEngine _engine;
    private readonly BatchConversionService _batchService;
    private readonly ZipExportService _zipService;
    private readonly IFileDialogService _fileDialogs;
    private readonly IThemeService _themeService;
    private readonly Action<Action> _runOnUiThread;
    private readonly object _resultsSync = new();

    private CancellationTokenSource? _activeBatchCts;
    private string? _activeBatchDirectory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    [NotifyPropertyChangedFor(nameof(CanEditMaxWidth))]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseFilesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportZipCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    private string _noticeMessage = string.Empty;

    [ObservableProperty]
    private bool _isDragOver;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedOutputFormat))]
    [NotifyPropertyChangedFor(nameof(IsQualityVisible))]
    [NotifyPropertyChangedFor(nameof(IsJpegNoteVisible))]
    private int _selectedFormatIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QualityDisplayText))]
    private double _quality = ConversionSettings.DefaultQuality;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditMaxWidth))]
    private bool _resizeEnabled;

    [ObservableProperty]
    private double _maxWidth = ConversionSettings.DefaultResizeWidth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(JpegBackgroundHex))]
    private RgbColor _jpegBackground = RgbColor.White;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExportZipButtonText))]
    private bool _isExportingZip;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeLabel))]
    private AppThemePreference _themePreference;

    public MainConverterViewModel(
        IImageProcessingEngine engine,
        IFileDialogService fileDialogs,
        IThemeService themeService,
        Action<Action>? runOnUiThread = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _fileDialogs = fileDialogs ?? throw new ArgumentNullException(nameof(fileDialogs));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _batchService = new BatchConversionService(_engine);
        _zipService = new ZipExportService();
        _runOnUiThread = runOnUiThread ?? (action => action());

        _themePreference = _themeService.LoadSavedTheme();

        Items.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(ReadyCountText));
            OnPropertyChanged(nameof(CanConvert));
            ConvertCommand.NotifyCanExecuteChanged();
        };

        Results.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasResults));
            ExportZipCommand.NotifyCanExecuteChanged();
        };
    }

    public ObservableCollection<QueueItemViewModel> Items { get; } = [];

    public ObservableCollection<ResultItemViewModel> Results { get; } = [];

    public IReadOnlyList<string> FormatOptions { get; } = ["JPEG", "PNG", "WebP", "AVIF"];

    public ImageFormatKind SelectedOutputFormat => SelectedFormatIndex switch
    {
        0 => ImageFormatKind.Jpeg,
        1 => ImageFormatKind.Png,
        2 => ImageFormatKind.WebP,
        3 => ImageFormatKind.Avif,
        _ => ImageFormatKind.Jpeg,
    };

    public bool IsNotBusy => !IsBusy;

    public bool CanEditMaxWidth => !IsBusy && ResizeEnabled;

    public bool HasNotice => !string.IsNullOrWhiteSpace(NoticeMessage);

    public bool HasItems => Items.Count > 0;

    public bool HasResults => Results.Count > 0;

    public bool IsQualityVisible => SelectedOutputFormat.SupportsQuality();

    public bool IsJpegNoteVisible => SelectedOutputFormat == ImageFormatKind.Jpeg;

    public string JpegBackgroundHex => JpegBackground.ToHex();

    public string QualityDisplayText => Math.Clamp((int)Math.Round(Quality), ConversionSettings.MinQuality, ConversionSettings.MaxQuality).ToString();

    public string ExportZipButtonText => IsExportingZip ? "Preparing ZIP…" : "Download ZIP";

    public bool CanConvert => !IsBusy && Items.Any(i => i.Status != QueueItemStatus.Failed);

    public string ReadyCountText
    {
        get
        {
            var count = Items.Count(i => i.Status != QueueItemStatus.Failed);
            return $"{count} image{(count == 1 ? string.Empty : "s")} ready";
        }
    }

    public string ThemeLabel => ThemePreference switch
    {
        AppThemePreference.Light => "Light",
        AppThemePreference.Dark => "Dark",
        _ => "System",
    };

    public ConversionSettings BuildCurrentSettings()
    {
        var widthValue = double.IsNaN(MaxWidth) || MaxWidth < 1
            ? ConversionSettings.DefaultResizeWidth
            : (int)Math.Round(MaxWidth, MidpointRounding.AwayFromZero);

        return new ConversionSettings
        {
            OutputFormat = SelectedOutputFormat,
            Quality = Math.Clamp((int)Math.Round(Quality, MidpointRounding.AwayFromZero), ConversionSettings.MinQuality, ConversionSettings.MaxQuality),
            ResizeEnabled = ResizeEnabled,
            MaxWidth = Math.Clamp(widthValue, ConversionSettings.MinResizeWidth, ConversionSettings.MaxResizeWidth),
            JpegBackgroundColor = JpegBackground,
        };
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task BrowseFilesAsync()
    {
        var picked = await _fileDialogs.PickImageFilesAsync();
        if (picked.Count > 0)
        {
            await AddFilesAsync(picked);
        }
    }

    public async Task AddFilesAsync(IReadOnlyList<string> filePaths)
    {
        if (IsBusy || filePaths.Count == 0)
        {
            return;
        }

        NoticeMessage = string.Empty;

        var remaining = ImageInputValidator.MaxFiles - Items.Count;
        if (remaining <= 0)
        {
            NoticeMessage = $"You can convert up to {ImageInputValidator.MaxFiles} files at a time.";
            return;
        }

        var accepted = filePaths.Take(remaining).ToList();
        var skippedOverflow = filePaths.Count > remaining;
        var errors = new List<string>();
        var candidates = new List<string>();

        var existingModels = Items.Select(vm => vm.Model).ToList();
        var seenInDrop = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in accepted)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(path);
            if (!seenInDrop.Add(fullPath))
            {
                continue;
            }

            var (validMeta, metaError) = ImageInputValidator.ValidateFileCandidate(fullPath);
            if (!validMeta)
            {
                errors.Add(metaError!);
                continue;
            }

            var size = new FileInfo(fullPath).Length;
            if (ImageInputValidator.IsDuplicate(existingModels, fullPath, size))
            {
                continue;
            }

            candidates.Add(fullPath);
        }

        if (candidates.Count > 0)
        {
            // Inspect up to 4 files concurrently so large drops feel responsive without spiking RAM.
            using var gate = new SemaphoreSlim(4, 4);
            var inspected = new (int Index, QueueImageItem? Item, string? Error)[candidates.Count];

            var tasks = candidates.Select(async (path, idx) =>
            {
                await gate.WaitAsync();
                try
                {
                    var model = await _engine.InspectAsync(path);
                    inspected[idx] = (idx, model, null);
                }
                catch (Exception ex)
                {
                    inspected[idx] = (idx, null, ex.Message);
                }
                finally
                {
                    gate.Release();
                }
            });

            await Task.WhenAll(tasks);

            foreach (var entry in inspected.OrderBy(e => e.Index))
            {
                if (entry.Item is not null)
                {
                    if (!ImageInputValidator.IsDuplicate(Items.Select(x => x.Model), entry.Item.SourcePath, entry.Item.FileSizeBytes))
                    {
                        Items.Add(new QueueItemViewModel(entry.Item));
                    }
                }
                else if (!string.IsNullOrWhiteSpace(entry.Error))
                {
                    errors.Add(entry.Error);
                }
            }
        }

        if (skippedOverflow)
        {
            errors.Add($"Only {ImageInputValidator.MaxFiles} files can be selected at once.");
        }

        if (errors.Count > 0)
        {
            NoticeMessage = ImageInputValidator.SummarizeErrors(errors);
        }
    }

    [RelayCommand]
    private void RemoveItem(QueueItemViewModel? item)
    {
        if (item is null || IsBusy)
        {
            return;
        }

        Items.Remove(item);
        if (Items.Count == 0)
        {
            NoticeMessage = string.Empty;
        }
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private void ClearAll()
    {
        Items.Clear();
        ClearPreviousResults();
        NoticeMessage = string.Empty;
        ProgressPercent = 0;
        ProgressText = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAsync()
    {
        if (IsBusy || Items.Count == 0)
        {
            return;
        }

        ClearPreviousResults();
        NoticeMessage = string.Empty;

        foreach (var item in Items)
        {
            if (item.Model.Status != QueueItemStatus.Failed)
            {
                item.Model.Status = QueueItemStatus.Ready;
                item.Model.ErrorMessage = null;
                item.SyncFromModel();
            }
        }

        var settings = BuildCurrentSettings();
        var batchDir = Path.Combine(Path.GetTempPath(), "DASImageConverter", "Batches", Guid.NewGuid().ToString("N"));
        _activeBatchDirectory = batchDir;

        var queueOrderById = Items
            .Select((item, idx) => (item.Id, idx))
            .ToDictionary(pair => pair.Id, pair => pair.idx);

        var totalReady = Items.Count(i => i.Status != QueueItemStatus.Failed);
        ProgressPercent = 0;
        ProgressText = $"Converting 0 of {totalReady}…";
        IsBusy = true;

        _activeBatchCts = new CancellationTokenSource();
        var ct = _activeBatchCts.Token;

        try
        {
            var progress = new Progress<BatchProgress>(snapshot =>
            {
                _runOnUiThread(() =>
                {
                    var pct = snapshot.TotalCount > 0
                        ? Math.Clamp(snapshot.CompletedCount * 100.0 / snapshot.TotalCount, 0, 100)
                        : 0;
                    ProgressPercent = pct;
                    ProgressText = $"Converting {snapshot.CompletedCount} of {snapshot.TotalCount}…";
                });
            });

            var outcome = await _batchService.ConvertBatchAsync(
                Items.Select(x => x.Model).ToList(),
                settings,
                batchDir,
                progress,
                onItemStateChanged: model =>
                {
                    _runOnUiThread(() =>
                    {
                        var vm = Items.FirstOrDefault(x => x.Id == model.Id);
                        vm?.SyncFromModel();
                        OnPropertyChanged(nameof(ReadyCountText));
                    });
                },
                onResultProduced: result =>
                {
                    _runOnUiThread(() => InsertResultInQueueOrder(result, queueOrderById));
                },
                ct);

            if (outcome.WasCancelled)
            {
                NoticeMessage = "Conversion cancelled. Completed files are still available below.";
                ProgressText = $"Cancelled ({outcome.Results.Count} completed)";
            }
            else if (outcome.Failures.Count > 0)
            {
                NoticeMessage = ImageInputValidator.SummarizeErrors(
                    outcome.Failures.Select(f => f.ErrorMessage).ToList());
                ProgressPercent = 100;
                ProgressText = $"Completed {outcome.Results.Count} of {totalReady} ({outcome.Failures.Count} failed)";
            }
            else
            {
                ProgressPercent = 100;
                ProgressText = $"Completed {outcome.Results.Count} image{(outcome.Results.Count == 1 ? string.Empty : "s")}";
            }
        }
        catch (Exception ex)
        {
            NoticeMessage = ex.Message;
        }
        finally
        {
            _activeBatchCts?.Dispose();
            _activeBatchCts = null;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _activeBatchCts?.Cancel();
    }

    [RelayCommand]
    private async Task SaveResultAsync(ResultItemViewModel? resultVm)
    {
        if (resultVm is null || !File.Exists(resultVm.Model.OutputFilePath))
        {
            return;
        }

        var destination = await _fileDialogs.PickSaveImagePathAsync(
            resultVm.OutputFileName,
            resultVm.Model.OutputFormat);

        if (string.IsNullOrWhiteSpace(destination))
        {
            return;
        }

        try
        {
            await using var source = new FileStream(resultVm.Model.OutputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            await using var dest = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            await source.CopyToAsync(dest);
        }
        catch (Exception ex)
        {
            NoticeMessage = $"Could not save “{resultVm.OutputFileName}”: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanExportZip))]
    private async Task ExportZipAsync()
    {
        if (!CanExportZip())
        {
            return;
        }

        var suggestedName = ZipExportService.BuildDefaultZipFileName();
        var destination = await _fileDialogs.PickSaveZipPathAsync(suggestedName);
        if (string.IsNullOrWhiteSpace(destination))
        {
            return;
        }

        IsExportingZip = true;
        ExportZipCommand.NotifyCanExecuteChanged();
        try
        {
            await _zipService.CreateZipAsync(Results.Select(r => r.Model), destination);
        }
        catch (Exception)
        {
            NoticeMessage = "The ZIP could not be created. Save the completed files individually instead.";
        }
        finally
        {
            IsExportingZip = false;
            ExportZipCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanExportZip() => !IsBusy && !IsExportingZip && Results.Count > 0;

    [RelayCommand]
    private void CycleTheme()
    {
        var next = ThemePreference switch
        {
            AppThemePreference.System => AppThemePreference.Light,
            AppThemePreference.Light => AppThemePreference.Dark,
            _ => AppThemePreference.System,
        };

        ThemePreference = next;
        _themeService.ApplyTheme(next);
    }

    public void SetTheme(AppThemePreference preference)
    {
        ThemePreference = preference;
        _themeService.ApplyTheme(preference);
    }

    private void InsertResultInQueueOrder(
        ConvertedImageResult result,
        IReadOnlyDictionary<Guid, int> queueOrderById)
    {
        lock (_resultsSync)
        {
            var targetOrder = queueOrderById.GetValueOrDefault(result.SourceItemId, int.MaxValue);
            var insertAt = 0;
            while (insertAt < Results.Count &&
                   queueOrderById.GetValueOrDefault(Results[insertAt].SourceItemId, int.MaxValue) < targetOrder)
            {
                insertAt++;
            }

            Results.Insert(insertAt, new ResultItemViewModel(result));
        }
    }

    private void ClearPreviousResults()
    {
        Results.Clear();
        BatchConversionService.TryCleanupBatchDirectory(_activeBatchDirectory);
        _activeBatchDirectory = null;
    }

    public void Dispose()
    {
        _activeBatchCts?.Cancel();
        _activeBatchCts?.Dispose();
        BatchConversionService.TryCleanupBatchDirectory(_activeBatchDirectory);
    }
}
