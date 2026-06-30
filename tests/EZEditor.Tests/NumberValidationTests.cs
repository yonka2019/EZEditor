using EZEditor.Services;

namespace EZEditor.Tests;

public class NumberValidationTests
{
    [Theory]
    [InlineData("0", true)]
    [InlineData("42", true)]
    [InlineData("-17", true)]
    [InlineData("3.14", true)]
    [InlineData("1.5e3", true)]
    [InlineData("12345678901234567890", true)]
    [InlineData("1e500", true)]        // valid JSON number, overflows double
    [InlineData("1e-500", true)]
    [InlineData("-0", true)]
    [InlineData("abc", false)]
    [InlineData("12abc", false)]
    [InlineData("1.2.3", false)]
    [InlineData("1.", false)]          // no fractional digits
    [InlineData(".5", false)]          // no integer part
    [InlineData("NaN", false)]
    [InlineData("Infinity", false)]
    [InlineData("1,000", false)]       // thousands separator is not JSON
    [InlineData("(5)", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("true", false)]
    public void IsValidNumber_MatchesExpectation(string input, bool expected)
    {
        Assert.Equal(expected, JsonDocumentService.IsValidNumber(input));
    }
}
