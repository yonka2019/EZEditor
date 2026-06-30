using EZEditor.Models;
using EZEditor.Services;
using EZEditor.ViewModels;

namespace EZEditor.Tests;

public class JsonSerializeTests
{
    private readonly JsonDocumentService _svc = new();

    [Fact]
    public void Serialize_UsesTwoSpaceIndent()
    {
        var root = new JsonNodeViewModel(JsonNodeKind.Object);
        root.Children.Add(new JsonNodeViewModel(JsonNodeKind.String, "k", "v", root));
        var json = _svc.Serialize(root);
        Assert.Contains("\n  \"k\": \"v\"", json.Replace("\r\n", "\n"));
    }

    [Fact]
    public void RoundTrip_PreservesData_Order_And_NumberText()
    {
        const string input = """{"z":"hi","n":30,"big":12345678901234567890,"b":false,"arr":[1,null,"x"]}""";
        var root = _svc.Parse(input);
        var output = _svc.Serialize(root);

        // Re-parse output and compare structure/values.
        var reparsed = _svc.Parse(output);
        Assert.Equal(new[] { "z", "n", "big", "b", "arr" }, reparsed.Children.Select(c => c.Name).ToArray());
        Assert.Equal("12345678901234567890", reparsed.Children[2].Value); // big number text preserved
        Assert.Equal(JsonNodeKind.Null, reparsed.Children[4].Children[1].Kind);
    }
}
