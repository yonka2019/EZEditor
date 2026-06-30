# EZEditor — Multi-Format (JSON / CSV / XML) Editor — Design Spec

**Date:** 2026-06-30
**Status:** Approved (design)
**Supersedes naming in:** `2026-06-29-json-editor-design.md` (project renamed JSONEditor → EZEditor)

## 1. Purpose

Evolve the existing native Windows JSON editor into **EZEditor**, a multi-format
structured editor that opens, edits, and saves **JSON, CSV, and XML** files. Each
format is edited through a structured model (never free-form text) with an editor
surface natural to that format, so saved output is always well-formed. The rename
from "JSONEditor" to "EZEditor" reflects that it is no longer JSON-only.

Author/credit: **yonka** (shown bottom-right of the window).

## 2. Stack (unchanged)

- **.NET 9 + WPF**, C# (`net9.0-windows7.0`, `WinExe`), runs elevated (`app.manifest`).
- **MVVM** via **CommunityToolkit.Mvvm** (`[ObservableProperty]`, `[RelayCommand]`).
- Pure native WPF (no third-party UI library).
- `System.Text.Json` (JSON), `System.Xml.Linq` / `XDocument` (XML), hand-rolled
  RFC-4180 reader/writer (CSV) — all built-in, no new NuGet dependencies.
- **xUnit** test project (`UseWPF=true`, builds the real window on an STA thread).

## 3. Decisions (resolved during brainstorming)

1. **CSV editor** → spreadsheet **DataGrid** (rows × columns), not a tree.
2. **XML model** → **faithful tree**: preserve declaration, attributes, comments,
   namespaces, and element order; saves back as structurally-matching XML.
3. **Format detection** → **auto-detect by content** (extension is a tiebreaker only
   for ambiguous content).
4. **Git repo** → **not** renamed; remote stays `JSONEditor.git`. Rename is
   local / solution / code / app / docs only.

## 4. Part 1 — Rename JSONEditor → EZEditor

Type names that are inherently JSON-specific (`JsonNodeViewModel`,
`JsonDocumentService`, `JsonNodeKind`) are **kept** — they become the JSON-format
types alongside the new XML/CSV types. Only the project identity and root namespace
change.

| Surface | Change |
|---|---|
| Solution | `JSONEditor.slnx` → `EZEditor.slnx` (update both `<Project Path=…>` entries) |
| App project | dir `src/JsonEditor/` → `src/EZEditor/`; `JsonEditor.csproj` → `EZEditor.csproj`; `RootNamespace` → `EZEditor`; add `<AssemblyName>EZEditor</AssemblyName>` ⇒ output `EZEditor.exe` |
| Test project | dir `tests/JsonEditor.Tests/` → `tests/EZEditor.Tests/`; csproj renamed; `ProjectReference` updated; namespace `JsonEditor.Tests` → `EZEditor.Tests` |
| Namespaces | `JsonEditor.*` → `EZEditor.*` across all `.cs` and XAML (`JsonEditor.ViewModels`/`.Services`/`.Models`/`.Validation`/`.Converters`) |
| XAML | `x:Class="JsonEditor.MainWindow"`→`EZEditor.MainWindow`; `x:Class` in `App.xaml`; all `clr-namespace:JsonEditor.*`; pack URI `pack://application:,,,/EZEditor;component/icon.ico` |
| UI text | Toolbar wordmark `JSON`/` Editor` → **EZEditor**; window `Title` → `EZEditor` and reflects the open file + format, e.g. `EZEditor — data.csv [CSV]` |
| Manifest | `app.manifest` assembly `identity` name (if present) → `EZEditor` |
| Docs | `CLAUDE.md` and `README.md` updated to EZEditor, multi-format, new paths/exe name |
| Build commands | All `dotnet build/test/publish` paths updated (`EZEditor.slnx`, `src/EZEditor/EZEditor.csproj`) |

Untracked `src/JsonEditor/Properties/` moves with the project directory.

## 5. Part 2 — Multi-format architecture

### 5.1 Document abstraction (the shell talks only to this)

- **`DocumentFormat`** enum: `Json`, `Xml`, `Csv`.
- **`EditableDocument`** — abstract base (in `ViewModels`):
  - `DocumentFormat Format { get; }`
  - `bool IsDirty` + `event EventHandler? Changed` (raised on any edit)
  - `string Serialize()` and `void Save(string path)`
  - `void ApplyFilter(string? text)`
  - `string DisplayName { get; }`

  The shell (`MainViewModel`) holds exactly one `EditableDocument` at a time and
  never reaches into format-specific internals.

### 5.2 Format implementations

- **`JsonDocument : EditableDocument`** — wraps the existing `JsonNodeViewModel`
  root and delegates to `JsonDocumentService`. Existing JSON edit/filter/serialize
  behavior is preserved; this is a thin refactor of today's `MainViewModel` root
  handling behind the base class.
- **`XmlDocument : EditableDocument`** — owns an `XmlNodeViewModel` tree:
  - `XmlNodeKind` enum: `Element`, `Attribute`, `Text`, `Comment`, `CData`,
    `Declaration` (XML declaration / processing instruction).
  - `XmlDocumentService.Parse/Serialize/Load/Save` via `XDocument` with
    `LoadOptions.PreserveWhitespace`; preserves declaration, attribute order,
    namespaces/prefixes, comments, CDATA, and child order. Element edits map back
    to the `XDocument` on serialize.
  - Edit ops: rename element/attribute, edit text/attribute value, add/delete
    child element or attribute, add comment.
- **`CsvDocument : EditableDocument`** — tabular model:
  - `Columns` (header names) + `ObservableCollection<CsvRow>` where a row exposes
    indexable cells bound to `DataGrid` columns.
  - `CsvDocumentService.Parse/Serialize/Load/Save`: RFC-4180 compliant —
    handles quoted fields, embedded commas/quotes/newlines; **preserves the
    detected delimiter** (`,` default; detect `;`/`\t` from content) and a
    **header-row toggle** (default **on**: first row = column headers).
  - Edit ops: edit cell, add/delete row, add/delete/rename column.

### 5.3 Detection — `DocumentFactory`

`EditableDocument LoadAuto(string path)`:
1. Read text. Trim leading whitespace/BOM.
2. Starts with `<` or `<?xml` ⇒ **XML**.
3. Else attempt `JsonDocument.Parse` (lenient: trailing commas, comments) ⇒ if it
   succeeds, **JSON**.
4. Else ⇒ **CSV**.
5. **Tiebreaker** for ambiguous content (empty file, single column, no delimiters):
   fall back to the file **extension** (`.json`/`.xml`/`.csv`); default CSV→text-like
   single column if still unknown.

Save reuses the document's own `Format`. **Save As** may change format when the user
picks a different extension in the dialog (re-serialize through that format's service;
warn if the in-memory model can't be expressed losslessly in the target format).

### 5.4 `MainViewModel` (the shell)

Keeps `CurrentDocument` (`EditableDocument?`), `CurrentPath`, `IsDirty` (bridged from
the document's `Changed`), `FilterText`, and `StatusText` (now includes format).
`Open`/`Save`/`SaveAs`/`Reload`/`ConfirmDiscardIfDirty` delegate to the factory /
current document. The JSON-only commands (`AddChild`, `DeleteNode`, `Make*`,
`ExpandAll`, `CollapseAll`) move onto `JsonDocument` (or a JSON-specific sub-VM) and
are enabled only when the current document is JSON; XML/CSV expose their own edit
commands.

### 5.5 View — `MainWindow`

The hard-wired `TreeView` is replaced by a `ContentControl` bound to
`CurrentDocument`, with a `DataTemplate` per document type:

- `JsonDocument` → the existing JSON `TreeView` (unchanged template, virtualization,
  custom scrollbar, context menus).
- `XmlDocument` → an XML `TreeView` (`HierarchicalDataTemplate`): element names in the
  blue "key" color, attributes shown inline in a second accent, comments greyed/italic,
  text values in the string color.
- `CsvDocument` → an editable `DataGrid` styled to the dark theme (auto-generated
  columns from headers; row/column add-delete via context menu + toolbar; reuses the
  existing scrollbar/corner styling).

The toolbar, status bar, and filter box stay shared. "Open Externally" works for all
formats unchanged.

## 6. Data flow

```
File ──DocumentFactory.LoadAuto (sniff content)──> EditableDocument (Json|Xml|Csv)
     ──MainViewModel.CurrentDocument──> ContentControl picks DataTemplate ──> editor
user edits (tree / grid) ──> document.Changed ──> IsDirty = true
Save ──> document.Serialize() ──> write file in its own format
```

Each format edits only its structured model, so the in-memory document is always
well-formed. JSON object-key order, XML structure/attributes/comments, and CSV
delimiter/quoting all round-trip.

## 7. Error handling

- **Parse failure on open** → friendly, format-specific dialog (`IUserPrompt.Error`):
  "Could not parse JSON/XML…" with message/position; "Malformed CSV near line N".
  Current document is kept; app never crashes.
- **Bad field input** → JSON number keeps its inline red-border rule; XML/CSV cells
  accept free text (format permits it).
- **I/O / ACL** → existing `IOException` / `UnauthorizedAccessException` handling
  retained for all formats; Save also catches `XmlException` and CSV write errors.
- **Unsaved changes** → existing discard prompt on Open/Reload/Close, format-agnostic.
- **Save As to a different format** → if the source model has constructs the target
  can't represent, warn before writing.

## 8. Testing

All current JSON tests are retained (namespace → `EZEditor.Tests`). Added coverage
(UI-independent unless noted):

- **CSV** `CsvDocumentService` round-trip: quoted fields, embedded commas/quotes/
  newlines, delimiter detection (`,`/`;`/tab), header on/off; cell/row/column edits.
- **XML** `XmlDocumentService` round-trip: declaration, attributes (order + values),
  namespaces/prefixes, comments, CDATA, element order; element/attribute edits.
- **`DocumentFactory`** detection: JSON vs XML vs CSV by content, extension tiebreaker
  for ambiguous input.
- **Shell**: `MainViewModel` open/save/reload/dirty/filter across formats with fakes.
- **WPF smoke test**: build the real `MainWindow` on an STA thread with a JSON, an
  XML, and a CSV document; assert the correct editor template is realized.

## 9. Theme

Reuse the existing palette and fonts. XML element names → existing blue key color;
attribute names → a second accent (added to `Theme.xaml`); comments → muted italic.
The `DataGrid` is restyled for the dark theme (header, gridlines, cell/row selection,
edit caret) and inherits the slim dark scrollbar + recolored corner.

## 10. Phasing (implementation order)

1. **Rename** JSONEditor → EZEditor (mechanical; keep all tests green).
2. **Document abstraction**: extract `EditableDocument` + `JsonDocument`, swap
   `MainWindow` to the `ContentControl`/template approach (JSON still works).
3. **CSV**: `CsvDocument`/service + `DataGrid` view + tests.
4. **XML**: `XmlDocument`/`XmlNodeViewModel`/service + XML tree view + tests.
5. **Detection + Save As format switch**, docs (`CLAUDE.md`/`README.md`), samples.

## 11. Scope

**In scope:** rename to EZEditor; open/save/save-as/reload for JSON, CSV, XML;
content auto-detection; CSV grid editing (cells, rows, columns, header toggle);
faithful XML tree editing (elements, attributes, text, comments); shared
filter/dirty/status/external-open; friendly per-format errors; tests; updated docs;
optional `samples/sample.csv` + `samples/sample.xml` fixtures.

**Out of scope:** JSON Schema / XSD validation; undo/redo; multi-tab/multi-file;
raw-text split view; CSV formula evaluation; XML pretty-reformat-on-load (whitespace
is preserved, not normalized); renaming the GitHub repository.
