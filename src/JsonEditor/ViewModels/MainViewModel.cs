using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JsonEditor.Models;
using JsonEditor.Services;

namespace JsonEditor.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly JsonDocumentService _svc;
    private readonly IFileDialogService _dialogs;
    private readonly IUserPrompt _prompt;

    public MainViewModel(JsonDocumentService svc, IFileDialogService dialogs, IUserPrompt prompt)
    {
        _svc = svc;
        _dialogs = dialogs;
        _prompt = prompt;
        Roots = new ObservableCollection<JsonNodeViewModel>();
    }

    public ObservableCollection<JsonNodeViewModel> Roots { get; }

    [ObservableProperty] private string? _currentPath;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string? _filterText;

    public JsonNodeViewModel? Root => Roots.Count > 0 ? Roots[0] : null;

    public string StatusText =>
        (CurrentPath ?? "No file") + (IsDirty ? "  ●" : string.Empty);

    partial void OnCurrentPathChanged(string? value) => OnPropertyChanged(nameof(StatusText));
    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(StatusText));
    partial void OnFilterTextChanged(string? value) => Root?.ApplyFilter(value);

    public void OpenPath(string path)
    {
        var root = _svc.Load(path);
        SetRoot(root);
        CurrentPath = path;
        IsDirty = false;
    }

    private void SetRoot(JsonNodeViewModel root)
    {
        if (Root is not null) Root.Changed -= OnTreeChanged;
        Roots.Clear();
        Roots.Add(root);
        root.Changed += OnTreeChanged;
        OnPropertyChanged(nameof(Root));
    }

    private void OnTreeChanged(object? sender, EventArgs e) => IsDirty = true;

    // Returns true if it is safe to proceed (no changes, or user chose to discard).
    // Public so the window's Closing handler can guard against losing unsaved edits.
    public bool ConfirmDiscardIfDirty()
    {
        if (!IsDirty) return true;
        return _prompt.ConfirmDiscard() switch
        {
            PromptResult.Yes => true,
            _ => false
        };
    }

    [RelayCommand]
    private void Open()
    {
        if (!ConfirmDiscardIfDirty()) return;
        var path = _dialogs.OpenFile();
        if (path is null) return;
        try
        {
            OpenPath(path);
        }
        catch (JsonException ex)
        {
            _prompt.Error($"Could not parse JSON:\n{ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _prompt.Error($"Could not open file:\n{ex.Message}");
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (Root is null) return;
        if (CurrentPath is null) { SaveAs(); return; }
        Persist(CurrentPath);
    }

    [RelayCommand]
    private void SaveAs()
    {
        if (Root is null) return;
        var path = _dialogs.SaveFile(CurrentPath);
        if (path is null) return;
        Persist(path);
    }

    private void Persist(string path)
    {
        try
        {
            _svc.Save(Root!, path);
            CurrentPath = path;
            IsDirty = false;
        }
        catch (Exception ex) when (ex is IOException or JsonException
                                     or UnauthorizedAccessException or InvalidOperationException)
        {
            _prompt.Error($"Could not save file:\n{ex.Message}");
        }
    }

    [RelayCommand]
    private void Reload()
    {
        if (CurrentPath is null) return;
        if (!ConfirmDiscardIfDirty()) return;
        try
        {
            OpenPath(CurrentPath);
        }
        catch (JsonException ex)
        {
            _prompt.Error($"Could not parse JSON:\n{ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _prompt.Error($"Could not reload file:\n{ex.Message}");
        }
    }

    [RelayCommand]
    private void AddChild(JsonNodeViewModel? node)
    {
        if (node is { IsContainer: true })
            node.AddChild(JsonNodeKind.String);
    }

    [RelayCommand]
    private void DeleteNode(JsonNodeViewModel? node)
    {
        if (node is not null && node.Parent is not null)
            node.Delete();
    }

    [RelayCommand] private void MakeString(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.String);
    [RelayCommand] private void MakeNumber(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.Number);
    [RelayCommand] private void MakeBoolean(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.Boolean);
    [RelayCommand] private void MakeNull(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.Null);
    [RelayCommand] private void MakeObject(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.Object);
    [RelayCommand] private void MakeArray(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.Array);
}
