using JsonEditor.Models;
using JsonEditor.Services;
using JsonEditor.ViewModels;

namespace JsonEditor.Tests;

// Broad battery of structural round-trip checks across tricky JSON shapes.
public class RoundTripEdgeCaseTests
{
    private readonly JsonDocumentService _svc = new();

    private static void AssertTreesEqual(JsonNodeViewModel a, JsonNodeViewModel b)
    {
        Assert.Equal(a.Kind, b.Kind);
        Assert.Equal(a.Name, b.Name);
        Assert.Equal(a.Value, b.Value);
        Assert.Equal(a.Children.Count, b.Children.Count);
        for (var i = 0; i < a.Children.Count; i++)
            AssertTreesEqual(a.Children[i], b.Children[i]);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("-0.5")]
    [InlineData("1e10")]
    [InlineData("\"hello\"")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("null")]
    [InlineData("{\"o\":{},\"a\":[]}")]
    [InlineData("[1,[2,[3,[4]]]]")]
    [InlineData("{\"a\":[{\"b\":[{\"c\":1}]}]}")]
    public void RoundTrip_IsStructurallyStable(string json)
    {
        var first = _svc.Parse(json);
        var second = _svc.Parse(_svc.Serialize(first));
        AssertTreesEqual(first, second);

        // Serializing twice must be idempotent.
        Assert.Equal(_svc.Serialize(first), _svc.Serialize(second));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0")]
    [InlineData("123")]
    [InlineData("-17")]
    [InlineData("3.14159")]
    [InlineData("1e10")]
    [InlineData("1.5E-3")]
    [InlineData("123456789012345678901234567890")]
    public void RoundTrip_PreservesExactNumberText(string number)
    {
        var root = _svc.Parse($"{{\"n\":{number}}}");
        Assert.Equal(JsonNodeKind.Number, root.Children[0].Kind);
        Assert.Equal(number, root.Children[0].Value);

        var re = _svc.Parse(_svc.Serialize(root));
        Assert.Equal(number, re.Children[0].Value);
    }

    [Fact]
    public void RoundTrip_PreservesNumbersBeyondDoubleRangeAndPrecision()
    {
        // These are valid JSON numbers but overflow/exceed double — must NOT be coerced to 0.
        var root = _svc.Parse("{\"big\":1e500,\"tiny\":1e-500,\"huge\":123456789012345678901234567890}");
        var re = _svc.Parse(_svc.Serialize(root));
        Assert.Equal("1e500", re.Children[0].Value);
        Assert.Equal("1e-500", re.Children[1].Value);
        Assert.Equal("123456789012345678901234567890", re.Children[2].Value);
    }

    [Fact]
    public void RoundTrip_PreservesUnicodeAndSpecialCharsSemantically()
    {
        // café / 😀 (surrogate pair) / <b>&  — written with escapes to avoid source-encoding pitfalls.
        var json = "{\"city\":\"café\",\"emoji\":\"😀\",\"html\":\"<b>&\"}";
        var re = _svc.Parse(_svc.Serialize(_svc.Parse(json)));
        Assert.Equal("café", re.Children[0].Value);
        Assert.Equal("😀", re.Children[1].Value);
        Assert.Equal("<b>&", re.Children[2].Value);
    }

    [Fact]
    public void RoundTrip_PreservesEscapeSequencesInStrings()
    {
        var json = "{\"s\":\"line1\\nline2\\ttab\\\"quote\\\"\\\\back\"}";
        var re = _svc.Parse(_svc.Serialize(_svc.Parse(json)));
        Assert.Equal("line1\nline2\ttab\"quote\"\\back", re.Children[0].Value);
    }

    [Fact]
    public void RoundTrip_PreservesSpecialCharactersInKeys()
    {
        var json = "{\"a b\":1,\"a\\\"b\":2,\"\":3}";
        var root = _svc.Parse(json);
        Assert.Equal(new[] { "a b", "a\"b", "" }, root.Children.Select(c => c.Name).ToArray());

        var re = _svc.Parse(_svc.Serialize(root));
        Assert.Equal(new[] { "a b", "a\"b", "" }, re.Children.Select(c => c.Name).ToArray());
    }

    [Fact]
    public void PrimitiveRoot_IsEditableAndSerializes()
    {
        var root = _svc.Parse("42");
        Assert.Null(root.Name);
        Assert.Equal(JsonNodeKind.Number, root.Kind);
        root.Value = "99";
        Assert.Equal("99", _svc.Serialize(root));
    }

    [Fact]
    public void EditThenSerialize_ReflectsChanges()
    {
        var root = _svc.Parse("{\"a\":1}");
        root.AddChild(JsonNodeKind.Boolean);          // -> "newKey": false
        root.Children[0].Value = "5";                 // a = 5
        var re = _svc.Parse(_svc.Serialize(root));
        Assert.Equal("5", re.Children[0].Value);
        Assert.Equal(JsonNodeKind.Boolean, re.Children[1].Kind);
        Assert.Equal("newKey", re.Children[1].Name);
    }

    [Fact]
    public void ChangeKind_ThenSerialize_ProducesValidJsonForEveryTarget()
    {
        foreach (var target in new[]
                 {
                     JsonNodeKind.String, JsonNodeKind.Number, JsonNodeKind.Boolean,
                     JsonNodeKind.Null, JsonNodeKind.Object, JsonNodeKind.Array
                 })
        {
            var root = _svc.Parse("{\"x\":\"hello\"}");
            root.Children[0].ChangeKind(target);
            var json = _svc.Serialize(root);                 // must not throw
            var re = _svc.Parse(json);                        // must be valid JSON
            Assert.Equal(target, re.Children[0].Kind);
        }
    }

    [Fact]
    public void Serialize_NeverThrows_OnInvalidNumberText()
    {
        var root = _svc.Parse("{\"n\":1}");
        root.Children[0].Value = "not-a-number";          // simulate bad edit landing in Value
        var json = _svc.Serialize(root);                  // guarded -> "0"
        var re = _svc.Parse(json);
        Assert.Equal("0", re.Children[0].Value);
    }
}
