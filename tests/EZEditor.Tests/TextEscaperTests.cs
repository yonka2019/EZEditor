using EZEditor.Services;

namespace EZEditor.Tests;

public class TextEscaperTests
{
    // --- Escape ---

    [Theory]
    [InlineData("line1\nline2", @"line1\nline2")]
    [InlineData("a\rb", @"a\rb")]
    [InlineData("a\tb", @"a\tb")]
    [InlineData("a\bb", @"a\bb")]
    [InlineData("a\fb", @"a\fb")]
    [InlineData("C:\\temp", @"C:\\temp")]
    [InlineData("regex \\d+", @"regex \\d+")]
    [InlineData("\r\n", @"\r\n")]
    public void Escape_MapsSpecialCharacters(string input, string expected)
        => Assert.Equal(expected, TextEscaper.Escape(input));

    [Theory]
    [InlineData(0x01)]
    [InlineData(0x0B)] // vertical tab has no short escape
    [InlineData(0x1F)]
    public void Escape_OtherControlCharsUseUnicodeForm(int code)
    {
        var input = "a" + (char)code + "b";
        var expected = "a" + "\\u" + code.ToString("x4") + "b";
        Assert.Equal(expected, TextEscaper.Escape(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain text")]
    [InlineData("שלום 世界 🙂")]
    public void Escape_NoSpecialChars_ReturnsSameInstance(string input)
        => Assert.Same(input, TextEscaper.Escape(input));

    // --- Unescape ---

    [Theory]
    [InlineData(@"line1\nline2", "line1\nline2")]
    [InlineData(@"a\rb", "a\rb")]
    [InlineData(@"a\tb", "a\tb")]
    [InlineData(@"a\bb", "a\bb")]
    [InlineData(@"a\fb", "a\fb")]
    [InlineData(@"C:\\temp", "C:\\temp")]
    public void Unescape_MapsEscapeSequences(string input, string expected)
        => Assert.Equal(expected, TextEscaper.Unescape(input));

    [Fact]
    public void Unescape_UnicodeForm_MapsToChar()
    {
        Assert.Equal("A", TextEscaper.Unescape("\\u0041"));
        Assert.Equal("\n", TextEscaper.Unescape("\\u000A")); // hex digits case-insensitive
        Assert.Equal(((char)0x1F).ToString(), TextEscaper.Unescape("\\u001f"));
    }

    [Theory]
    [InlineData(@"a\qb")]      // unknown escape
    [InlineData("trailing\\")] // backslash at end
    [InlineData(@"\uZZZZ")]    // bad hex
    [InlineData(@"\u12")]      // too short
    public void Unescape_InvalidSequences_KeptLiterally(string input)
        => Assert.Equal(input, TextEscaper.Unescape(input));

    [Theory]
    [InlineData("")]
    [InlineData("plain text")]
    public void Unescape_NoEscapes_ReturnsSameInstance(string input)
        => Assert.Same(input, TextEscaper.Unescape(input));

    // --- Round-trip ---

    [Theory]
    [InlineData("multi\nline\twith\\slash")]
    [InlineData(@"literal \n stays literal")]
    [InlineData("plain")]
    public void RoundTrip_UnescapeOfEscape_IsIdentity(string original)
        => Assert.Equal(original, TextEscaper.Unescape(TextEscaper.Escape(original)));

    [Fact]
    public void RoundTrip_ControlChar_IsIdentity()
    {
        var original = "a" + (char)0x07 + "b";
        Assert.Equal(original, TextEscaper.Unescape(TextEscaper.Escape(original)));
    }
}
