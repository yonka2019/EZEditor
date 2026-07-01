using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using EZEditor.Models;

namespace EZEditor.ViewModels;

public sealed partial class XmlNodeViewModel : ObservableObject
{
    public XmlNodeViewModel(XmlNodeKind kind, XObject xobj, XmlNodeViewModel? parent,
        string? name = null, string? value = null)
    {
        _kind = kind;
        XObject = xobj;
        Parent = parent;
        _name = name;     // set backing fields directly so construction never raises Changed
        _value = value;
        Attributes = new ObservableCollection<XmlNodeViewModel>();
        Children = new ObservableCollection<XmlNodeViewModel>();
        Children.CollectionChanged += OnChildrenChanged;
        Attributes.CollectionChanged += OnChildrenChanged;
    }

    public XObject XObject { get; }
    public XmlNodeViewModel? Parent { get; }
    public ObservableCollection<XmlNodeViewModel> Attributes { get; }
    public ObservableCollection<XmlNodeViewModel> Children { get; }

    [ObservableProperty] private XmlNodeKind _kind;
    [ObservableProperty] private string? _name;
    [ObservableProperty] private string? _value;
    [ObservableProperty] private bool _isExpanded = true;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isFilteredOut;

    public bool IsElement => Kind == XmlNodeKind.Element;
    public bool HasAttributes => Attributes.Count > 0;

    public string DisplayName => Kind switch
    {
        XmlNodeKind.Attribute => $"@{Name}",
        XmlNodeKind.Element => Name ?? "(element)",
        XmlNodeKind.Comment => "(comment)",
        XmlNodeKind.CData => "(cdata)",
        _ => "(text)",
    };

    public event EventHandler? Changed;

    public void RaiseChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
        Parent?.RaiseChanged();
    }

    partial void OnValueChanged(string? value)
    {
        switch (XObject)
        {
            case XText t: t.Value = value ?? string.Empty; break;   // XCData derives from XText
            case XComment c: c.Value = value ?? string.Empty; break;
            case XAttribute a: a.Value = value ?? string.Empty; break;
        }
        RaiseChanged();
    }

    partial void OnNameChanged(string? value)
    {
        if (XObject is XElement el && !string.IsNullOrWhiteSpace(value))
        {
            var local = value.Contains(':') ? value[(value.IndexOf(':') + 1)..] : value;
            el.Name = XName.Get(local, el.Name.NamespaceName);
        }
        RaiseChanged();
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e) => RaiseChanged();

    public XmlNodeViewModel AddChildElement(string name)
    {
        if (XObject is not XElement el)
            throw new InvalidOperationException("Only elements can have child elements.");
        var child = new XElement(XName.Get(name));
        el.Add(child);
        var vm = new XmlNodeViewModel(XmlNodeKind.Element, child, this, name: name);
        Children.Add(vm);
        return vm;
    }

    public void Delete()
    {
        switch (XObject)
        {
            case XAttribute a: a.Remove(); Parent?.Attributes.Remove(this); break;
            default:
                if (XObject is XNode n) n.Remove();
                Parent?.Children.Remove(this);
                break;
        }
        Parent?.RaiseChanged();
    }

    public void SetExpandedRecursive(bool expanded)
    {
        IsExpanded = expanded;
        foreach (var c in Children) c.SetExpandedRecursive(expanded);
    }

    private bool? _expandedBeforeFilter;

    public bool ApplyFilter(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            IsFilteredOut = false;
            if (_expandedBeforeFilter is bool prev) { IsExpanded = prev; _expandedBeforeFilter = null; }
            foreach (var c in Children) c.ApplyFilter(null);
            foreach (var a in Attributes) a.ApplyFilter(null);
            return true;
        }

        bool Match(string? s) => s is not null && s.Contains(text, StringComparison.OrdinalIgnoreCase);
        var selfMatch = Match(Name) || Match(Value)
            || Attributes.Any(a => Match(a.Name) || Match(a.Value));

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
            _expandedBeforeFilter ??= IsExpanded;
            IsExpanded = true;
        }
        return matched;
    }

    private static bool ShowSubtree(XmlNodeViewModel n)
    {
        n.IsFilteredOut = false;
        foreach (var c in n.Children) ShowSubtree(c);
        return true;
    }
}
