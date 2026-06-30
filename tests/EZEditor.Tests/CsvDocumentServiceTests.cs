using EZEditor.Services;

namespace EZEditor.Tests;

public class CsvDocumentServiceTests
{
    private readonly CsvDocumentService _svc = new();

    [Fact]
    public void ParseRows_SimpleGrid()
    {
        var rows = _svc.ParseRows("name,age\nAlice,30\nBob,25", ',');
        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { "name", "age" }, rows[0].ToArray());
        Assert.Equal(new[] { "Bob", "25" }, rows[2].ToArray());
    }

    [Fact]
    public void ParseRows_QuotedFieldWithCommaAndQuote()
    {
        // field: She said "hi", and left   -> "She said ""hi"", and left"
        var rows = _svc.ParseRows("a,b\n\"She said \"\"hi\"\", and left\",2", ',');
        Assert.Equal("She said \"hi\", and left", rows[1][0]);
        Assert.Equal("2", rows[1][1]);
    }

    [Fact]
    public void ParseRows_QuotedFieldWithEmbeddedNewline()
    {
        var rows = _svc.ParseRows("a\n\"line1\nline2\"", ',');
        Assert.Equal(2, rows.Count);
        Assert.Equal("line1\nline2", rows[1][0]);
    }

    [Fact]
    public void ParseRows_IgnoresTrailingNewline()
    {
        var rows = _svc.ParseRows("a,b\n1,2\n", ',');
        Assert.Equal(2, rows.Count);
    }

    [Theory]
    [InlineData("a;b;c\n1;2;3", ';')]
    [InlineData("a\tb\n1\t2", '\t')]
    [InlineData("a,b\n1,2", ',')]
    public void DetectDelimiter_PicksMostFrequentOnFirstLine(string text, char expected)
        => Assert.Equal(expected, CsvDocumentService.DetectDelimiter(text));

    [Fact]
    public void Serialize_QuotesOnlyWhenNeeded_AndRoundTrips()
    {
        var header = new[] { "name", "note" };
        var rows = new IReadOnlyList<string>[]
        {
            new[] { "Alice", "plain" },
            new[] { "Bob", "has,comma" },
            new[] { "Cara", "has \"quote\"" },
        };
        var text = _svc.Serialize(header, rows, ',', hasHeader: true);
        Assert.Equal(
            "name,note\r\nAlice,plain\r\nBob,\"has,comma\"\r\nCara,\"has \"\"quote\"\"\"",
            text);

        var reparsed = _svc.ParseRows(text, ',');
        Assert.Equal("has,comma", reparsed[2][1]);
        Assert.Equal("has \"quote\"", reparsed[3][1]);
    }

    [Fact]
    public void Serialize_NoHeader_OmitsHeaderRow()
    {
        var rows = new IReadOnlyList<string>[] { new[] { "1", "2" } };
        var text = _svc.Serialize(new[] { "a", "b" }, rows, ',', hasHeader: false);
        Assert.Equal("1,2", text);
    }
}
