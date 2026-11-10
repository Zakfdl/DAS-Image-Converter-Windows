using DasImageConverter.Core.Models;
using DasImageConverter.Core.Services;

namespace DasImageConverter.Tests;

public sealed class FileNameSanitizerTests
{
    [Theory]
    [InlineData("my photo (1).png", "my-photo-1")]
    [InlineData("  hero banner  .jpeg", "hero-banner")]
    [InlineData("café_menu.webp", "café_menu")]
    [InlineData("!!!.png", "image")]
    [InlineData("CON.jpg", "CON-image")]
    [InlineData("lpt1.avif", "lpt1-image")]
    public void SanitizeBaseName_NormalizesAndProtectsReservedNames(string input, string expected)
    {
        Assert.Equal(expected, FileNameSanitizer.SanitizeBaseName(input));
    }

    [Fact]
    public void MakeUniqueOutputFileName_DisambiguatesCollisionsCaseInsensitively()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var first = FileNameSanitizer.MakeUniqueOutputFileName("photo.jpg", ImageFormatKind.WebP, used);
        var second = FileNameSanitizer.MakeUniqueOutputFileName("photo.png", ImageFormatKind.WebP, used);
        var third = FileNameSanitizer.MakeUniqueOutputFileName("PHOTO.avif", ImageFormatKind.WebP, used);

        Assert.Equal("photo.webp", first);
        Assert.Equal("photo (2).webp", second);
        Assert.Equal("PHOTO (3).webp", third);
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(25600, "25 KB")]
    [InlineData(2 * 1024 * 1024, "2.0 MB")]
    [InlineData(15 * 1024 * 1024, "15 MB")]
    public void FormatBytes_MatchesExpectedScale(long bytes, string expected)
    {
        Assert.Equal(expected, FileNameSanitizer.FormatBytes(bytes));
    }

    [Fact]
    public void FormatSizeDelta_DistinguishesSmallerAndLarger()
    {
        var (smallerText, isLargerSmall) = FileNameSanitizer.FormatSizeDelta(1000, 400);
        var (largerText, isLargerBig) = FileNameSanitizer.FormatSizeDelta(1000, 1250);

        Assert.Equal("60% smaller", smallerText);
        Assert.False(isLargerSmall);
        Assert.Equal("25% larger", largerText);
        Assert.True(isLargerBig);
    }
}
