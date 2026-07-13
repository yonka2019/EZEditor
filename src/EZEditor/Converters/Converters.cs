using System.Globalization;
using System.Windows;
using System.Windows.Data;
using EZEditor.Services;

namespace EZEditor.Converters;

// Visible when the bound enum's ToString() equals the string ConverterParameter.
// Enum-agnostic: works for both JsonNodeKind and XmlNodeKind.
public class KindToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? parameter, CultureInfo c)
        => value is not null && parameter is string s && value.ToString() == s
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

// Two-way display escaping for value/key editors: control characters render as
// escape sequences (real newline shows as "\n", backslash as "\\") and typed
// escapes commit the real character back to the document (see TextEscaper).
public class EscapedTextConverter : IValueConverter
{
    public object? Convert(object? value, Type t, object? p, CultureInfo c)
        => value is string s ? TextEscaper.Escape(s) : value;
    public object? ConvertBack(object? value, Type t, object? p, CultureInfo c)
        => value is string s ? TextEscaper.Unescape(s) : value;
}
