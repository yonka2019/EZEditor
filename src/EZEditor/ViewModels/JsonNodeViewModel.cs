using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using EZEditor.Models;

namespace EZEditor.ViewModels;

public partial class JsonNodeViewModel : ObservableObject
{
    public JsonNodeViewModel(
        JsonNodeKind kind,
        string? name = null,
        string? value = null,
        JsonNodeViewModel? parent = null)
    {
        _kind = kind;
        _name = name;
        _value = value;
        Parent = parent;
        Children = new ObservableCollection<JsonNodeViewModel>();
        Children.CollectionChanged += OnChildrenChanged;
    }

    public JsonNodeViewModel? Parent { get; }

    public ObservableCollection<JsonNodeViewModel> Children { get; }

    [ObservableProperty] private string? _name;
    [ObservableProperty] private JsonNodeKind _kind;
    [ObservableProperty] private string? _value;
    [ObservableProperty] private bool _isExpanded = true;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isFilteredOut;

    public bool IsContainer => Kind is JsonNodeKind.Object or JsonNodeKind.Array;

    public bool IsObjectMember => Parent is { Kind: JsonNodeKind.Object };

    public string DisplayName
    {
        get
        {
            if (Name is not null) return Name;
            if (Parent is { Kind: JsonNodeKind.Array })
                return $"[{Parent.Children.IndexOf(this)}]";
            return "(root)";
        }
    }

    public event EventHandler? Changed;

    public void RaiseChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
        Parent?.RaiseChanged();
    }

    partial void OnValueChanged(string? value) => RaiseChanged();
    partial void OnNameChanged(string? value) => RaiseChanged();

    partial void OnKindChanged(JsonNodeKind value)
    {
        OnPropertyChanged(nameof(IsContainer));
        RaiseChanged();
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Array element indices shift when the collection changes.
        foreach (var c in Children)
            c.OnPropertyChanged(nameof(DisplayName));
        RaiseChanged();
    }

    public JsonNodeViewModel AddChild(JsonNodeKind kind)
    {
        if (!IsContainer)
            throw new InvalidOperationException("Cannot add a child to a non-container node.");

        string? childName = null;
        if (Kind == JsonNodeKind.Object)
        {
            childName = "newKey";
            var i = 2;
            while (Children.Any(c => c.Name == childName))
                childName = $"newKey{i++}";
        }

        var value = kind switch
        {
            JsonNodeKind.String => string.Empty,
            JsonNodeKind.Number => "0",
            JsonNodeKind.Boolean => "false",
            _ => (string?)null
        };

        var child = new JsonNodeViewModel(kind, childName, value, this);
        Children.Add(child);
        return child;
    }

    public void Delete() => Parent?.Children.Remove(this);

    public void Rename(string newName) => Name = newName;

    public void ChangeKind(JsonNodeKind newKind)
    {
        if (newKind == Kind) return;

        switch (newKind)
        {
            case JsonNodeKind.String:
                Value = Value ?? string.Empty;
                Children.Clear();
                break;
            case JsonNodeKind.Number:
                // Use the same predicate as the serializer so a value that survives
                // the type change is guaranteed to survive Save (no silent -> 0).
                Value = Services.JsonDocumentService.IsValidNumber(Value) ? Value!.Trim() : "0";
                Children.Clear();
                break;
            case JsonNodeKind.Boolean:
                Value = string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
                Children.Clear();
                break;
            case JsonNodeKind.Null:
                Value = null;
                Children.Clear();
                break;
            case JsonNodeKind.Object:
            case JsonNodeKind.Array:
                Value = null;
                Children.Clear();
                break;
        }

        Kind = newKind;
    }

    // Expand or collapse this node and its entire subtree (does not mark dirty).
    public void SetExpandedRecursive(bool expanded)
    {
        IsExpanded = expanded;
        foreach (var c in Children) c.SetExpandedRecursive(expanded);
    }

    // Remembers the user's expansion state so a filter can force-expand to reveal
    // matches and then restore the original state when the filter is cleared.
    private bool? _expandedBeforeFilter;

    // Recursively applies a case-insensitive filter over keys AND values. Returns
    // true if this node or any descendant matches; clears the filter when text is empty.
    public bool ApplyFilter(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            IsFilteredOut = false;
            if (_expandedBeforeFilter is bool previous)
            {
                IsExpanded = previous;
                _expandedBeforeFilter = null;
            }
            foreach (var c in Children) c.ApplyFilter(null);
            return true;
        }

        // A null node has no Value text; treat it as the literal "null" so a "null"
        // search matches both real-null nodes and string values containing "null".
        var valueText = Kind == JsonNodeKind.Null ? "null" : Value;
        var selfMatch =
            (Name is not null && Name.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
            (valueText is not null && valueText.Contains(text, StringComparison.OrdinalIgnoreCase));

        var childMatch = false;
        foreach (var c in Children)
        {
            var m = selfMatch ? ShowSubtree(c) : c.ApplyFilter(text);
            childMatch |= m;
        }

        var matched = selfMatch || childMatch;
        IsFilteredOut = !matched;
        if (matched && Children.Count > 0)
        {
            _expandedBeforeFilter ??= IsExpanded; // snapshot once, before forcing open
            IsExpanded = true;
        }
        return matched;
    }

    private static bool ShowSubtree(JsonNodeViewModel n)
    {
        n.IsFilteredOut = false;
        foreach (var c in n.Children) ShowSubtree(c);
        return true;
    }

    // Expose protected OnPropertyChanged for sibling index refresh.
    internal new void OnPropertyChanged(string propertyName) => base.OnPropertyChanged(propertyName);
}
