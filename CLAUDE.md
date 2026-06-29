# JSONEditor — project guide for Claude

A native **Windows WPF desktop app** (C#, **.NET 10**) that opens a JSON file and shows it in a
beautiful, syntax‑highlighted, collapsible **tree** with **type‑aware** inline editors, then saves
valid JSON back. Author/credit: **yonka** (shown bottom‑right of the window).

## Build / run / test

- Solution file is **`JSONEditor.slnx`** (the new XML solution format — NOT `.sln`). Use that name:
  - Build: `dotnet build JSONEditor.slnx`
  - Test:  `dotnet test JSONEditor.slnx`  (xUnit; currently **85 tests**, all green)
- App exe: `src/JsonEditor/bin/Debug/net10.0-windows/JsonEditor.exe`
- Open a file from the command line (also powers Windows "Open with"): `JsonEditor.exe "C:\path\file.json"`
- Sample file: `samples/sample.json`

### ⚠️ The app runs ELEVATED (admin) — important build/run gotchas
`src/JsonEditor/app.manifest` requests `requireAdministrator`, so the app **always launches as
administrator**. Consequences when working on it:
- **Launch it with `Start-Process` (ShellExecute), not a bare exec.** A plain `CreateProcess`
  (e.g. running the exe directly from the Bash tool) fails with *"requires elevation"*.
  `Start-Process $exe -ArgumentList '"<file>"'` elevates correctly. (This machine's UAC is
  "never notify", so it auto‑elevates with no prompt.)
- **A running instance locks the exe and a non‑elevated shell CANNOT kill it** ("Access is
  denied"). **Before every rebuild**, kill it elevated:
  `Start-Process taskkill -ArgumentList '/F','/IM','JsonEditor.exe' -Verb RunAs -Wait`
- **You cannot screenshot the elevated window** from non‑elevated tooling (UAC isolation). Also,
  `PrintWindow` on an occluded WPF window returns a blank white client area — don't trust a blank
  capture as "the UI broke"; verify health via the process being alive + responding instead.

## Architecture (MVVM, logic is UI‑independent and unit‑tested)

JSON is loaded into a tree of `JsonNodeViewModel`; the UI binds to it and serializes it back.
No third‑party UI library — **pure native WPF** (WPF‑UI was tried and removed; the user dislikes it).
Only NuGet dependency: **CommunityToolkit.Mvvm** (`[ObservableProperty]` / `[RelayCommand]`).

```
JSONEditor.slnx
src/JsonEditor/                      WPF app (net10.0-windows, WinExe)
  App.xaml / App.xaml.cs             merges Theme.xaml; OnStartup builds MainViewModel,
                                     opens e.Args[0] if a valid file, shows window
  MainWindow.xaml / .cs              native Window; dark title bar via DWM P/Invoke;
                                     Closing guard for unsaved changes;
                                     OnOpenExternally (+ FindNotepadPlusPlus)
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
tests/JsonEditor.Tests/              xUnit (net10.0-windows), references the app project
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
  `MakeString/MakeNumber/MakeBoolean/MakeNull/MakeObject/MakeArray`; `OpenPath(path)`,
  `ConfirmDiscardIfDirty()`. File-op catches cover `IOException`, `JsonException`,
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
shortcuts **Ctrl+S / Ctrl+Shift+S / Ctrl+O / Ctrl+R**.

## Conventions
- Clean MVVM. All file/dialog/prompt access goes through interfaces so view‑models are testable
  with fakes. Keep parse/serialize/edit/filter logic in `JsonDocumentService` / `JsonNodeViewModel`
  (no WPF types) so it stays unit‑testable.
- JSON output: 2‑space indent, preserve key order and number text.
- Fonts: `MonoFont` = "Cascadia Code, Consolas" (tree data), `UiFont` = Segoe UI (chrome). Tree
  editor text is 13; the toolbar "JSON Editor" wordmark is 20. Palette + all styles live in
  `Themes/Theme.xaml`.

## Git
- Remote: `https://github.com/yonka2019/JSONEditor.git`, branch **`main`**.
- **NEVER commit or push without the user's explicit request** (their standing rule). Local commit
  identity is set to `yonka2019` / `jonatan@cyray.io`. `.gitignore` excludes `bin/`/`obj/`.

## Deferred / known limitations
- Large files: every node defaults to expanded on load → defeats TreeView virtualization.
- Duplicate‑key rename produces valid‑but‑pathological JSON (one member lost on reload); no guard yet.
- Not built: JSON Schema validation, undo/redo, multi‑tab/multi‑file, raw‑text split view.
