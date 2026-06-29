using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using JsonEditor.Models;
using JsonEditor.ViewModels;

namespace JsonEditor.Services;

public class JsonDocumentService
{
    public JsonNodeViewModel Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        return Build(doc.RootElement, name: null, parent: null);
    }

    private static JsonNodeViewModel Build(JsonElement el, string? name, JsonNodeViewModel? parent)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var node = new JsonNodeViewModel(JsonNodeKind.Object, name, parent: parent);
                foreach (var prop in el.EnumerateObject())
                    node.Children.Add(Build(prop.Value, prop.Name, node));
                return node;
            }
            case JsonValueKind.Array:
            {
                var node = new JsonNodeViewModel(JsonNodeKind.Array, name, parent: parent);
                foreach (var item in el.EnumerateArray())
                    node.Children.Add(Build(item, null, node));
                return node;
            }
            case JsonValueKind.String:
                return new JsonNodeViewModel(JsonNodeKind.String, name, el.GetString(), parent);
            case JsonValueKind.Number:
                return new JsonNodeViewModel(JsonNodeKind.Number, name, el.GetRawText(), parent);
            case JsonValueKind.True:
                return new JsonNodeViewModel(JsonNodeKind.Boolean, name, "true", parent);
            case JsonValueKind.False:
                return new JsonNodeViewModel(JsonNodeKind.Boolean, name, "false", parent);
            default: // Null / Undefined
                return new JsonNodeViewModel(JsonNodeKind.Null, name, null, parent);
        }
    }

    public string Serialize(JsonNodeViewModel root)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
               {
                   Indented = true,
                   IndentCharacter = ' ',
                   IndentSize = 2
               }))
        {
            Write(writer, root);
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Write(Utf8JsonWriter w, JsonNodeViewModel node)
    {
        switch (node.Kind)
        {
            case JsonNodeKind.Object:
                w.WriteStartObject();
                foreach (var c in node.Children)
                {
                    w.WritePropertyName(c.Name ?? string.Empty);
                    Write(w, c);
                }
                w.WriteEndObject();
                break;
            case JsonNodeKind.Array:
                w.WriteStartArray();
                foreach (var c in node.Children)
                    Write(w, c);
                w.WriteEndArray();
                break;
            case JsonNodeKind.String:
                w.WriteStringValue(node.Value ?? string.Empty);
                break;
            case JsonNodeKind.Number:
                // WriteRawValue validates by default and would throw on invalid text,
                // so fall back to 0 for empty/invalid numbers to keep Save crash-free.
                w.WriteRawValue(IsValidNumber(node.Value) ? node.Value! : "0");
                break;
            case JsonNodeKind.Boolean:
                w.WriteBooleanValue(string.Equals(node.Value, "true", StringComparison.OrdinalIgnoreCase));
                break;
            default:
                w.WriteNullValue();
                break;
        }
    }

    // True when the text is a valid, finite JSON number. Shared by the serializer
    // (crash-safety backstop) and the number-field validation rule.
    public static bool IsValidNumber(string? s)
        => !string.IsNullOrWhiteSpace(s)
           && double.TryParse(s, NumberStyles.Float | NumberStyles.AllowLeadingSign,
                  CultureInfo.InvariantCulture, out var d)
           && double.IsFinite(d);

    public JsonNodeViewModel Load(string path) => Parse(File.ReadAllText(path));

    public void Save(JsonNodeViewModel root, string path)
    {
        var json = Serialize(root);
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }
}
