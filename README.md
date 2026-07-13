# EZEditor

A native **Windows desktop** editor (C#, **WPF**, .NET 9) that opens **JSON, CSV, and XML**
files and edits them in a structured, type-aware UI — never as free-form text — then saves
valid output back. Format is **auto-detected from file content**.

![status](https://img.shields.io/badge/platform-Windows-blue) · by **yonka**

## Formats

| Format | Editor | Fidelity |
|--------|--------|----------|
| **JSON** | Collapsible tree with type-aware inline editors (string/number/bool/null/object/array) | Preserves key order + exact number text, 2-space indent |
| **CSV**  | Spreadsheet grid (rows × columns), add/delete row & column, header row; Enter commits the cell edit in place | RFC-4180 quoting; delimiter (`,` `;` tab) detected & preserved |
| **XML**  | Faithful element tree: editable element names, inline attributes, comments, CDATA | Preserves declaration, attributes, namespaces, comments, element order |

## Features

- Auto-detect format by content (extension is a tiebreaker only)
- Open / Save / Save As / Reload from disk; "Open Externally" (Notepad++ if installed, else default app)
- Control characters display as escape sequences (`\n`, `\t`, `\r`, `\\`, `\uXXXX`) — values
  stay on one line; typing `\n` in an editor commits a real newline (JSON-style, exact round-trip)
- Filter box (matches keys/values), dirty `●` indicator, unsaved-changes prompt
- Format tag `[JSON]` / `[CSV]` / `[XML]` in the status bar
- Dark native theme, custom slim scrollbar, Shift+wheel horizontal scroll
- Shortcuts: Ctrl+S / Ctrl+Shift+S / Ctrl+O / Ctrl+R
- Runs elevated (administrator)

## Build / run / test

```sh
dotnet build EZEditor.slnx
dotnet test  EZEditor.slnx
```

App exe: `src/EZEditor/bin/Debug/net9.0-windows7.0/EZEditor.exe`  
Open a file from the CLI (also powers "Open with"): `EZEditor.exe "C:\path\file.csv"`  
Samples: `samples/sample.json`, `samples/sample.csv`, `samples/sample.xml`

### Publish single-file x64

```sh
dotnet publish src/EZEditor/EZEditor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/self-contained-x64
```

## Architecture

MVVM (CommunityToolkit.Mvvm). A format-agnostic shell (`MainViewModel`) holds one
`EditableDocument`; `DocumentFactory` sniffs content and builds a `JsonDocument`,
`CsvDocument`, or `XmlDocument`. `MainWindow` swaps the editor surface via a `ContentControl`
with one `DataTemplate` per document type. Parse/serialize/edit logic is WPF-free and unit-tested.

## License / credit

Author: **yonka** (jonatan@cyray.io). Repository: https://github.com/yonka2019/EZEditor
