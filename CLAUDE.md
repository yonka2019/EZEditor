# EZEditor — project guide for Claude

A native **Windows WPF desktop app** (C#, **.NET 9** — `net9.0-windows7.0`) that opens **JSON,
CSV, and XML** files and edits them in a structured, type-aware UI — never as free-form text —
then saves valid output back. **Format is auto-detected from file content.** Author/credit:
**yonka** (shown bottom-right of the window).

## Build / run / test

- Solution file is **`EZEditor.slnx`** (the new XML solution format — NOT `.sln`). Use that name:
  - Build: `dotnet build EZEditor.slnx`
  - Test:  `dotnet test EZEditor.slnx`  (xUnit; **169 tests** — logic, converters, EditableDocument/
    JsonDocument/CsvDocument/XmlDocument/DocumentFactory/service tests, and WPF view smoke tests
    that build the real `MainWindow` on an STA thread). The **test project has `UseWPF=true`**
    (so it can use `Visibility`/construct the window) — that drops the implicit `System.IO` using,
    which is re-added via `<Using Include="System.IO" />` in the test csproj.
- Publish single-file x64 exes (output gitignored under `publish/`):
  - Self-contained (no runtime needed, ~126 MB): `dotnet publish src/EZEditor/EZEditor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/self-contained-x64`
  - Framework-dependent (needs .NET 9 Desktop Runtime, ~0.4 MB): same with `--self-contained false`
- App exe: `src/EZEditor/bin/Debug/net9.0-windows7.0/EZEditor.exe`
- Open a file from the command line (also powers Windows "Open with"): `EZEditor.exe "C:\path\file.csv"`
- Sample files: `samples/sample.json`, `samples/sample.csv`, `samples/sample.xml`

### ⚠️ The app runs ELEVATED (admin) — important build/run gotchas
`src/EZEditor/app.manifest` requests `requireAdministrator`, so the app **always launches as
administrator**. Consequences when working on it:
- **Launch it with `Start-Process` (ShellExecute), not a bare exec.** A plain `CreateProcess`
  (e.g. running the exe directly from the Bash tool) fails with *"requires elevation"*.
  `Start-Process $exe -ArgumentList '"<file>"'` elevates correctly. (This machine's UAC is
  "never notify", so it auto‑elevates with no prompt.)
- **A running instance locks the exe and a non‑elevated shell CANNOT kill it** ("Access is
  denied"). **Before every rebuild**, kill it elevated:
  `Start-Process taskkill -ArgumentList '/F','/IM','EZEditor.exe' -Verb RunAs -Wait`
- **You cannot screenshot the elevated window** from non‑elevated tooling (UAC isolation). Also,
  `PrintWindow` on an occluded WPF window returns a blank white client area — don't trust a blank
  capture as "the UI broke"; verify health via the process being alive + responding instead.

## Architecture (MVVM, logic is UI‑independent and unit‑tested)

A format-agnostic shell (`MainViewModel`) holds one `EditableDocument`; `DocumentFactory` sniffs
content and builds a `JsonDocument`, `CsvDocument`, or `XmlDocument`. `MainWindow` swaps the
editor surface via a `ContentControl` with one `DataTemplate` per document type (JSON tree
unchanged, XML faithful element tree, CSV editable `DataGrid`).
No third‑party UI library — **pure native WPF** (WPF‑UI was tried and removed; the user dislikes it).
Only NuGet dependency: **CommunityToolkit.Mvvm** (`[ObservableProperty]` / `[RelayCommand]`).

```
EZEditor.slnx
src/EZEditor/                        WPF app (net9.0-windows7.0, WinExe)
  App.xaml / App.xaml.cs             merges Theme.xaml; OnStartup builds MainViewModel,
                                     opens e.Args[0] if a valid file, shows window
  MainWindow.xaml / .cs              native Window; dark title bar via DWM P/Invoke; Closing guard
                                     for unsaved changes; OnOpenExternally (+ FindNotepadPlusPlus);
                                     Shift+wheel = horizontal scroll (OnTreePreviewMouseWheel +
                                     FindScrollViewer). Window icon uses an assembly-qualified pack URI.
                                     ContentControl bound to CurrentDocument swaps JSON TreeView /
                                     XML TreeView / CSV DataGrid by DataTemplate.
  app.manifest                       requireAdministrator (always elevated)
  icon.ico                           app icon ({ } braces + 3 type-colored dots)
  Models/
    JsonNodeKind.cs                  enum: Object, Array, String, Number, Boolean, Null
    XmlNodeKind.cs                   enum: Element, Attribute, Text, Comment, CData
  ViewModels/
    EditableDocument.cs              abstract base: Format, Changed event, Serialize(), Save() UTF-8-no-BOM, ApplyFilter()
    JsonDocument.cs                  wraps JsonNodeViewModel tree; adapts to EditableDocument
    CsvDocument.cs                   wraps CsvRow collection; adapts to EditableDocument
    CsvRow.cs                        observable row of cell strings; add/remove cells
    XmlDocument.cs                   wraps XmlNodeViewModel tree over XDocument; adapts to EditableDocument
    XmlNodeViewModel.cs              tree node: Kind, Name, Value, Attributes, Children, Parent; XObject ref
    JsonNodeViewModel.cs             tree node (see below)
    MainViewModel.cs                 commands + document state (see below)
  Services/
    DocumentFactory.cs               Detect (content sniff) + LoadAuto + Create per format
    JsonDocumentService.cs           Parse / Serialize / Load / Save / IsValidNumber
    CsvDocumentService.cs            RFC-4180 parse/serialize; delimiter detection + preservation
    CsvParseResult.cs                parse result: headers, rows, detected delimiter
    XmlDocumentService.cs            XDocument-based parse/serialize; preserves declaration/whitespace
    XmlParseResult.cs                parse result: XDocument + format metadata
    IFileDialogService.cs + FileDialogService.cs   open/save dialogs (abstracted for tests);
                                                   multi-format filters (JSON/CSV/XML/All)
    IUserPrompt.cs + MessageBoxPrompt.cs           Error / ConfirmDiscard (PromptResult enum)
  Converters/Converters.cs           KindToVisibility, StringBool, BoolToVisibility, InverseBoolToVisibility
  Validation/NumberValidationRule.cs number-field input validation (uses IsValidNumber)
  Themes/Theme.xaml                  palette, fonts, all control styles + TreeViewItem template +
                                     dark DataGrid styles for CSV
tests/EZEditor.Tests/                xUnit (net9.0-windows7.0), references the app project
docs/superpowers/                    specs/ and plans/ (design + implementation plan)
```

### Key types
- **`EditableDocument`** (abstract) — `Format` (`DocumentFormat` enum: Json/Xml/Csv), `Changed`
  event, `Serialize()`, `Save(path)` (UTF-8 no-BOM), `ApplyFilter(text)`. Subclasses:
  `JsonDocument`, `CsvDocument`, `XmlDocument`.
- **`DocumentFactory`** — `Detect(text, ext)` returns a `DocumentFormat` by content sniff: leading
  `<` ⇒ XML; structural JSON (starts with `{`/`[`/`"` and parses) ⇒ JSON; else the **extension is
  the tiebreaker** (`.json`/`.xml` ⇒ that format, else CSV). So a malformed `.json`/`.xml` still
  classifies as its declared format rather than silently becoming CSV — the parse error then
  surfaces in `Create` (which calls the format's `Parse`), not in `Detect`. `LoadAuto(path)` reads
  the file and calls `Create(format, text)`.
- **`JsonNodeViewModel`** — `Kind`, `Name` (object key; null for array elements/root), `Value`
  (scalar text for String/Number/Boolean; null otherwise), `Children`, `Parent`, `IsExpanded`,
  `IsSelected`, `IsFilteredOut`, `IsObjectMember`, `DisplayName`.
  - `Changed` event **bubbles to Parent** → `MainViewModel` subscribes to the root to set `IsDirty`.
  - `ApplyFilter(text)` — case‑insensitive, matches **keys AND values**; a `Null`‑kind node is
    treated as the literal text `"null"` (so a "null" search matches both real‑null and string
    `"null"`). Snapshots/restores `IsExpanded` so clearing the filter restores collapse state.
    Filtering must never mark the document dirty.
  - `AddChild(kind)` (unique `newKey`/`newKey2`… for objects), `Delete()`, `Rename(name)`,
    `ChangeKind(kind)` (coerces value; number coercion uses `JsonDocumentService.IsValidNumber`).
- **`XmlNodeViewModel`** — `Kind` (`XmlNodeKind`), `Name`, `Value`, `Attributes`
  (child `XmlNodeViewModel`s for attributes), `Children`, `Parent`, `XObject` (live reference to
  the underlying `XObject` in the `XDocument`). Element names shown in blue; attributes rendered
  inline in `AttrColor` (#E5C07B); comments muted italic.
- **`CsvRow`** / **`CsvDocument`** — `CsvRow` is an observable list of cell strings; `CsvDocument`
  exposes `Headers`, `Rows`, add/delete row & column operations. Right-click context menu on the
  `DataGrid` for row/column ops.
- **`MainViewModel`** — `CurrentDocument` (`EditableDocument?`), `CurrentPath`, `IsDirty`,
  `FilterText`, `StatusText` (includes `|JSON|`/`|CSV|`/`|XML|` format tag);
  `CommitPendingEdits` (`Action?` set by the view; invoked at the top of `OpenPath` to commit/
  cancel any open CSV DataGrid edit transaction BEFORE the document is swapped — swapping
  `ItemsSource` mid-edit throws in `DataGrid.ClearSortDescriptions`);
  `JsonRoot` (JSON-only commands, null when document isn't JSON);
  commands `Open`, `Save`, `SaveAs`, `Reload`, `AddChild`, `DeleteNode`,
  `MakeString/MakeNumber/MakeBoolean/MakeNull/MakeObject/MakeArray`, `ExpandAll`, `CollapseAll`
  (CollapseAll keeps the root open); `OpenPath(path)`, `ConfirmDiscardIfDirty()`.
  File-op catches cover `IOException`, `JsonException`, `XmlException`,
  `UnauthorizedAccessException` (read‑only/ACL) — and Save also `InvalidOperationException`.
- **`JsonDocumentService`** — parse via `JsonDocument`/`JsonElement`; serialize via `Utf8JsonWriter`
  (Indented, **2‑space**, preserves **object key order** and **exact number text** via
  `GetRawText()`/`WriteRawValue`). `IsValidNumber` is a **syntactic JSON‑number check using
  `Utf8JsonReader`** (NOT `double` — so `1e500` and high‑precision numbers round‑trip instead of
  becoming `0`). Invalid number text serializes as `0` (crash‑safety backstop).
- **`CsvDocumentService`** — RFC-4180 parse/serialize; auto-detects delimiter (`,` / `;` / tab)
  and preserves it on round-trip; header row enabled by default.
- **`XmlDocumentService`** — parse/serialize via `System.Xml.Linq.XDocument` with
  `LoadOptions.PreserveWhitespace`; faithful round-trip (declaration, attributes, namespaces,
  comments, CDATA, element order preserved).

## Features (all implemented)
Open / Save / Save As / **Reload from disk**; **Open Externally** (Notepad++ if installed — found via
registry `App Paths\notepad++.exe` or `Program Files\Notepad++`, else the OS default app);
**JSON**: type‑aware inline editors (string=green, number=orange/with red‑border validation, boolean=toggle
switch, null, object/array show child counts); **keys blue**; add/delete/rename/**change‑type** via
right‑click context menu; **hover a key/field to see its type** (tooltip, not on the value);
**Expand all / Collapse all** (right-click any node OR empty tree space);
**CSV**: spreadsheet `DataGrid` (rows × columns), add/delete row & column via right-click, header
row on by default, delimiter auto-detected and preserved, **Enter commits the cell edit in place**
(no move to next row — `OnCsvGridPreviewKeyDown`), column headers centered;
**XML**: faithful element tree — editable element names, attributes inline, comments/CDATA displayed,
declaration/namespaces/element order preserved;
**Filter** box (keys + values, all formats); format tag `|JSON|`/`|CSV|`/`|XML|` in status bar;
dirty `●` in status bar; unsaved‑changes prompt on Open/Reload/Close;
**Shift+mouse-wheel scrolls horizontally**; **custom slim dark scrollbar** (fixed-size thumb);
shortcuts **Ctrl+S / Ctrl+Shift+S / Ctrl+O / Ctrl+R**. Default window 760×560.

## Scrolling & custom scrollbar (hard-won notes — don't relearn)
- TreeView uses **recycling virtualization** (`VirtualizingPanel.IsVirtualizing=True`,
  `VirtualizationMode=Recycling`, `CanContentScroll=True`) with **`ScrollUnit="Item"`**.
  Do NOT switch to `ScrollUnit="Pixel"` for big files — pixel-precise scrolling forces WPF to measure
  every row across the whole tree and made huge JSON laggy. Item scrolling stays fast (~177 MB for a
  900-object / 593 KB file; virtualization is working).
- The custom `ScrollBar` (Theme.xaml) uses a **fixed-size thumb** via `Track ViewportSize="NaN"`.
  Required because WPF's `Track` **ignores the thumb's MinHeight/MinWidth when sizing proportionally**
  (viewport mode) → on a huge file the thumb shrank to ~2px. NaN = non-proportional mode, so the
  thumb's min size is honored (vertical ~80px tall, horizontal ~60px). Tradeoff: thumb size no longer
  reflects how much content there is.
- The **scrollbar corner** (where V/H bars meet) was a white box on the dark theme — fixed by
  overriding `SystemColors.ControlBrushKey` to the surface color inside `TreeView.Resources`.
- **Expand all / Collapse all** live in two menus: the per-node menu (in the HierarchicalDataTemplate;
  commands via `{x:Reference Root}`, fine because it's deferred in a template) and a **TreeView-level
  menu** for empty space (commands via `PlacementTarget.DataContext` — using `{x:Reference Root}`
  there throws a XAML cyclical-dependency error at load).

## Conventions
- Clean MVVM. All file/dialog/prompt access goes through interfaces so view‑models are testable
  with fakes. Keep parse/serialize/edit/filter logic in services / node VMs (no WPF types) so it
  stays unit‑testable. `System.Xml.Linq` and `System.Text.Json` are the only XML/JSON libraries used.
- JSON output: 2‑space indent, preserve key order and number text.
- Fonts: `MonoFont` = "Cascadia Code, Consolas" (tree data), `UiFont` = Segoe UI (chrome). Tree
  editor text is 13; the toolbar "EZEditor" wordmark is 20. Palette + all styles live in
  `Themes/Theme.xaml`.

## Git
- Remote: `https://github.com/yonka2019/EZEditor.git`, branch **`main`**. The GitHub repo was
  renamed from `JSONEditor` to `EZEditor` to match the project; GitHub redirects the old
  `JSONEditor.git` URL, but `origin` points at the canonical `EZEditor.git`.
- **NEVER commit or push without the user's explicit request** (their standing rule). Local commit
  identity is set to `yonka2019` / `jonatan@cyray.io`. `.gitignore` excludes `bin/`/`obj/`,
  `publish/`, and `.superpowers/` (SDD scratch).

## Deferred / known limitations
- On load, containers deeper than 2 levels start collapsed (`AutoExpandDepth=2` in `JsonDocumentService.Build`) — shows the first two key levels, helps large files.
- `samples/` also has `big-sample.json` (300 objects) and `huge-sample.json` (900 objects) as scroll-test fixtures, plus `big-sample.csv` (2,000 rows) and `huge-sample.csv` (50,000 rows) as CSV grid stress fixtures.
- Duplicate‑key rename produces valid‑but‑pathological JSON (one member lost on reload); no guard yet.
- **Cross-format Save As conversion not implemented** — saving a JSON document as `.csv` is not supported; Save As stays within the current format.
- **XML add-element** produces no surrounding indent whitespace (the new node is appended without adjusting adjacent text nodes).
- **CSV assumes a single delimiter per file** — mixed delimiters within one file are not handled.
- **Bare-CR (old-Mac) line endings** in CSV files are not handled.
- View smoke tests show the window off-screen to realize `ContentControl` templates without a real display.
- Not built: JSON Schema validation, undo/redo, multi‑tab/multi‑file, raw‑text split view.
