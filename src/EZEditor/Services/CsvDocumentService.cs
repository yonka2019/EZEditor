using System.Text;

namespace EZEditor.Services;

public sealed class CsvDocumentService
{
    private static readonly char[] Candidates = { ',', ';', '\t' };

    public static char DetectDelimiter(string text)
    {
        var nl = text.IndexOfAny(new[] { '\r', '\n' });
        var firstLine = nl < 0 ? text : text[..nl];
        var best = ','; var bestCount = -1;
        foreach (var c in Candidates)
        {
            var count = firstLine.Count(ch => ch == c);
            if (count > bestCount) { bestCount = count; best = c; }
        }
        return best;
    }

    // RFC-4180 reader. Records end on a newline that is NOT inside quotes.
    public List<List<string>> ParseRows(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var sawAny = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(ch);
                continue;
            }

            if (ch == '"') { inQuotes = true; sawAny = true; }
            else if (ch == delimiter) { row.Add(field.ToString()); field.Clear(); sawAny = true; }
            else if (ch == '\r') { /* swallow; handled by \n or EOF */ }
            else if (ch == '\n')
            {
                row.Add(field.ToString()); field.Clear();
                rows.Add(row); row = new List<string>(); sawAny = false;
            }
            else { field.Append(ch); sawAny = true; }
        }

        if (sawAny || field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return rows;
    }

    public string Serialize(
        IReadOnlyList<string> header,
        IReadOnlyList<IReadOnlyList<string>> rows,
        char delimiter,
        bool hasHeader)
    {
        var lines = new List<string>();
        if (hasHeader) lines.Add(JoinRow(header, delimiter));
        foreach (var r in rows) lines.Add(JoinRow(r, delimiter));
        return string.Join("\r\n", lines);
    }

    private static string JoinRow(IReadOnlyList<string> fields, char delimiter)
        => string.Join(delimiter, fields.Select(f => Quote(f, delimiter)));

    private static string Quote(string field, char delimiter)
    {
        var needs = field.Contains(delimiter) || field.Contains('"')
                    || field.Contains('\r') || field.Contains('\n');
        if (!needs) return field;
        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }
}
