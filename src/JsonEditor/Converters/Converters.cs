using System.Globalization;
using System.Windows;
using System.Windows.Data;
using JsonEditor.Models;

namespace JsonEditor.Converters;

// Visible when the bound JsonNodeKind equals the kind named in ConverterParameter.
public class KindToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? parameter, CultureInfo c)
        => value is JsonNodeKind k && parameter is string s && k.ToString() == s
            ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

// Two-way bridge between a "true"/"false" string Value and a bool (for ToggleSwitch.IsChecked).
public class StringBoolConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
        => string.Equals(value as string, "true", StringComparison.OrdinalIgnoreCase);
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c)
        => value is true ? "true" : "false";
}

// Visible when the bound bool is TRUE (editable key box for object members).
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
        => value is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

// Visible when the bound bool is FALSE (index/root label, and showing non-filtered nodes).
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
        => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

// Maps a JsonNodeKind to the themed syntax-highlight brush (single source of truth in Theme.xaml).
public class KindToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        var key = value switch
        {
            JsonNodeKind.String => "StringColor",
            JsonNodeKind.Number => "NumberColor",
            JsonNodeKind.Boolean => "BoolColor",
            JsonNodeKind.Null => "NullColor",
            JsonNodeKind.Object => "StructColor",
            JsonNodeKind.Array => "StructColor",
            _ => "TextPrimary"
        };
        return Application.Current?.TryFindResource(key)
               ?? Application.Current?.TryFindResource("TextPrimary")
               ?? System.Windows.Media.Brushes.White;
    }

    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

// Maps a JsonNodeKind to a short type tag shown in the type chip.
public class KindToTagConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
        => value switch
        {
            JsonNodeKind.String => "str",
            JsonNodeKind.Number => "num",
            JsonNodeKind.Boolean => "bool",
            JsonNodeKind.Null => "null",
            JsonNodeKind.Object => "{ }",
            JsonNodeKind.Array => "[ ]",
            _ => ""
        };

    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}
