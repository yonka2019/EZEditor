using System.IO;
using System.Text.Json;
using System.Xml;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EZEditor.Models;
using EZEditor.Services;

namespace EZEditor.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DocumentFactory _factory;
    private readonly IFileDialogService _dialogs;
    private readonly IUserPrompt _prompt;

    public MainViewModel(DocumentFactory factory, IFileDialogService dialogs, IUserPrompt prompt)
    {
        _factory = factory;
        _dialogs = dialogs;
        _prompt = prompt;
    }

    [ObservableProperty] private EditableDocument? _currentDocument;
    [ObservableProperty] private string? _currentPath;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string? _filterText;

    // JSON-only commands operate on this; null when the open document isn't JSON.
    public JsonNodeViewModel? JsonRoot => (CurrentDocument as JsonDocument)?.Root;

    public string StatusText =>
        (CurrentPath ?? "No file")
        + (CurrentDocument is not null ? $"  [{CurrentDocument.Format.ToString().ToUpperInvariant()}]" : string.Empty)
        + (IsDirty ? "  ●" : string.Empty);

    partial void OnCurrentDocumentChanged(EditableDocument? oldValue, EditableDocument? newValue)
    {
        if (oldValue is not null) oldValue.Changed -= OnDocChanged;
        if (newValue is not null) newValue.Changed += OnDocChanged;
        OnPropertyChanged(nameof(JsonRoot));
        OnPropertyChanged(nameof(StatusText));
    }

    partial void OnCurrentPathChanged(string? value) => OnPropertyChanged(nameof(StatusText));
    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(StatusText));
    partial void OnFilterTextChanged(string? value) => CurrentDocument?.ApplyFilter(value);

    private void OnDocChanged(object? sender, EventArgs e) => IsDirty = true;

    public void OpenPath(string path)
    {
        var doc = _factory.LoadAuto(path);
        FilterText = null;
        CurrentDocument = doc;
        CurrentPath = path;
        IsDirty = false;
    }

    public bool ConfirmDiscardIfDirty()
    {
        if (!IsDirty) return true;
        return _prompt.ConfirmDiscard() switch { PromptResult.Yes => true, _ => false };
    }

    [RelayCommand]
    private void Open()
    {
        if (!ConfirmDiscardIfDirty()) return;
        var path = _dialogs.OpenFile();
        if (path is null) return;
        TryLoad(path, "open");
    }

    private void TryLoad(string path, string verb)
    {
        try { OpenPath(path); }
        catch (JsonException ex) { _prompt.Error($"Could not parse JSON:\n{ex.Message}"); }
        catch (XmlException ex) { _prompt.Error($"Could not parse XML:\n{ex.Message}"); }
        catch (FormatException ex) { _prompt.Error($"Could not parse file:\n{ex.Message}"); }
        catch (NotSupportedException ex) { _prompt.Error($"Could not parse file:\n{ex.Message}"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { _prompt.Error($"Could not {verb} file:\n{ex.Message}"); }
    }

    [RelayCommand]
    private void Save()
    {
        if (CurrentDocument is null) return;
        if (CurrentPath is null) { SaveAs(); return; }
        Persist(CurrentPath);
    }

    [RelayCommand]
    private void SaveAs()
    {
        if (CurrentDocument is null) return;
        var path = _dialogs.SaveFile(CurrentPath);
        if (path is null) return;
        Persist(path);
    }

    private void Persist(string path)
    {
        try
        {
            CurrentDocument!.Save(path);
            CurrentPath = path;
            IsDirty = false;
        }
        catch (Exception ex) when (ex is IOException or JsonException or XmlException
                                     or UnauthorizedAccessException or InvalidOperationException)
        { _prompt.Error($"Could not save file:\n{ex.Message}"); }
    }

    [RelayCommand]
    private void Reload()
    {
        if (CurrentPath is null) return;
        if (!ConfirmDiscardIfDirty()) return;
        TryLoad(CurrentPath, "reload");
    }

    // ---- JSON-only edit commands (no-op unless the JSON document is active) ----
    [RelayCommand]
    private void AddChild(JsonNodeViewModel? node)
    {
        if (node is { IsContainer: true }) node.AddChild(JsonNodeKind.String);
    }

    [RelayCommand]
    private void DeleteNode(JsonNodeViewModel? node)
    {
        if (node is not null && node.Parent is not null) node.Delete();
    }

    [RelayCommand] private void ExpandAll() => JsonRoot?.SetExpandedRecursive(true);

    [RelayCommand]
    private void CollapseAll()
    {
        if (JsonRoot is null) return;
        JsonRoot.SetExpandedRecursive(false);
        JsonRoot.IsExpanded = true;
    }

    [RelayCommand] private void MakeString(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.String);
    [RelayCommand] private void MakeNumber(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.Number);
    [RelayCommand] private void MakeBoolean(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.Boolean);
    [RelayCommand] private void MakeNull(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.Null);
    [RelayCommand] private void MakeObject(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.Object);
    [RelayCommand] private void MakeArray(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.Array);
}
