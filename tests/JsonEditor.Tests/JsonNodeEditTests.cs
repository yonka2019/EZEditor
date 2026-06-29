using JsonEditor.Models;
using JsonEditor.ViewModels;

namespace JsonEditor.Tests;

public class JsonNodeEditTests
{
    [Fact]
    public void AddChild_ToObject_AssignsUniqueKeys()
    {
        var obj = new JsonNodeViewModel(JsonNodeKind.Object);
        var a = obj.AddChild(JsonNodeKind.String);
        var b = obj.AddChild(JsonNodeKind.String);
        Assert.Equal("newKey", a.Name);
        Assert.Equal("newKey2", b.Name);
        Assert.Equal(2, obj.Children.Count);
    }

    [Fact]
    public void AddChild_ToArray_LeavesNameNull()
    {
        var arr = new JsonNodeViewModel(JsonNodeKind.Array);
        var item = arr.AddChild(JsonNodeKind.Number);
        Assert.Null(item.Name);
        Assert.Equal(JsonNodeKind.Number, item.Kind);
    }

    [Fact]
    public void Delete_RemovesFromParent()
    {
        var obj = new JsonNodeViewModel(JsonNodeKind.Object);
        var a = obj.AddChild(JsonNodeKind.String);
        a.Delete();
        Assert.Empty(obj.Children);
    }

    [Fact]
    public void ChangeKind_StringToNumber_KeepsParseableValue()
    {
        var n = new JsonNodeViewModel(JsonNodeKind.String, value: "42");
        n.ChangeKind(JsonNodeKind.Number);
        Assert.Equal(JsonNodeKind.Number, n.Kind);
        Assert.Equal("42", n.Value);
    }

    [Fact]
    public void ChangeKind_StringToNumber_NonNumericResetsToZero()
    {
        var n = new JsonNodeViewModel(JsonNodeKind.String, value: "hello");
        n.ChangeKind(JsonNodeKind.Number);
        Assert.Equal("0", n.Value);
    }

    [Theory]
    [InlineData("42", "42")]
    [InlineData("  7  ", "7")]      // trimmed
    [InlineData("1,000", "0")]      // serializer would destroy it -> rejected here too
    [InlineData("NaN", "0")]
    [InlineData("Infinity", "0")]
    [InlineData("(5)", "0")]
    public void ChangeKind_Number_OnlyKeepsSerializerValidValues(string input, string expected)
    {
        var n = new JsonNodeViewModel(JsonNodeKind.String, value: input);
        n.ChangeKind(JsonNodeKind.Number);
        Assert.Equal(expected, n.Value);
    }

    [Fact]
    public void ChangeKind_ToContainer_ClearsValueAndChildren()
    {
        var n = new JsonNodeViewModel(JsonNodeKind.String, value: "x");
        n.ChangeKind(JsonNodeKind.Object);
        Assert.Equal(JsonNodeKind.Object, n.Kind);
        Assert.Null(n.Value);
        Assert.Empty(n.Children);
    }

    [Fact]
    public void ChangeKind_ToBoolean_TruthyMapping()
    {
        var n = new JsonNodeViewModel(JsonNodeKind.String, value: "true");
        n.ChangeKind(JsonNodeKind.Boolean);
        Assert.Equal("true", n.Value);

        var m = new JsonNodeViewModel(JsonNodeKind.String, value: "nope");
        m.ChangeKind(JsonNodeKind.Boolean);
        Assert.Equal("false", m.Value);
    }
}
