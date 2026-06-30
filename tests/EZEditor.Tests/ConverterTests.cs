using System.Globalization;
using System.Windows;
using EZEditor.Converters;
using EZEditor.Models;

namespace EZEditor.Tests;

public class ConverterTests
{
    private static readonly CultureInfo C = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(JsonNodeKind.String, "String", true)]
    [InlineData(JsonNodeKind.Number, "Number", true)]
    [InlineData(JsonNodeKind.Number, "String", false)]
    [InlineData(JsonNodeKind.Object, "Object", true)]
    [InlineData(JsonNodeKind.Null, "Boolean", false)]
    public void KindToVisibility_VisibleOnlyWhenKindMatchesParameter(JsonNodeKind kind, string param, bool visible)
    {
        var result = new KindToVisibilityConverter().Convert(kind, typeof(Visibility), param, C);
        Assert.Equal(visible ? Visibility.Visible : Visibility.Collapsed, result);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("anything", false)]
    [InlineData(null, false)]
    public void StringBool_Convert_ParsesTrueCaseInsensitive(string? value, bool expected)
    {
        Assert.Equal(expected, new StringBoolConverter().Convert(value, typeof(bool), null, C));
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void StringBool_ConvertBack_EmitsCanonicalString(bool value, string expected)
    {
        Assert.Equal(expected, new StringBoolConverter().ConvertBack(value, typeof(string), null, C));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void BoolToVisibility_VisibleWhenTrue(bool value, bool visible)
    {
        var result = new BoolToVisibilityConverter().Convert(value, typeof(Visibility), null, C);
        Assert.Equal(visible ? Visibility.Visible : Visibility.Collapsed, result);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void InverseBoolToVisibility_VisibleWhenFalse(bool value, bool visible)
    {
        var result = new InverseBoolToVisibilityConverter().Convert(value, typeof(Visibility), null, C);
        Assert.Equal(visible ? Visibility.Visible : Visibility.Collapsed, result);
    }
}
