using System.Text;

namespace EZEditor.Services;

// Display-layer escaping for control characters: real newline <-> "\n", literal
// backslash <-> "\\" (JSON-style). Keeps every value on one visual line in the
// editors while the underlying document keeps the real characters.
public static class TextEscaper
{
    public static string Escape(string text)
    {
        var needsEscape = false;
        foreach (var ch in text)
            if (ch < ' ' || ch == '\\') { needsEscape = true; break; }
        if (!needsEscape) return text;

        var sb = new StringBuilder(text.Length + 8);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            switch (c)
            {
                case '\\': sb.Append(@"\\"); break;
                case '\n': sb.Append(@"\n"); break;
                case '\r': sb.Append(@"\r"); break;
                case '\t': sb.Append(@"\t"); break;
                case '\b': sb.Append(@"\b"); break;
                case '\f': sb.Append(@"\f"); break;
                default:
                    if (c < ' ') sb.Append(@"\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    // Lenient reverse of Escape: an unrecognized or incomplete sequence ("\q",
    // trailing "\", malformed "\uXYZ") is kept literally — user input inside a
    // binding must never throw.
    public static string Unescape(string text)
    {
        var first = text.IndexOf('\\');
        if (first < 0) return text;

        var sb = new StringBuilder(text.Length).Append(text, 0, first);
        for (var i = first; i < text.Length; i++)
        {
            var c = text[i];
            if (c != '\\' || i == text.Length - 1)
            {
                sb.Append(c);
                continue;
            }
            var next = text[i + 1];
            switch (next)
            {
                case '\\': sb.Append('\\'); i++; break;
                case 'n': sb.Append('\n'); i++; break;
                case 'r': sb.Append('\r'); i++; break;
                case 't': sb.Append('\t'); i++; break;
                case 'b': sb.Append('\b'); i++; break;
                case 'f': sb.Append('\f'); i++; break;
                case 'u' when i + 5 < text.Length
                              && int.TryParse(text.AsSpan(i + 2, 4),
                                              System.Globalization.NumberStyles.HexNumber, null, out var code):
                    sb.Append((char)code); i += 5; break;
                default: sb.Append(c); break; // keep the backslash literally
            }
        }
        return sb.ToString();
    }
}
