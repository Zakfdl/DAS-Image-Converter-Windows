using CommunityToolkit.Mvvm.ComponentModel;
using DasImageConverter.Core.Models;
using DasImageConverter.Core.Services;

namespace DasImageConverter.Core.ViewModels;

/// <summary>
/// Observable row model for a queued input image.
/// </summary>
public sealed partial class QueueItemViewModel : ObservableObject
{
    public QueueImageItem Model { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    [NotifyPropertyChangedFor(nameof(IsWorking))]
    [NotifyPropertyChangedFor(nameof(IsDone))]
    [NotifyPropertyChangedFor(nameof(IsFailed))]
    private QueueItemStatus _status;

    [ObservableProperty]
    private string? _errorMessage;

    public QueueItemViewModel(QueueImageItem model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _status = model.Status;
        _errorMessage = model.ErrorMessage;
        DetailsText = $"{FileNameSanitizer.FormatBytes(model.FileSizeBytes)} · {FileNameSanitizer.FormatDimensions(model.Width, model.Height)}";
    }

    public Guid Id => Model.Id;

    public string FileName => Model.FileName;

    public string DetailsText { get; }

    public byte[] ThumbnailBytes => Model.ThumbnailBytes;

    public bool IsReady => Status == QueueItemStatus.Ready;

    public bool IsWorking => Status == QueueItemStatus.Processing;

    public bool IsDone => Status == QueueItemStatus.Done;

    public bool IsFailed => Status == QueueItemStatus.Failed;

    public string StatusLabel => Status switch
    {
        QueueItemStatus.Ready => "Ready",
        QueueItemStatus.Processing => "Working…",
        QueueItemStatus.Done => "Done",
        QueueItemStatus.Failed => "Unsupported",
        _ => "Ready",
    };

    public void SyncFromModel()
    {
        Status = Model.Status;
        ErrorMessage = Model.ErrorMessage;
    }
}

/// <summary>
/// Observable row model for a converted output image.
/// </summary>
public sealed class ResultItemViewModel
{
    public ConvertedImageResult Model { get; }

    public ResultItemViewModel(ConvertedImageResult model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        OriginalSizeText = FileNameSanitizer.FormatBytes(model.OriginalSizeBytes);
        OutputSizeText = FileNameSanitizer.FormatBytes(model.OutputSizeBytes);
        DimensionsText = FileNameSanitizer.FormatDimensions(model.Width, model.Height);
        (DeltaText, IsLarger) = FileNameSanitizer.FormatSizeDelta(model.OriginalSizeBytes, model.OutputSizeBytes);
    }

    public Guid SourceItemId => Model.SourceItemId;

    public string OutputFileName => Model.OutputFileName;

    public string OriginalSizeText { get; }

    public string OutputSizeText { get; }

    public string DimensionsText { get; }

    public string DeltaText { get; }

    public bool IsLarger { get; }

    public bool IsNotLarger => !IsLarger;

    public byte[] PreviewBytes => Model.PreviewBytes;
}
