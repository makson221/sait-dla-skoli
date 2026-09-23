using ArkhivRobit.Services;

namespace ArkhivRobit.Tests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("КН-41", "КН-41")]
    [InlineData("кн-41", "КН-41")]
    [InlineData("кн 41", "КН-41")]
    [InlineData(" кн – 41 ", "КН-41")]
    [InlineData("іпз--31", "ІПЗ-31")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Group_NormalizesDifferentSpellings(string? input, string expected) =>
        Assert.Equal(expected, TextNormalizer.Group(input));

    [Fact]
    public void Clean_CollapsesWhitespace() =>
        Assert.Equal("Іваненко Петро Олегович", TextNormalizer.Clean("  Іваненко   Петро\tОлегович "));

    [Fact]
    public void SearchText_JoinsLowercasedPartsAndSkipsEmpty() =>
        Assert.Equal("тема роботи іваненко кн-41", TextNormalizer.SearchText("Тема  Роботи", "ІВАНЕНКО", null, " ", "КН-41"));
}
