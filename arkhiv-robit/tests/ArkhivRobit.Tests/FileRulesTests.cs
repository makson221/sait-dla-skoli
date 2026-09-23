using ArkhivRobit.Services;

namespace ArkhivRobit.Tests;

public class FileRulesTests
{
    private readonly ArchiveOptions _options = new() { MaxFileSizeMb = 10 };

    [Theory]
    [InlineData("курсова.pdf")]
    [InlineData("Диплом.DOCX")]
    [InlineData("код.zip")]
    public void Validate_AcceptsAllowedFiles(string name) =>
        Assert.Null(FileRules.Validate(name, 1024, _options));

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("script.js")]
    [InlineData("без_розширення")]
    public void Validate_RejectsOtherExtensions(string name) =>
        Assert.NotNull(FileRules.Validate(name, 1024, _options));

    [Fact]
    public void Validate_RejectsEmptyAndTooLargeFiles()
    {
        Assert.NotNull(FileRules.Validate("a.pdf", 0, _options));
        Assert.NotNull(FileRules.Validate("a.pdf", 10L * 1024 * 1024 + 1, _options));
        Assert.Null(FileRules.Validate("a.pdf", 10L * 1024 * 1024, _options));
    }

    [Fact]
    public void CreateKey_UsesYearFolderAndLatinCharactersOnly()
    {
        var key = FileRules.CreateKey(2026, "Курсова робота.PDF");

        Assert.Matches("^works/2026/[0-9a-f]{32}\\.pdf$", key);
    }

    [Theory]
    [InlineData(@"C:\Users\teacher\Desktop\робота.pdf", "робота.pdf")]
    [InlineData("папка/робота.pdf", "робота.pdf")]
    [InlineData("робота.pdf", "робота.pdf")]
    public void SafeFileName_RemovesPath(string input, string expected) =>
        Assert.Equal(expected, FileRules.SafeFileName(input));

    [Theory]
    [InlineData(0, "0 КБ")]
    [InlineData(500, "1 КБ")]
    [InlineData(2048, "2 КБ")]
    [InlineData(5 * 1024 * 1024, "5,0 МБ")]
    public void FormatSize_IsHumanReadable(long bytes, string expected)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("uk-UA");
        try { Assert.Equal(expected, FileRules.FormatSize(bytes)); }
        finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
    }
}
