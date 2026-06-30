// src/EZEditor/ViewModels/EditableDocument.cs
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EZEditor.ViewModels;

public enum DocumentFormat { Json, Xml, Csv }

// Format-agnostic open document. The shell (MainViewModel) holds exactly one and
// never reaches into format-specific internals except through the typed subclasses.
public abstract class EditableDocument : ObservableObject
{
    public abstract DocumentFormat Format { get; }

    // Raised on any edit; the shell bridges this to its IsDirty flag.
    public event EventHandler? Changed;
    protected void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public abstract string Serialize();

    public virtual void Save(string path) =>
        File.WriteAllText(path, Serialize(), new UTF8Encoding(false));

    public virtual void ApplyFilter(string? text) { }
}
