using System.Xml;
using EZEditor.Services;

namespace EZEditor.Tests;

public class XmlDocumentServiceTests
{
    private readonly XmlDocumentService _svc = new();

    [Fact]
    public void Parse_BuildsElementTree_WithAttributesAndChildren()
    {
        var r = _svc.Parse("<root id=\"1\"><city>Berlin</city></root>");
        Assert.Equal("root", r.Root.Name);
        Assert.Single(r.Root.Attributes);
        Assert.Equal("id", r.Root.Attributes[0].Name);
        Assert.Equal("1", r.Root.Attributes[0].Value);
        Assert.Single(r.Root.Children);
        Assert.Equal("city", r.Root.Children[0].Name);
    }

    [Fact]
    public void Parse_Malformed_Throws()
        => Assert.Throws<XmlException>(() => _svc.Parse("<root><unclosed></root>"));

    [Fact]
    public void Serialize_PreservesDeclarationAttributesCommentNamespace()
    {
        var src = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n"
                + "<note xmlns:x=\"urn:x\" id=\"7\">\r\n  <!-- hi -->\r\n  <x:body>text</x:body>\r\n</note>";
        var r = _svc.Parse(src);
        var outText = _svc.Serialize(r.Document);
        Assert.Contains("<?xml version=\"1.0\" encoding=\"utf-8\"?>", outText);
        Assert.Contains("id=\"7\"", outText);
        Assert.Contains("<!-- hi -->", outText);
        Assert.Contains("xmlns:x=\"urn:x\"", outText);
        Assert.Contains("<x:body>text</x:body>", outText);
    }

    [Fact]
    public void Serialize_IsIdempotent()
    {
        var src = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<r a=\"1\"><c>v</c></r>";
        var once = _svc.Serialize(_svc.Parse(src).Document);
        var twice = _svc.Serialize(_svc.Parse(once).Document);
        Assert.Equal(once, twice);
    }

    [Fact]
    public void Serialize_AppliesValueEdit()
    {
        var r = _svc.Parse("<r><c>old</c></r>");
        // c -> text child
        var textVm = r.Root.Children[0].Children[0];
        textVm.Value = "new";
        Assert.Contains("<c>new</c>", _svc.Serialize(r.Document));
    }
}
