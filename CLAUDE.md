# EZEditor — project guide for Claude

A native **Windows WPF desktop app** (C#, **.NET 9** — `net9.0-windows7.0`) that opens a JSON file and shows it in a
beautiful, syntax‑highlighted, collapsible **tree** with **type‑aware** inline editors, then saves
valid JSON back. Author/credit: **yonka** (shown bottom‑right of the window).

## Build / run / test

- Solution file is **`EZEditor.slnx`** (the new XML solution format — NOT `.sln`). Use that name:
  - Build: `dotnet build EZEditor.slnx`
  - Test:  `dotnet test EZEditor.slnx`  (xUnit; **105 tests** — logic, converters, and a WPF view
    smoke test that builds the real `MainWindow` on an STA thread). The **test project has
    `UseWPF=true`** (so it can use `Visibility`/construct the window) — that drops the implicit
    `System.IO` using, which is re-added via `<Using Include="System.IO" />` in the test csproj.
- Publish single-file x64 exes (output gitignored under `publish/`):
  - Self-contained (no runtime needed, ~126 MB): `dotnet publish src/EZEditor/EZEditor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/self-contained-x64`
  - Framework-dependent (needs .NET 9 Desktop Runtime, ~0.4 MB): same with `--self-contained false`
- App exe: `src/EZEditor/bin/Debug/net9.0-windows7.0/EZEditor.exe`
- Open a file from the command line (also powers Windows "Open with"): `EZEditor.exe "C:\path\file.json"`
- Sample file: `samples/sample.json`

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

JSON is loaded into a tree of `JsonNodeViewModel`; the UI binds to it and serializes it back.
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
  app.manifest                       requireAdministrator (always elevated)
  icon.ico                           app icon ({ } braces + 3 type-colored dots)
  Models/JsonNodeKind.cs             enum: Object, Array, String, Number, Boolean, Null
  ViewModels/
    JsonNodeViewModel.cs             tree node (see below)
    MainViewModel.cs                 commands + document state (see below)
  Services/
    JsonDocumentService.cs           Parse / Serialize / Load / Save / IsValidNumber
    IFileDialogService.cs + FileDialogService.cs   open/save dialogs (abstracted for tests)
    IUserPrompt.cs + MessageBoxPrompt.cs           Error / ConfirmDiscard (PromptResult enum)
  Converters/Converters.cs           KindToVisibility, StringBool, BoolToVisibility, InverseBoolToVisibility
  Validation/NumberValidationRule.cs number-field input validation (uses IsValidNumber)
  Themes/Theme.xaml                  palette, fonts, all control styles + TreeViewItem template
tests/EZEditor.Tests/                xUnit (net9.0-windows7.0), references the app project
docs/superpowers/                    specs/ and plans/ (design + implementation plan)
```

### Key types
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
- **`MainViewModel`** — `Roots` (single root, wrapped for the TreeView), `CurrentPath`, `IsDirty`,
  `FilterText`, `StatusText`; commands `Open`, `Save`, `SaveAs`, `Reload`, `AddChild`, `DeleteNode`,
  `MakeString/MakeNumber/MakeBoolean/MakeNull/MakeObject/MakeArray`, `ExpandAll`, `CollapseAll`
  (CollapseAll keeps the root open); `OpenPath(path)`, `ConfirmDiscardIfDirty()`. File-op catches cover `IOException`, `JsonException`,
  `UnauthorizedAccessException` (read‑only/ACL) — and Save also `InvalidOperationException`.
- **`JsonDocumentService`** — parse via `JsonDocument`/`JsonElement`; serialize via `Utf8JsonWriter`
  (Indented, **2‑space**, preserves **object key order** and **exact number text** via
  `GetRawText()`/`WriteRawValue`). `IsValidNumber` is a **syntactic JSON‑number check using
  `Utf8JsonReader`** (NOT `double` — so `1e500` and high‑precision numbers round‑trip instead of
  becoming `0`). Invalid number text serializes as `0` (crash‑safety backstop).

## Features (all implemented)
Open / Save / Save As / **Reload from disk**; **Open Externally** (Notepad++ if installed — found via
registry `App Paths\notepad++.exe` or `Program Files\Notepad++`, else the OS default app);
type‑aware inline editors (string=green, number=orange/with red‑border validation, boolean=toggle
switch, null, object/array show child counts); **keys blue**; add/delete/rename/**change‑type** via
right‑click context menu; **Filter** box (keys + values); dirty `●` in status bar; unsaved‑changes
prompt on Open/Reload/Close; **hover a key/field to see its type** (tooltip, not on the value);
**Expand all / Collapse all** (right-click any node OR empty tree space); **Shift+mouse-wheel scrolls
horizontally**; **custom slim dark scrollbar** (fixed-size thumb); shortcuts
**Ctrl+S / Ctrl+Shift+S / Ctrl+O / Ctrl+R**. Default window 760×560.

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
  with fakes. Keep parse/serialize/edit/filter logic in `JsonDocumentService` / `JsonNodeViewModel`
  (no WPF types) so it stays unit‑testable.
- JSON output: 2‑space indent, preserve key order and number text.
- Fonts: `MonoFont` = "Cascadia Code, Consolas" (tree data), `UiFont` = Segoe UI (chrome). Tree
  editor text is 13; the toolbar "EZEditor" wordmark is 20. Palette + all styles live in
  `Themes/Theme.xaml`.

## Git
- Remote: `https://github.com/yonka2019/JSONEditor.git`, branch **`main`**. The GitHub repo was
  intentionally **not** renamed when the project became EZEditor (local/code/app rename only), so the
  remote name stays `JSONEditor.git`.
- **NEVER commit or push without the user's explicit request** (their standing rule). Local commit
  identity is set to `yonka2019` / `jonatan@cyray.io`. `.gitignore` excludes `bin/`/`obj/`,
  `publish/`, and `.superpowers/` (SDD scratch).

## Deferred / known limitations
- On load, containers deeper than 2 levels start collapsed (`AutoExpandDepth=2` in `JsonDocumentService.Build`) — shows the first two key levels, helps large files.
- `samples/` also has `big-sample.json` (300 objects) and `huge-sample.json` (900 objects) as scroll-test fixtures.
- Duplicate‑key rename produces valid‑but‑pathological JSON (one member lost on reload); no guard yet.
- Not built: JSON Schema validation, undo/redo, multi‑tab/multi‑file, raw‑text split view.
