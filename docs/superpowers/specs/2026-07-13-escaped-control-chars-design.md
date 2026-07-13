# Escaped control characters in value displays — design

**Date:** 2026-07-13
**Status:** approved (chat), implementing

## Problem

String values containing control characters (newline, tab, CR…) render literally in the
editor UI: a JSON string with `\n` breaks into two visual lines inside its tree row, a CSV
cell with an embedded newline stretches the row height, XML text nodes wrap the tree row.
The user wants these shown as escape sequences (`\n`, `\t`, …) — one visual line per value —
while the underlying data (and saved file) keeps the real characters.

## Decisions (user-confirmed)

- **Scope:** all formats — JSON string values + keys, CSV cells + column headers,
  XML attribute values / text / CData / comments.
- **Backslash:** JSON-style. A real newline displays as `\n` (single backslash); a literal
  backslash displays as `\\`. Guarantees exact round-trip — a literal `\n` two-char sequence
  in data displays as `\\n` and can never be confused with a real newline.

## Approach

Two-way display converter; data layer untouched.

- **`Services/TextEscaper.cs`** (static, no WPF types — unit-testable):
  - `Escape(string)`: `\` → `\\`, LF → `\n`, CR → `\r`, TAB → `\t`, BS → `\b`, FF → `\f`,
    any other control char < U+0020 → `\uXXXX`. Returns the same instance when nothing to
    escape (fast path).
  - `Unescape(string)`: exact reverse. **Lenient**: an unrecognized or incomplete escape
    (`\q`, trailing `\`, malformed `\uXYZ`) is kept literally — editing must never throw
    inside a binding.
- **`EscapedTextConverter`** in `Converters/Converters.cs`: `Convert` = Escape,
  `ConvertBack` = Unescape (pass-through for non-strings). Registered in `Themes/Theme.xaml`
  as `EscText`.
- **Wire-up:**
  - JSON: key editor TextBox (`Name` binding) and string value TextBox (`MainWindow.xaml`).
  - XML: attribute value, Text, CData TextBoxes + comment TextBlock (converter composes with
    its existing `StringFormat`). NOT element/attribute names (control chars are invalid in
    XML names anyway; XLinq would reject them).
  - CSV: `DataGridTextColumn` bindings get a shared static converter instance in
    `BuildCsvColumns` (`MainWindow.xaml.cs`); column `Header` text display-escaped via
    `TextEscaper.Escape` (no rename UI exists, so display-only is safe).
  - Untouched: number editor (validation forbids control chars), boolean toggle, null label,
    child-count labels, index labels.

## Editing semantics

Typing `\n` in any wired editor commits a **real newline** into the value; `\\` commits a
backslash; `\uXXXX` commits that character. WPF does not push a binding's own source update
back into the focused TextBox, so no caret jumps / mid-typing rewrites; existing
`UpdateSourceTrigger`s stay as they are. Saved output is unchanged by display escaping —
JSON serialization escapes as before, CSV quotes embedded newlines per RFC-4180, XML
round-trips via XDocument.

## Known limitations

- Filter box matches **raw** text: searching `\n` will not match a real newline.
- With `UpdateSourceTrigger=PropertyChanged` (JSON string, XML text/CData), intermediate
  keystrokes of a multi-char escape transiently commit partial values (e.g. typing `\t…`
  passes through a real tab); final state is correct.

## Testing

- xUnit `TextEscaperTests`: escape of each mapped char, `\uXXXX` for other control chars,
  backslash doubling, round-trip property (`Unescape(Escape(s)) == s`), lenient invalid
  sequences, empty/no-op fast path.
- `ConverterTests`: `EscapedTextConverter` Convert/ConvertBack incl. non-string pass-through.
- Existing 169 tests must stay green (data layer untouched).
