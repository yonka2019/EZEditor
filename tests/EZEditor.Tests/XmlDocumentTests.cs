using EZEditor.Services;
using EZEditor.ViewModels;

namespace EZEditor.Tests;

public class XmlDocumentTests
{
    private static XmlDocument Make(string xml)
    {
        var svc = new XmlDocumentService();
        return new XmlDocument(svc.Parse(xml), svc);
    }

    [Fact]
    public void Format_IsXml_AndRootExposed()
    {
        var doc = Make("<r><c>v</c></r>");
        Assert.Equal(DocumentFormat.Xml, doc.Format);
        Assert.Equal("r", doc.Root.Name);
        Assert.Single(doc.Roots);
    }

    [Fact]
    public void EditingValue_RaisesChanged_AndSerializes()
    {
        var doc = Make("<r><c>old</c></r>");
        var fired = 0;
        doc.Changed += (_, _) => fired++;
        doc.Root.Children[0].Children[0].Value = "new"; // c -> text
        Assert.True(fired >= 1);
        Assert.Contains("<c>new</c>", doc.Serialize());
    }

    [Fact]
    public void ApplyFilter_DelegatesToRoot()
    {
        var doc = Make("<r><a>x</a><b>y</b></r>");
        doc.ApplyFilter("a");
        Assert.False(doc.Root.Children.First(c => c.Name == "a").IsFilteredOut);
        Assert.True(doc.Root.Children.First(c => c.Name == "b").IsFilteredOut);
    }
}
