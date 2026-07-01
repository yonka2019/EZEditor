using System.Xml.Linq;
using EZEditor.Models;
using EZEditor.ViewModels;

namespace EZEditor.Tests;

public class XmlNodeViewModelTests
{
    [Fact]
    public void EditingValue_WritesThroughToXObject()
    {
        var el = new XElement("a", new XText("old"));
        var textNode = el.Nodes().OfType<XText>().First();
        var vm = new XmlNodeViewModel(XmlNodeKind.Text, textNode, null, value: "old");
        vm.Value = "new";
        Assert.Equal("new", textNode.Value);
    }

    [Fact]
    public void EditingName_RenamesElement_PreservingNamespace()
    {
        XNamespace ns = "http://example.com";
        var el = new XElement(ns + "old");
        var vm = new XmlNodeViewModel(XmlNodeKind.Element, el, null, name: "old");
        vm.Name = "renamed";
        Assert.Equal(ns + "renamed", el.Name);
    }

    [Fact]
    public void EditingAttributeValue_WritesThrough()
    {
        var attr = new XAttribute("id", "1");
        _ = new XElement("a", attr);
        var vm = new XmlNodeViewModel(XmlNodeKind.Attribute, attr, null, name: "id", value: "1");
        vm.Value = "2";
        Assert.Equal("2", attr.Value);
    }

    [Fact]
    public void Changed_BubblesToParent()
    {
        var parentEl = new XElement("p", new XElement("c", new XText("v")));
        var pvm = new XmlNodeViewModel(XmlNodeKind.Element, parentEl, null, name: "p");
        var childEl = parentEl.Elements().First();
        var cvm = new XmlNodeViewModel(XmlNodeKind.Element, childEl, pvm, name: "c");
        pvm.Children.Add(cvm);

        var fired = 0;
        pvm.Changed += (_, _) => fired++;
        cvm.Name = "c2";
        Assert.True(fired >= 1);
    }

    [Fact]
    public void DisplayName_PrefixesAttributesWithAt()
    {
        var attr = new XAttribute("id", "1");
        var vm = new XmlNodeViewModel(XmlNodeKind.Attribute, attr, null, name: "id", value: "1");
        Assert.Equal("@id", vm.DisplayName);
    }

    [Fact]
    public void AddingAttribute_BubblesChanged()
    {
        var el = new System.Xml.Linq.XElement("e");
        var vm = new XmlNodeViewModel(XmlNodeKind.Element, el, null, name: "e");
        var fired = 0;
        vm.Changed += (_, _) => fired++;

        var attr = new System.Xml.Linq.XAttribute("id", "1");
        el.Add(attr);
        vm.Attributes.Add(new XmlNodeViewModel(XmlNodeKind.Attribute, attr, vm, name: "id", value: "1"));

        Assert.True(fired >= 1);
    }

    [Fact]
    public void ApplyFilter_MatchesNameValueAndAttributes()
    {
        var el = new XElement("root", new XElement("city", new XText("Berlin")));
        var rvm = new XmlNodeViewModel(XmlNodeKind.Element, el, null, name: "root");
        var cityEl = el.Elements().First();
        var cvm = new XmlNodeViewModel(XmlNodeKind.Element, cityEl, rvm, name: "city");
        var txt = new XmlNodeViewModel(XmlNodeKind.Text, cityEl.Nodes().OfType<XText>().First(), cvm, value: "Berlin");
        cvm.Children.Add(txt);
        rvm.Children.Add(cvm);

        rvm.ApplyFilter("berlin");
        Assert.False(cvm.IsFilteredOut);  // matched via descendant text
        Assert.False(txt.IsFilteredOut);
    }
}
