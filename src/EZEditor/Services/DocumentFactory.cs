using System.IO;
using EZEditor.ViewModels;

namespace EZEditor.Services;

public sealed class DocumentFactory
{
    private readonly JsonDocumentService _json = new();

    public EditableDocument LoadAuto(string path)
    {
        var text = File.ReadAllText(path);
        return Create(Detect(text, Path.GetExtension(path)), text);
    }

    public EditableDocument Create(DocumentFormat fmt, string text) => fmt switch
    {
        DocumentFormat.Json => new JsonDocument(_json.Parse(text), _json),
        DocumentFormat.Csv => throw new NotSupportedException("CSV not wired yet"),
        DocumentFormat.Xml => throw new NotSupportedException("XML not wired yet"),
        _ => throw new NotSupportedException($"Unhandled format {fmt}"),
    };

    // Content sniff: leading '<' => XML; else structural JSON => JSON; else CSV.
    // Extension is the tiebreaker only when the trimmed content is empty/ambiguous.
    public static DocumentFormat Detect(string text, string? ext = null)
    {
        var t = text.TrimStart('﻿', ' ', '\t', '\r', '\n'); // ﻿ = BOM
        if (t.Length == 0) return ExtFormat(ext);
        if (t[0] == '<') return DocumentFormat.Xml;
        if (LooksLikeJson(t)) return DocumentFormat.Json;
        return DocumentFormat.Csv;
    }

    private static DocumentFormat ExtFormat(string? ext) => ext?.ToLowerInvariant() switch
    {
        ".json" => DocumentFormat.Json,
        ".xml" => DocumentFormat.Xml,
        _ => DocumentFormat.Csv,
    };

    private static bool LooksLikeJson(string t)
    {
        if (t[0] is not ('{' or '[' or '"')) return false;
        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(t, new System.Text.Json.JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = System.Text.Json.JsonCommentHandling.Skip
            });
            return true;
        }
        catch (System.Text.Json.JsonException) { return false; }
    }
}
