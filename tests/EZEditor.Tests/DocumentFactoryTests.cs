using EZEditor.Services;
using EZEditor.ViewModels;

namespace EZEditor.Tests;

public class DocumentFactoryTests
{
    [Theory]
    [InlineData("<root><a>1</a></root>", DocumentFormat.Xml)]
    [InlineData("  \n<?xml version=\"1.0\"?><r/>", DocumentFormat.Xml)]
    [InlineData("{ \"a\": 1 }", DocumentFormat.Json)]
    [InlineData("[1, 2, 3]", DocumentFormat.Json)]
    [InlineData("name,age\nAlice,30", DocumentFormat.Csv)]
    public void Detect_ClassifiesByContent(string text, DocumentFormat expected)
        => Assert.Equal(expected, DocumentFactory.Detect(text));

    [Theory]
    [InlineData("", ".json", DocumentFormat.Json)]
    [InlineData("", ".xml", DocumentFormat.Xml)]
    [InlineData("", ".csv", DocumentFormat.Csv)]
    public void Detect_EmptyContent_FallsBackToExtension(string text, string ext, DocumentFormat expected)
        => Assert.Equal(expected, DocumentFactory.Detect(text, ext));

    [Theory]
    [InlineData("{ broken", ".json", DocumentFormat.Json)]
    [InlineData("just plain text", ".xml", DocumentFormat.Xml)]
    [InlineData("just plain text", null, DocumentFormat.Csv)]
    public void Detect_AmbiguousContent_ExtensionDecides(string text, string? ext, DocumentFormat expected)
        => Assert.Equal(expected, DocumentFactory.Detect(text, ext));

    [Fact]
    public void LoadAuto_JsonFile_ReturnsJsonDocument()
    {
        var path = Path.Combine(Path.GetTempPath(), $"df_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "a": 1 }""");
        try
        {
            var doc = new DocumentFactory().LoadAuto(path);
            Assert.IsType<JsonDocument>(doc);
        }
        finally { File.Delete(path); }
    }
}
