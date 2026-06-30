using System.Collections.ObjectModel;
using EZEditor.Services;

namespace EZEditor.ViewModels;

public sealed class JsonDocument : EditableDocument
{
    private readonly JsonDocumentService _svc;

    public JsonDocument(JsonNodeViewModel root, JsonDocumentService svc)
    {
        _svc = svc;
        Roots = new ObservableCollection<JsonNodeViewModel> { root };
        root.Changed += (_, _) => OnChanged();
    }

    // Single-element collection so the JSON TreeView can bind ItemsSource directly.
    public ObservableCollection<JsonNodeViewModel> Roots { get; }
    public JsonNodeViewModel Root => Roots[0];

    public override DocumentFormat Format => DocumentFormat.Json;
    public override string Serialize() => _svc.Serialize(Root);
    public override void Save(string path) => _svc.Save(Root, path);
    public override void ApplyFilter(string? text) => Root.ApplyFilter(text);
}
