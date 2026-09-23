using ArkhivRobit.Services;

namespace ArkhivRobit.Tests;

public class AccessCodesTests
{
    [Fact]
    public void Generate_ReturnsCodeInExpectedFormat()
    {
        var code = AccessCodes.Generate();

        Assert.Matches("^[A-Z2-9]{4}-[A-Z2-9]{4}$", code);
        Assert.DoesNotContain('0', code);
        Assert.DoesNotContain('O', code);
        Assert.DoesNotContain('1', code);
        Assert.DoesNotContain('I', code);
    }

    [Fact]
    public void Generate_ReturnsDifferentCodes()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => AccessCodes.Generate()).ToHashSet();
        Assert.Equal(200, codes.Count);
    }

    [Theory]
    [InlineData("abcd-efgh")]
    [InlineData("ABCD EFGH")]
    [InlineData("  AbCdEfGh ")]
    [InlineData("ABCD–EFGH")]
    public void Matches_IgnoresCaseSpacesAndDashes(string input) =>
        Assert.True(AccessCodes.Matches(input, "ABCD-EFGH"));

    [Theory]
    [InlineData("ABCD-EFGX")]
    [InlineData("")]
    [InlineData(null)]
    public void Matches_RejectsWrongCode(string? input) =>
        Assert.False(AccessCodes.Matches(input, "ABCD-EFGH"));

    [Fact]
    public void Matches_RejectsWhenExpectedCodeIsEmpty() =>
        Assert.False(AccessCodes.Matches("", ""));

    [Fact]
    public void Hash_IsStableAndDoesNotContainCode()
    {
        var hash = AccessCodes.Hash("abcd-efgh");

        Assert.Equal(AccessCodes.Hash("ABCDEFGH"), hash);
        Assert.Equal(64, hash.Length);
        Assert.DoesNotContain("ABCD", hash);
    }
}
