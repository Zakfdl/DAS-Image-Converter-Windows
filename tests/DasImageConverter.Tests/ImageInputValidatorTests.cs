using DasImageConverter.Core.Models;
using DasImageConverter.Core.Validation;

namespace DasImageConverter.Tests;

public sealed class ImageInputValidatorTests
{
    [Fact]
    public void ValidateFileCandidate_RejectsEmptyUnsupportedAndMissingFiles()
    {
        using var workspace = new TempWorkspace();
        var emptyPath = workspace.GetPath("empty.jpg");
        File.WriteAllBytes(emptyPath, []);
        var bmpPath = workspace.GetPath("sample.bmp");
        File.WriteAllBytes(bmpPath, [1, 2, 3, 4]);

        var (missingOk, _) = ImageInputValidator.ValidateFileCandidate(workspace.GetPath("missing.jpg"));
        var (emptyOk, emptyMsg) = ImageInputValidator.ValidateFileCandidate(emptyPath);
        var (bmpOk, bmpMsg) = ImageInputValidator.ValidateFileCandidate(bmpPath);

        Assert.False(missingOk);
        Assert.False(emptyOk);
        Assert.Contains("empty", emptyMsg, StringComparison.OrdinalIgnoreCase);
        Assert.False(bmpOk);
        Assert.Contains("not a supported", bmpMsg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateDimensions_Enforces60MegapixelCap()
    {
        var (okAtLimit, _) = ImageInputValidator.ValidateDimensions("max.png", 10_000, 6_000);
        var (overLimit, error) = ImageInputValidator.ValidateDimensions("huge.png", 10_000, 6_001);
        var (zeroDim, _) = ImageInputValidator.ValidateDimensions("zero.png", 0, 100);

        Assert.True(okAtLimit);
        Assert.False(overLimit);
        Assert.Contains("60 megapixels", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(zeroDim);
    }

    [Fact]
    public void IsDuplicate_MatchesSamePathOrSameFileNameAndSize()
    {
        var existing = new[]
        {
            new QueueImageItem
            {
                SourcePath = @"C:\temp\a\photo.png",
                FileName = "photo.png",
                FileSizeBytes = 4096,
                Width = 100,
                Height = 100,
            },
        };

        Assert.True(ImageInputValidator.IsDuplicate(existing, @"C:\temp\b\photo.png", 4096));
        Assert.False(ImageInputValidator.IsDuplicate(existing, @"C:\temp\b\photo.png", 8192));
    }

    [Fact]
    public void ConversionSettings_ComputeTargetDimensions_NeverUpscales()
    {
        var settings = new ConversionSettings
        {
            ResizeEnabled = true,
            MaxWidth = 1920,
        };

        Assert.Equal((1920, 1080), settings.ComputeTargetDimensions(3840, 2160));
        Assert.Equal((800, 600), settings.ComputeTargetDimensions(800, 600));

        var smallTarget = settings with { MaxWidth = 80 };
        Assert.Equal((80, 53), smallTarget.ComputeTargetDimensions(300, 200));
    }
}
