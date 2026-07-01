using System.Text;
using System.Xml;
using System.Xml.Linq;
using EZEditor.Models;
using EZEditor.ViewModels;

namespace EZEditor.Services;

public sealed class XmlDocumentService
{
    private const int AutoExpandDepth = 2;

    public XmlParseResult Parse(string text)
    {
        // PreserveWhitespace keeps insignificant whitespace as XText nodes so output is faithful.
        var doc = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
        if (doc.Root is null) throw new XmlException("XML has no root element.");
        return new XmlParseResult(doc, BuildElement(doc.Root, null, 0));
    }

    private static XmlNodeViewModel BuildElement(XElement el, XmlNodeViewModel? parent, int depth)
    {
        var vm = new XmlNodeViewModel(XmlNodeKind.Element, el, parent, name: PrefixedName(el))
        {
            IsExpanded = depth < AutoExpandDepth
        };

        foreach (var a in el.Attributes())
        {
            if (a.IsNamespaceDeclaration) continue; // shown via element ns; preserved in the XDocument
            vm.Attributes.Add(new XmlNodeViewModel(XmlNodeKind.Attribute, a, vm,
                name: PrefixedAttrName(a), value: a.Value));
        }

        foreach (var node in el.Nodes())
        {
            switch (node)
            {
                case XElement child:
                    vm.Children.Add(BuildElement(child, vm, depth + 1));
                    break;
                case XCData cd:
                    vm.Children.Add(new XmlNodeViewModel(XmlNodeKind.CData, cd, vm, value: cd.Value));
                    break;
                case XComment cm:
                    vm.Children.Add(new XmlNodeViewModel(XmlNodeKind.Comment, cm, vm, value: cm.Value));
                    break;
                case XText tx when !string.IsNullOrWhiteSpace(tx.Value):
                    vm.Children.Add(new XmlNodeViewModel(XmlNodeKind.Text, tx, vm, value: tx.Value));
                    break;
            }
        }
        return vm;
    }

    private static string PrefixedName(XElement el)
    {
        var prefix = el.GetPrefixOfNamespace(el.Name.Namespace);
        return string.IsNullOrEmpty(prefix) ? el.Name.LocalName : $"{prefix}:{el.Name.LocalName}";
    }

    private static string PrefixedAttrName(XAttribute a)
    {
        if (a.Name.Namespace == XNamespace.None) return a.Name.LocalName;
        var prefix = a.Parent?.GetPrefixOfNamespace(a.Name.Namespace);
        return string.IsNullOrEmpty(prefix) ? a.Name.LocalName : $"{prefix}:{a.Name.LocalName}";
    }

    public string Serialize(XDocument doc)
    {
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,        // declaration emitted manually to preserve its exact text
            Indent = false,
            NewLineHandling = NewLineHandling.None,
        };
        var sb = new StringBuilder();
        using (var xw = XmlWriter.Create(sb, settings)) doc.Save(xw);
        var body = sb.ToString();
        return doc.Declaration is null
            ? body
            : doc.Declaration + "\r\n" + body.TrimStart('\r', '\n');
    }
}
