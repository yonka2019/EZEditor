using JsonEditor.Services;

namespace JsonEditor.Tests;

public class NumberValidationTests
{
    [Theory]
    [InlineData("0", true)]
    [InlineData("42", true)]
    [InlineData("-17", true)]
    [InlineData("3.14", true)]
    [InlineData("1.5e3", true)]
    [InlineData("12345678901234567890", true)]
    [InlineData("abc", false)]
    [InlineData("12abc", false)]
    [InlineData("1.2.3", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("true", false)]
    public void IsValidNumber_MatchesExpectation(string input, bool expected)
    {
        Assert.Equal(expected, JsonDocumentService.IsValidNumber(input));
    }
}
