using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using JsonEditor.Models;

namespace JsonEditor.ViewModels;

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
                Value = double.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _)
                    ? Value
                    : "0";
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

    // Recursively applies a case-insensitive key filter. Returns true if this node
    // or any descendant matches; clears the filter when text is empty.
    public bool ApplyFilter(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            IsFilteredOut = false;
            foreach (var c in Children) c.ApplyFilter(null);
            return true;
        }

        var selfMatch = Name is not null &&
                        Name.Contains(text, StringComparison.OrdinalIgnoreCase);

        var childMatch = false;
        foreach (var c in Children)
        {
            var m = selfMatch ? ShowSubtree(c) : c.ApplyFilter(text);
            childMatch |= m;
        }

        var matched = selfMatch || childMatch;
        IsFilteredOut = !matched;
        if (matched) IsExpanded = true;
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
