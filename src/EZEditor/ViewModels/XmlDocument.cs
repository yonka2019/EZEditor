using System.Collections.ObjectModel;
using System.Xml.Linq;
using EZEditor.Services;

namespace EZEditor.ViewModels;

public sealed class XmlDocument : EditableDocument
{
    private readonly XmlDocumentService _svc;
    private readonly XDocument _doc;

    public XmlDocument(XmlParseResult parsed, XmlDocumentService svc)
    {
        _svc = svc;
        _doc = parsed.Document;
        Roots = new ObservableCollection<XmlNodeViewModel> { parsed.Root };
        parsed.Root.Changed += (_, _) => OnChanged();
    }

    public ObservableCollection<XmlNodeViewModel> Roots { get; }
    public XmlNodeViewModel Root => Roots[0];

    public override DocumentFormat Format => DocumentFormat.Xml;
    public override string Serialize() => _svc.Serialize(_doc);
    public override void ApplyFilter(string? text) => Root.ApplyFilter(text);
}
