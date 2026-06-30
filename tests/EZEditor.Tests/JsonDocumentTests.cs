using EZEditor.Models;
using EZEditor.Services;
using EZEditor.ViewModels;

namespace EZEditor.Tests;

public class JsonDocumentTests
{
    private static JsonDocument Make(string json)
    {
        var svc = new JsonDocumentService();
        return new JsonDocument(svc.Parse(json), svc);
    }

    [Fact]
    public void Roots_ContainsSingleRoot_AndFormatIsJson()
    {
        var doc = Make("""{ "a": 1 }""");
        Assert.Single(doc.Roots);
        Assert.Same(doc.Roots[0], doc.Root);
        Assert.Equal(DocumentFormat.Json, doc.Format);
    }

    [Fact]
    public void EditingTree_RaisesChanged()
    {
        var doc = Make("""{ "a": 1 }""");
        var fired = 0;
        doc.Changed += (_, _) => fired++;
        doc.Root.Children[0].Value = "2";
        Assert.True(fired >= 1);
    }

    [Fact]
    public void Serialize_RoundTripsValueEdit()
    {
        var doc = Make("""{ "a": 1 }""");
        doc.Root.Children[0].Value = "2";
        Assert.Contains("\"a\": 2", doc.Serialize());
    }

    [Fact]
    public void ApplyFilter_DelegatesToRoot()
    {
        var doc = Make("""{ "name": "Alice", "age": 30 }""");
        doc.ApplyFilter("age");
        Assert.True(doc.Root.Children.First(c => c.Name == "name").IsFilteredOut);
        Assert.False(doc.Root.Children.First(c => c.Name == "age").IsFilteredOut);
    }
}
