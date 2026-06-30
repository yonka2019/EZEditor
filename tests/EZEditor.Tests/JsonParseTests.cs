using System.Text.Json;
using EZEditor.Models;
using EZEditor.Services;

namespace EZEditor.Tests;

public class JsonParseTests
{
    private readonly JsonDocumentService _svc = new();

    [Fact]
    public void Parse_Object_WithEachScalarKind()
    {
        var root = _svc.Parse("""
            { "s": "hi", "n": 30, "b": true, "z": null }
            """);

        Assert.Equal(JsonNodeKind.Object, root.Kind);
        Assert.Equal(4, root.Children.Count);

        Assert.Equal(JsonNodeKind.String, root.Children[0].Kind);
        Assert.Equal("hi", root.Children[0].Value);

        Assert.Equal(JsonNodeKind.Number, root.Children[1].Kind);
        Assert.Equal("30", root.Children[1].Value);

        Assert.Equal(JsonNodeKind.Boolean, root.Children[2].Kind);
        Assert.Equal("true", root.Children[2].Value);

        Assert.Equal(JsonNodeKind.Null, root.Children[3].Kind);
        Assert.Null(root.Children[3].Value);
    }

    [Fact]
    public void Parse_NestedArrayAndObject()
    {
        var root = _svc.Parse("""{ "list": [1, {"x": "y"}] }""");
        var list = root.Children[0];
        Assert.Equal(JsonNodeKind.Array, list.Kind);
        Assert.Equal(2, list.Children.Count);
        Assert.Equal(JsonNodeKind.Number, list.Children[0].Kind);
        Assert.Equal(JsonNodeKind.Object, list.Children[1].Kind);
        Assert.Equal("x", list.Children[1].Children[0].Name);
    }

    [Fact]
    public void Parse_PreservesKeyOrder()
    {
        var root = _svc.Parse("""{ "z": 1, "a": 2, "m": 3 }""");
        Assert.Equal(new[] { "z", "a", "m" }, root.Children.Select(c => c.Name).ToArray());
    }

    [Fact]
    public void Parse_InvalidJson_Throws()
    {
        Assert.ThrowsAny<JsonException>(() => _svc.Parse("{ not json "));
    }

    [Fact]
    public void Parse_ExpandsTopLevels_CollapsesDeeperContainers()
    {
        // root(0) -> a(1) -> b(2) -> c(3)
        var root = _svc.Parse("""{ "a": { "b": { "c": 1 } } }""");
        var a = root.Children[0];   // depth 1 object
        var b = a.Children[0];      // depth 2 object

        Assert.True(root.IsExpanded);   // depth 0 — open
        Assert.True(a.IsExpanded);      // depth 1 — open
        Assert.False(b.IsExpanded);     // depth 2 — collapsed by default
    }
}
