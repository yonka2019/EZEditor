# EZEditor Multi-Format Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename JSONEditor → EZEditor and turn the JSON-only tree editor into a multi-format editor that opens/edits/saves JSON, CSV, and XML, auto-detecting format by content.

**Architecture:** A format-agnostic shell (`MainViewModel` + toolbar/status/filter) holds one `EditableDocument` at a time. Three implementations — `JsonDocument` (existing tree), `CsvDocument` (tabular), `XmlDocument` (faithful element tree) — each wrap a UI-independent service. `DocumentFactory` sniffs file content and builds the right document. `MainWindow` swaps the editor surface through a `ContentControl` with one `DataTemplate` per document type (JSON `TreeView`, XML `TreeView`, CSV `DataGrid`).

**Tech Stack:** .NET 9 + WPF (`net9.0-windows7.0`, WinExe, elevated), C#, MVVM via CommunityToolkit.Mvvm, `System.Text.Json` (JSON), `System.Xml.Linq` (XML), hand-rolled RFC-4180 reader/writer (CSV), xUnit tests (`UseWPF=true`).

## Global Constraints

- **Target framework:** `net9.0-windows7.0`; `WinExe`; `Nullable` enabled; `ImplicitUsings` enabled; `LangVersion` latest. (verbatim from existing csproj)
- **No new NuGet dependencies.** Only `CommunityToolkit.Mvvm` (app) and the existing xUnit/coverlet stack (tests).
- **Solution file is `EZEditor.slnx`** (XML solution format, not `.sln`). Build: `dotnet build EZEditor.slnx`; Test: `dotnet test EZEditor.slnx`.
- **Root namespace is `EZEditor`** after Task 1. Type names that are format-specific keep their `Json`/`Xml`/`Csv` prefixes (e.g. `JsonNodeViewModel`, `XmlNodeViewModel`, `CsvDocument`).
- **JSON output:** 2-space indent, preserve object key order and exact number text (unchanged behavior).
- **All file/dialog/prompt access goes through interfaces** (`IFileDialogService`, `IUserPrompt`) so view-models are unit-testable with fakes. Keep parse/serialize/edit/filter logic free of WPF types (`System.Windows.*`) so it stays unit-testable; `System.Xml.Linq` and `System.Text.Json` are allowed in services/VMs.
- **The app runs elevated.** Before every rebuild, kill any running instance elevated: `Start-Process taskkill -ArgumentList '/F','/IM','EZEditor.exe' -Verb RunAs -Wait`. Launch with `Start-Process` (ShellExecute), never a bare exec.
- **Author credit "by yonka"** stays bottom-right of the window.
- **Never git commit/push without the user's explicit request** (standing rule). Steps that say "Commit" are gated on that rule — stage and report, ask before committing.

---

## Phase 1 — Rename JSONEditor → EZEditor

### Task 1: Rename project, solution, namespaces, and app text to EZEditor

This is an atomic rename: the namespace token change and the file/dir renames must land together for a buildable state. The literal token `JsonEditor` (no space) is replaced by `EZEditor` everywhere — this is safe because the kept type names are `JsonNodeViewModel` / `JsonDocumentService` / `JsonNodeKind` (`Json...`, never `JsonEditor...`). UI strings `"JSON Editor"` and the wordmark are handled as explicit edits.

**Files:**
- Rename (dir): `src/JsonEditor/` → `src/EZEditor/`; `tests/JsonEditor.Tests/` → `tests/EZEditor.Tests/`
- Rename (file): `src/EZEditor/JsonEditor.csproj` → `src/EZEditor/EZEditor.csproj`; `tests/EZEditor.Tests/JsonEditor.Tests.csproj` → `tests/EZEditor.Tests/EZEditor.Tests.csproj`; `JSONEditor.slnx` → `EZEditor.slnx`
- Modify (token `JsonEditor`→`EZEditor`): all `.cs`, `.xaml`, `.csproj`, `app.manifest` under `src/` and `tests/`
- Modify (UI text): `src/EZEditor/MainWindow.xaml`, `src/EZEditor/MainWindow.xaml.cs`, `src/EZEditor/Services/MessageBoxPrompt.cs`
- Modify (slnx paths): `EZEditor.slnx`

- [ ] **Step 1: Kill any running instance (elevated) so files unlock**

Run:
```powershell
Start-Process taskkill -ArgumentList '/F','/IM','JsonEditor.exe' -Verb RunAs -Wait
Start-Process taskkill -ArgumentList '/F','/IM','EZEditor.exe' -Verb RunAs -Wait
```
Expected: either "SUCCESS" or "not found" — both fine.

- [ ] **Step 2: Rename directories and project/solution files (preserve git history)**

Run (from repo root):
```bash
git mv src/JsonEditor src/EZEditor
git mv src/EZEditor/JsonEditor.csproj src/EZEditor/EZEditor.csproj
git mv tests/JsonEditor.Tests tests/EZEditor.Tests
git mv tests/EZEditor.Tests/JsonEditor.Tests.csproj tests/EZEditor.Tests/EZEditor.Tests.csproj
git mv JSONEditor.slnx EZEditor.slnx
```
Note: the untracked `src/EZEditor/Properties/` folder moves automatically with the directory rename. If `git mv` reports it is untracked and not moved, move it manually: `mv src/JsonEditor/Properties src/EZEditor/Properties` (only if the dir-level `git mv` left it behind).

- [ ] **Step 3: Replace the `JsonEditor` token with `EZEditor` across source**

Run (from repo root):
```powershell
Get-ChildItem -Recurse -File -Include *.cs,*.xaml,*.csproj,*.manifest src,tests |
  ForEach-Object {
    $c = Get-Content -Raw -LiteralPath $_.FullName
    if ($c -match 'JsonEditor') {
      ($c -creplace 'JsonEditor','EZEditor') | Set-Content -NoNewline -Encoding utf8 -LiteralPath $_.FullName
    }
  }
```
This updates: `RootNamespace`, every `namespace`/`using`, XAML `x:Class`, `clr-namespace:`, the `pack://application:,,,/EZEditor;component/icon.ico` URI, `app.manifest` `name="EZEditor.app"`, the test `ProjectReference` path, and the `JsonEditor.Tests` namespace. `-creplace` is case-sensitive so `JsonNodeViewModel`/`JsonDocumentService`/`JsonNodeKind` are untouched.

- [ ] **Step 4: Add `AssemblyName` so the output is `EZEditor.exe`**

In `src/EZEditor/EZEditor.csproj`, inside the first `<PropertyGroup>` add after `<RootNamespace>EZEditor</RootNamespace>`:
```xml
    <AssemblyName>EZEditor</AssemblyName>
```

- [ ] **Step 5: Fix solution project paths in `EZEditor.slnx`**

Replace the file contents with:
```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/EZEditor/EZEditor.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/EZEditor.Tests/EZEditor.Tests.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 6: Update the window title + toolbar wordmark**

In `src/EZEditor/MainWindow.xaml`:
- Change `Title="JSON Editor"` to `Title="EZEditor"`.
- Replace the wordmark `TextBlock` (the `<Run Text="JSON".../><Run Text=" Editor".../>`) with:
```xml
                    <TextBlock VerticalAlignment="Center" Margin="0,0,20,0" FontSize="20">
                        <Run Text="EZ" FontWeight="SemiBold" Foreground="#82AAFF"/><Run Text="Editor" Foreground="#E6E9F2"/>
                    </TextBlock>
```

- [ ] **Step 7: Update dialog titles to EZEditor**

In `src/EZEditor/Services/MessageBoxPrompt.cs` replace both `"JSON Editor"` literals with `"EZEditor"`.
In `src/EZEditor/MainWindow.xaml.cs` replace both `"JSON Editor"` literals (in `OnOpenExternally`) with `"EZEditor"`.

- [ ] **Step 8: Build the solution**

Run: `dotnet build EZEditor.slnx`
Expected: `Build succeeded`, 0 errors. (If an error mentions a stale `obj/` reference to the old name, run `dotnet clean EZEditor.slnx` then rebuild.)

- [ ] **Step 9: Run the full test suite**

Run: `dotnet test EZEditor.slnx`
Expected: all existing tests pass (the 105 baseline), 0 failed.

- [ ] **Step 10: Smoke-run the renamed app**

Run:
```powershell
Start-Process taskkill -ArgumentList '/F','/IM','EZEditor.exe' -Verb RunAs -Wait
dotnet build EZEditor.slnx
Start-Process "src/EZEditor/bin/Debug/net9.0-windows7.0/EZEditor.exe" -ArgumentList '"samples/sample.json"'
```
Expected: the window opens titled **EZEditor**, wordmark reads **EZEditor**, the JSON sample loads in the tree. Close it.

- [ ] **Step 11: Commit (gated on user request)**

```bash
git add -A
git commit -m "refactor: rename JSONEditor -> EZEditor (solution, project, namespaces, app text)"
```

---

## Phase 2 — Document abstraction (JSON still works end-to-end)

After this phase the app behaves exactly as before, but the shell talks only to `EditableDocument` and the editor is selected by a `ContentControl` template. All paths below use the post-rename `EZEditor` tree.

### Task 2: Add `DocumentFormat` enum and `EditableDocument` base

**Files:**
- Create: `src/EZEditor/ViewModels/EditableDocument.cs`
- Test: `tests/EZEditor.Tests/EditableDocumentTests.cs`

**Interfaces:**
- Produces: `enum EZEditor.ViewModels.DocumentFormat { Json, Xml, Csv }`
- Produces: `abstract class EZEditor.ViewModels.EditableDocument : ObservableObject` with `abstract DocumentFormat Format { get; }`, `event EventHandler? Changed`, `protected void OnChanged()`, `abstract string Serialize()`, `virtual void Save(string path)` (writes `Serialize()` as UTF-8 no BOM), `virtual void ApplyFilter(string? text)` (no-op default).

- [ ] **Step 1: Write the failing test**

```csharp
// tests/EZEditor.Tests/EditableDocumentTests.cs
using System.Text;
using EZEditor.ViewModels;

namespace EZEditor.Tests;

public class EditableDocumentTests
{
    private sealed class FakeDoc : EditableDocument
    {
        public string Text = "hello";
        public override DocumentFormat Format => DocumentFormat.Json;
        public override string Serialize() => Text;
        public void Edit(string t) { Text = t; OnChanged(); }
    }

    [Fact]
    public void OnChanged_RaisesChangedEvent()
    {
        var doc = new FakeDoc();
        var fired = 0;
        doc.Changed += (_, _) => fired++;
        doc.Edit("world");
        Assert.Equal(1, fired);
    }

    [Fact]
    public void Save_WritesSerializedTextAsUtf8NoBom()
    {
        var doc = new FakeDoc { Text = "abc" };
        var path = Path.Combine(Path.GetTempPath(), $"ed_{Guid.NewGuid():N}.txt");
        try
        {
            doc.Save(path);
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(new byte[] { 0x61, 0x62, 0x63 }, bytes); // no BOM
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test EZEditor.slnx --filter EditableDocumentTests`
Expected: FAIL — `EditableDocument`/`DocumentFormat` do not exist (compile error).

- [ ] **Step 3: Write the implementation**

```csharp
// src/EZEditor/ViewModels/EditableDocument.cs
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EZEditor.ViewModels;

public enum DocumentFormat { Json, Xml, Csv }

// Format-agnostic open document. The shell (MainViewModel) holds exactly one and
// never reaches into format-specific internals except through the typed subclasses.
public abstract class EditableDocument : ObservableObject
{
    public abstract DocumentFormat Format { get; }

    // Raised on any edit; the shell bridges this to its IsDirty flag.
    public event EventHandler? Changed;
    protected void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public abstract string Serialize();

    public virtual void Save(string path) =>
        File.WriteAllText(path, Serialize(), new UTF8Encoding(false));

    public virtual void ApplyFilter(string? text) { }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test EZEditor.slnx --filter EditableDocumentTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit (gated)**

```bash
git add src/EZEditor/ViewModels/EditableDocument.cs tests/EZEditor.Tests/EditableDocumentTests.cs
git commit -m "feat: add EditableDocument base + DocumentFormat enum"
```

### Task 3: Add `JsonDocument` wrapping the existing JSON tree

**Files:**
- Create: `src/EZEditor/ViewModels/JsonDocument.cs`
- Test: `tests/EZEditor.Tests/JsonDocumentTests.cs`

**Interfaces:**
- Consumes: `EditableDocument`, `JsonNodeViewModel`, `JsonDocumentService`.
- Produces: `sealed class EZEditor.ViewModels.JsonDocument : EditableDocument` with ctor `JsonDocument(JsonNodeViewModel root, JsonDocumentService svc)`, `ObservableCollection<JsonNodeViewModel> Roots { get; }` (single element, for `TreeView.ItemsSource`), `JsonNodeViewModel Root => Roots[0]`, `override Format => DocumentFormat.Json`, `override Serialize()` ⇒ `svc.Serialize(Root)`, `override ApplyFilter(text)` ⇒ `Root.ApplyFilter(text)`. Subscribes to `Root.Changed` → `OnChanged()`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/EZEditor.Tests/JsonDocumentTests.cs
using EZEditor.Models;
using EZEditor.Services;
using EZEditor.ViewModels;

namespace EZEditor.Tests;

public class JsonDocumentTests
{
    private static JsonDocument Make(string json)
    {
        var svc = new JsonDocumentService();
        return new JsonDocument(svc.Parse(json), svc);
    }

    [Fact]
    public void Roots_ContainsSingleRoot_AndFormatIsJson()
    {
        var doc = Make("""{ "a": 1 }""");
        Assert.Single(doc.Roots);
        Assert.Same(doc.Roots[0], doc.Root);
        Assert.Equal(DocumentFormat.Json, doc.Format);
    }

    [Fact]
    public void EditingTree_RaisesChanged()
    {
        var doc = Make("""{ "a": 1 }""");
        var fired = 0;
        doc.Changed += (_, _) => fired++;
        doc.Root.Children[0].Value = "2";
        Assert.True(fired >= 1);
    }

    [Fact]
    public void Serialize_RoundTripsValueEdit()
    {
        var doc = Make("""{ "a": 1 }""");
        doc.Root.Children[0].Value = "2";
        Assert.Contains("\"a\": 2", doc.Serialize());
    }

    [Fact]
    public void ApplyFilter_DelegatesToRoot()
    {
        var doc = Make("""{ "name": "Alice", "age": 30 }""");
        doc.ApplyFilter("age");
        Assert.True(doc.Root.Children.First(c => c.Name == "name").IsFilteredOut);
        Assert.False(doc.Root.Children.First(c => c.Name == "age").IsFilteredOut);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test EZEditor.slnx --filter JsonDocumentTests`
Expected: FAIL — `JsonDocument` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
// src/EZEditor/ViewModels/JsonDocument.cs
using System.Collections.ObjectModel;
using EZEditor.Services;

namespace EZEditor.ViewModels;

public sealed class JsonDocument : EditableDocument
{
    private readonly JsonDocumentService _svc;

    public JsonDocument(JsonNodeViewModel root, JsonDocumentService svc)
    {
        _svc = svc;
        Roots = new ObservableCollection<JsonNodeViewModel> { root };
        root.Changed += (_, _) => OnChanged();
    }

    // Single-element collection so the JSON TreeView can bind ItemsSource directly.
    public ObservableCollection<JsonNodeViewModel> Roots { get; }
    public JsonNodeViewModel Root => Roots[0];

    public override DocumentFormat Format => DocumentFormat.Json;
    public override string Serialize() => _svc.Serialize(Root);
    public override void Save(string path) => _svc.Save(Root, path);
    public override void ApplyFilter(string? text) => Root.ApplyFilter(text);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test EZEditor.slnx --filter JsonDocumentTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit (gated)**

```bash
git add src/EZEditor/ViewModels/JsonDocument.cs tests/EZEditor.Tests/JsonDocumentTests.cs
git commit -m "feat: add JsonDocument wrapping the JSON tree"
```

### Task 4: Add `DocumentFactory` with content detection (JSON path only for now)

**Files:**
- Create: `src/EZEditor/Services/DocumentFactory.cs`
- Test: `tests/EZEditor.Tests/DocumentFactoryTests.cs`

**Interfaces:**
- Consumes: `JsonDocumentService`, `JsonDocument`, `DocumentFormat`, `EditableDocument`.
- Produces: `sealed class EZEditor.Services.DocumentFactory` with `EditableDocument LoadAuto(string path)`, `EditableDocument Create(DocumentFormat fmt, string text)`, and `static DocumentFormat Detect(string text, string? ext = null)`. CSV/XML branches in `Create` throw `NotSupportedException` until Tasks 9/13 wire them; `Detect` already classifies all three so later tasks only add `Create` branches.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/EZEditor.Tests/DocumentFactoryTests.cs
using EZEditor.Services;
using EZEditor.ViewModels;

namespace EZEditor.Tests;

public class DocumentFactoryTests
{
    [Theory]
    [InlineData("<root><a>1</a></root>", DocumentFormat.Xml)]
    [InlineData("  \n<?xml version=\"1.0\"?><r/>", DocumentFormat.Xml)]
    [InlineData("{ \"a\": 1 }", DocumentFormat.Json)]
    [InlineData("[1, 2, 3]", DocumentFormat.Json)]
    [InlineData("name,age\nAlice,30", DocumentFormat.Csv)]
    public void Detect_ClassifiesByContent(string text, DocumentFormat expected)
        => Assert.Equal(expected, DocumentFactory.Detect(text));

    [Theory]
    [InlineData("", ".json", DocumentFormat.Json)]
    [InlineData("", ".xml", DocumentFormat.Xml)]
    [InlineData("", ".csv", DocumentFormat.Csv)]
    public void Detect_EmptyContent_FallsBackToExtension(string text, string ext, DocumentFormat expected)
        => Assert.Equal(expected, DocumentFactory.Detect(text, ext));

    [Fact]
    public void LoadAuto_JsonFile_ReturnsJsonDocument()
    {
        var path = Path.Combine(Path.GetTempPath(), $"df_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "a": 1 }""");
        try
        {
            var doc = new DocumentFactory().LoadAuto(path);
            Assert.IsType<JsonDocument>(doc);
        }
        finally { File.Delete(path); }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test EZEditor.slnx --filter DocumentFactoryTests`
Expected: FAIL — `DocumentFactory` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
// src/EZEditor/Services/DocumentFactory.cs
using System.IO;
using System.Text.Json;
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
        _ => new JsonDocument(_json.Parse(text), _json),
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
            using var _ = System.Text.Json.JsonDocument.Parse(t, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            return true;
        }
        catch (JsonException) { return false; }
    }
}
```
Note: `System.Text.Json.JsonDocument` is fully qualified to avoid colliding with `EZEditor.ViewModels.JsonDocument`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test EZEditor.slnx --filter DocumentFactoryTests`
Expected: PASS (9 cases).

- [ ] **Step 5: Commit (gated)**

```bash
git add src/EZEditor/Services/DocumentFactory.cs tests/EZEditor.Tests/DocumentFactoryTests.cs
git commit -m "feat: add DocumentFactory with content detection (JSON wired)"
```

### Task 5: Refactor `MainViewModel` onto `EditableDocument` + `DocumentFactory`

The shell now holds `CurrentDocument` instead of `Roots`, takes a `DocumentFactory`, and exposes `JsonRoot` for the JSON-only commands. `StatusText` gains the format tag. Existing `MainViewModelTests` are updated to the new surface.

**Files:**
- Modify: `src/EZEditor/ViewModels/MainViewModel.cs` (full rewrite)
- Modify: `src/EZEditor/App.xaml.cs:14` (construct with `DocumentFactory`)
- Modify: `tests/EZEditor.Tests/MainViewModelTests.cs` (full rewrite)

**Interfaces:**
- Consumes: `DocumentFactory`, `EditableDocument`, `JsonDocument`, `JsonNodeViewModel`, `IFileDialogService`, `IUserPrompt`.
- Produces: `MainViewModel(DocumentFactory factory, IFileDialogService dialogs, IUserPrompt prompt)`; props `EditableDocument? CurrentDocument`, `string? CurrentPath`, `bool IsDirty`, `string? FilterText`, `string StatusText`, `JsonNodeViewModel? JsonRoot`; methods `OpenPath(string)`, `ConfirmDiscardIfDirty()`; commands `Open/Save/SaveAs/Reload/AddChild/DeleteNode/MakeString/MakeNumber/MakeBoolean/MakeNull/MakeObject/MakeArray/ExpandAll/CollapseAll`.

- [ ] **Step 1: Rewrite the test file to the new surface (failing)**

```csharp
// tests/EZEditor.Tests/MainViewModelTests.cs
using EZEditor.Models;
using EZEditor.Services;
using EZEditor.ViewModels;

namespace EZEditor.Tests;

public class MainViewModelTests
{
    private sealed class FakeDialogs : IFileDialogService
    {
        public string? OpenPath; public string? SavePath;
        public string? OpenFile() => OpenPath;
        public string? SaveFile(string? suggestedName) => SavePath;
    }

    private sealed class FakePrompt : IUserPrompt
    {
        public string? LastError; public PromptResult Discard = PromptResult.Yes;
        public void Error(string message) => LastError = message;
        public PromptResult ConfirmDiscard() => Discard;
    }

    private static MainViewModel Make(out FakeDialogs d, out FakePrompt p)
    {
        d = new FakeDialogs();
        p = new FakePrompt();
        return new MainViewModel(new DocumentFactory(), d, p);
    }

    [Fact]
    public void OpenPath_LoadsJsonDocument_AndIsNotDirty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mvm_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "a": 1 }""");
        try
        {
            var vm = Make(out _, out _);
            vm.OpenPath(path);
            Assert.IsType<JsonDocument>(vm.CurrentDocument);
            Assert.NotNull(vm.JsonRoot);
            Assert.Equal(JsonNodeKind.Object, vm.JsonRoot!.Kind);
            Assert.False(vm.IsDirty);
            Assert.Equal(path, vm.CurrentPath);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void EditingValue_SetsDirty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mvm_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "a": 1 }""");
        try
        {
            var vm = Make(out _, out _);
            vm.OpenPath(path);
            vm.JsonRoot!.Children[0].Value = "2";
            Assert.True(vm.IsDirty);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Open_InvalidJson_ShowsError_AndKeepsNoDocument()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mvm_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ broken ");
        try
        {
            var vm = Make(out var d, out var p);
            d.OpenPath = path;
            vm.OpenCommand.Execute(null);
            Assert.NotNull(p.LastError);
            Assert.Null(vm.CurrentDocument);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SaveAs_WritesToChosenPath_AndClearsDirty()
    {
        var src = Path.Combine(Path.GetTempPath(), $"src_{Guid.NewGuid():N}.json");
        var dest = Path.Combine(Path.GetTempPath(), $"dest_{Guid.NewGuid():N}.json");
        File.WriteAllText(src, """{ "a": 1 }""");
        try
        {
            var vm = Make(out var d, out _);
            vm.OpenPath(src);
            vm.JsonRoot!.Children[0].Value = "2";
            Assert.True(vm.IsDirty);

            d.SavePath = dest;
            vm.SaveAsCommand.Execute(null);

            Assert.True(File.Exists(dest));
            Assert.False(vm.IsDirty);
            Assert.Equal(dest, vm.CurrentPath);
            Assert.Contains("\"a\": 2", File.ReadAllText(dest));
        }
        finally
        {
            if (File.Exists(src)) File.Delete(src);
            if (File.Exists(dest)) File.Delete(dest);
        }
    }

    [Fact]
    public void Reload_DiscardsEdits_AndReloadsFromDisk()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mvm_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "a": 1 }""");
        try
        {
            var vm = Make(out _, out _);
            vm.OpenPath(path);
            vm.JsonRoot!.Children[0].Value = "999";
            Assert.True(vm.IsDirty);

            vm.ReloadCommand.Execute(null);

            Assert.False(vm.IsDirty);
            Assert.Equal("1", vm.JsonRoot!.Children[0].Value);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void CollapseAll_CollapsesNestedButKeepsRootExpanded()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mvm_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "user": { "name": "Alice" } }""");
        try
        {
            var vm = Make(out _, out _);
            vm.OpenPath(path);
            vm.ExpandAllCommand.Execute(null);
            var user = vm.JsonRoot!.Children[0];
            Assert.True(user.IsExpanded);

            vm.CollapseAllCommand.Execute(null);
            Assert.True(vm.JsonRoot!.IsExpanded);
            Assert.False(user.IsExpanded);
            Assert.False(vm.IsDirty);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void FilterText_AppliesFilterToDocument()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mvm_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "name": "Alice", "age": 30 }""");
        try
        {
            var vm = Make(out _, out _);
            vm.OpenPath(path);
            vm.FilterText = "age";

            var name = vm.JsonRoot!.Children.First(c => c.Name == "name");
            var age = vm.JsonRoot!.Children.First(c => c.Name == "age");
            Assert.True(name.IsFilteredOut);
            Assert.False(age.IsFilteredOut);
            Assert.False(vm.IsDirty);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void StatusText_IncludesFormatTag_WhenDocumentOpen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mvm_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "a": 1 }""");
        try
        {
            var vm = Make(out _, out _);
            vm.OpenPath(path);
            Assert.Contains("[JSON]", vm.StatusText);
        }
        finally { File.Delete(path); }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test EZEditor.slnx --filter MainViewModelTests`
Expected: FAIL — `MainViewModel(DocumentFactory,…)`, `CurrentDocument`, `JsonRoot` do not exist.

- [ ] **Step 3: Rewrite `MainViewModel`**

```csharp
// src/EZEditor/ViewModels/MainViewModel.cs
using System.IO;
using System.Text.Json;
using System.Xml;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EZEditor.Models;
using EZEditor.Services;

namespace EZEditor.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DocumentFactory _factory;
    private readonly IFileDialogService _dialogs;
    private readonly IUserPrompt _prompt;

    public MainViewModel(DocumentFactory factory, IFileDialogService dialogs, IUserPrompt prompt)
    {
        _factory = factory;
        _dialogs = dialogs;
        _prompt = prompt;
    }

    [ObservableProperty] private EditableDocument? _currentDocument;
    [ObservableProperty] private string? _currentPath;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string? _filterText;

    // JSON-only commands operate on this; null when the open document isn't JSON.
    public JsonNodeViewModel? JsonRoot => (CurrentDocument as JsonDocument)?.Root;

    public string StatusText =>
        (CurrentPath ?? "No file")
        + (CurrentDocument is not null ? $"  [{CurrentDocument.Format.ToString().ToUpperInvariant()}]" : string.Empty)
        + (IsDirty ? "  ●" : string.Empty);

    partial void OnCurrentDocumentChanged(EditableDocument? oldValue, EditableDocument? newValue)
    {
        if (oldValue is not null) oldValue.Changed -= OnDocChanged;
        if (newValue is not null) newValue.Changed += OnDocChanged;
        OnPropertyChanged(nameof(JsonRoot));
        OnPropertyChanged(nameof(StatusText));
    }

    partial void OnCurrentPathChanged(string? value) => OnPropertyChanged(nameof(StatusText));
    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(StatusText));
    partial void OnFilterTextChanged(string? value) => CurrentDocument?.ApplyFilter(value);

    private void OnDocChanged(object? sender, EventArgs e) => IsDirty = true;

    public void OpenPath(string path)
    {
        var doc = _factory.LoadAuto(path);
        FilterText = null;
        CurrentDocument = doc;
        CurrentPath = path;
        IsDirty = false;
    }

    public bool ConfirmDiscardIfDirty()
    {
        if (!IsDirty) return true;
        return _prompt.ConfirmDiscard() switch { PromptResult.Yes => true, _ => false };
    }

    [RelayCommand]
    private void Open()
    {
        if (!ConfirmDiscardIfDirty()) return;
        var path = _dialogs.OpenFile();
        if (path is null) return;
        TryLoad(path, "open");
    }

    private void TryLoad(string path, string verb)
    {
        try { OpenPath(path); }
        catch (JsonException ex) { _prompt.Error($"Could not parse JSON:\n{ex.Message}"); }
        catch (XmlException ex) { _prompt.Error($"Could not parse XML:\n{ex.Message}"); }
        catch (FormatException ex) { _prompt.Error($"Could not parse file:\n{ex.Message}"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { _prompt.Error($"Could not {verb} file:\n{ex.Message}"); }
    }

    [RelayCommand]
    private void Save()
    {
        if (CurrentDocument is null) return;
        if (CurrentPath is null) { SaveAs(); return; }
        Persist(CurrentPath);
    }

    [RelayCommand]
    private void SaveAs()
    {
        if (CurrentDocument is null) return;
        var path = _dialogs.SaveFile(CurrentPath);
        if (path is null) return;
        Persist(path);
    }

    private void Persist(string path)
    {
        try
        {
            CurrentDocument!.Save(path);
            CurrentPath = path;
            IsDirty = false;
        }
        catch (Exception ex) when (ex is IOException or JsonException or XmlException
                                     or UnauthorizedAccessException or InvalidOperationException)
        { _prompt.Error($"Could not save file:\n{ex.Message}"); }
    }

    [RelayCommand]
    private void Reload()
    {
        if (CurrentPath is null) return;
        if (!ConfirmDiscardIfDirty()) return;
        TryLoad(CurrentPath, "reload");
    }

    // ---- JSON-only edit commands (no-op unless the JSON document is active) ----
    [RelayCommand]
    private void AddChild(JsonNodeViewModel? node)
    {
        if (node is { IsContainer: true }) node.AddChild(JsonNodeKind.String);
    }

    [RelayCommand]
    private void DeleteNode(JsonNodeViewModel? node)
    {
        if (node is not null && node.Parent is not null) node.Delete();
    }

    [RelayCommand] private void ExpandAll() => JsonRoot?.SetExpandedRecursive(true);

    [RelayCommand]
    private void CollapseAll()
    {
        if (JsonRoot is null) return;
        JsonRoot.SetExpandedRecursive(false);
        JsonRoot.IsExpanded = true;
    }

    [RelayCommand] private void MakeString(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.String);
    [RelayCommand] private void MakeNumber(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.Number);
    [RelayCommand] private void MakeBoolean(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.Boolean);
    [RelayCommand] private void MakeNull(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.Null);
    [RelayCommand] private void MakeObject(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.Object);
    [RelayCommand] private void MakeArray(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.Array);
}
```

- [ ] **Step 4: Update `App.xaml.cs` to construct with the factory**

Replace line 14 (`var vm = new MainViewModel(new JsonDocumentService(), …)`) with:
```csharp
        var vm = new MainViewModel(new DocumentFactory(), new FileDialogService(), new MessageBoxPrompt());
```

- [ ] **Step 4b: Update `ViewSmokeTests.cs` to the new MainViewModel surface**

`tests/EZEditor.Tests/ViewSmokeTests.cs` constructs `MainViewModel` with a `JsonDocumentService` and asserts `vm.Roots` — both gone after the refactor. Change the construction line to:
```csharp
                var vm = new MainViewModel(new DocumentFactory(), new NoDialogs(), new NoPrompt());
```
and replace `Assert.Single(vm.Roots);` with:
```csharp
                Assert.IsType<JsonDocument>(vm.CurrentDocument);
```
(`using EZEditor.Services;` is already present for `DocumentFactory`.)

- [ ] **Step 5: Run the full suite**

Run: `dotnet test EZEditor.slnx`
Expected: PASS — all rewritten `MainViewModelTests` plus prior tests green. (The app will not yet *display* a document until Task 6; this step verifies VM logic only.)

- [ ] **Step 6: Commit (gated)**

```bash
git add src/EZEditor/ViewModels/MainViewModel.cs src/EZEditor/App.xaml.cs tests/EZEditor.Tests/MainViewModelTests.cs
git commit -m "refactor: MainViewModel holds EditableDocument via DocumentFactory"
```

### Task 6: Swap `MainWindow` to a `ContentControl` + JSON `DataTemplate`

The hard-wired `TreeView` moves into a `DataTemplate` keyed to `JsonDocument`; a `ContentControl` bound to `CurrentDocument` selects it. This is verified by build + smoke run (not unit TDD).

**Files:**
- Modify: `src/EZEditor/MainWindow.xaml` (move TreeView into a DataTemplate; add ContentControl; rebind toolbar filter unchanged)

**Interfaces:**
- Consumes: `MainViewModel.CurrentDocument`, `JsonDocument.Roots`, `MainViewModel.*Command`, `JsonNodeViewModel` (existing `HierarchicalDataTemplate`).

- [ ] **Step 1: Add the document namespace + JSON editor template to `Window.Resources`**

In `MainWindow.xaml`, the existing `HierarchicalDataTemplate DataType="{x:Type vm:JsonNodeViewModel}"` stays as-is. After it (still inside `<Window.Resources>`), add a `DataTemplate` for the JSON document that hosts the current `TreeView` markup. Cut the entire `<Border Margin="14" …><TreeView …>…</TreeView></Border>` block out of the `DockPanel` and paste it inside this new template, changing `ItemsSource="{Binding Roots}"` to `ItemsSource="{Binding Roots}"` (now relative to `JsonDocument`, same property name) and updating the TreeView-level context menu binding (see Step 2):

```xml
        <DataTemplate DataType="{x:Type vm:JsonDocument}">
            <Border Margin="14" CornerRadius="10" Background="{StaticResource Surface}"
                    BorderBrush="{StaticResource Hairline}" BorderThickness="1">
                <TreeView x:Name="Tree" ItemsSource="{Binding Roots}"
                          ItemContainerStyle="{StaticResource JsonTreeItem}"
                          Padding="10,8" Margin="2"
                          Tag="{Binding DataContext, RelativeSource={RelativeSource AncestorType=Window}}"
                          PreviewMouseWheel="OnTreePreviewMouseWheel"
                          ScrollViewer.CanContentScroll="True"
                          VirtualizingPanel.IsVirtualizing="True"
                          VirtualizingPanel.VirtualizationMode="Recycling"
                          VirtualizingPanel.ScrollUnit="Item">
                    <TreeView.Resources>
                        <SolidColorBrush x:Key="{x:Static SystemColors.ControlBrushKey}" Color="#161922"/>
                    </TreeView.Resources>
                    <TreeView.ContextMenu>
                        <ContextMenu>
                            <MenuItem Header="Expand all"
                                      Command="{Binding PlacementTarget.Tag.ExpandAllCommand, RelativeSource={RelativeSource AncestorType=ContextMenu}}"/>
                            <MenuItem Header="Collapse all"
                                      Command="{Binding PlacementTarget.Tag.CollapseAllCommand, RelativeSource={RelativeSource AncestorType=ContextMenu}}"/>
                        </ContextMenu>
                    </TreeView.ContextMenu>
                </TreeView>
            </Border>
        </DataTemplate>
```
Rationale: the TreeView's `DataContext` is now the `JsonDocument`, so the empty-space context menu can no longer reach `MainViewModel` via `PlacementTarget.DataContext`. Binding `Tag` to the Window's `DataContext` (the `MainViewModel`) and using `PlacementTarget.Tag.…` restores it without the `{x:Reference Root}` cyclical-dependency error noted in CLAUDE.md. The per-node menu still uses `{x:Reference Root}` (the Window) and is unaffected.

- [ ] **Step 2: Replace the removed Border with a ContentControl in the DockPanel**

Where the `<Border Margin="14">…TreeView…</Border>` used to be (the last child of the outer `DockPanel`, filling the remaining space), put:
```xml
        <ContentControl Content="{Binding CurrentDocument}" />
```

- [ ] **Step 2b: Generalize `OnTreePreviewMouseWheel` (the `Tree` field is gone)**

Moving the `TreeView` into a `DataTemplate` removes the window-level `x:Name="Tree"` field (DataTemplate names live in a separate namescope), so the existing handler's `FindScrollViewer(Tree)` no longer compiles. In `src/EZEditor/MainWindow.xaml.cs` replace the body of `OnTreePreviewMouseWheel` to resolve the scroll viewer from the event sender instead:
```csharp
    private void OnTreePreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Shift) return;
        var sv = FindScrollViewer((DependencyObject)sender);
        if (sv is null) return;
        sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta);
        e.Handled = true;
    }
```
This also serves the XML `TreeView` added in Task 14.

- [ ] **Step 3: Build**

Run:
```powershell
Start-Process taskkill -ArgumentList '/F','/IM','EZEditor.exe' -Verb RunAs -Wait
dotnet build EZEditor.slnx
```
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 4: Smoke-run with the JSON sample**

Run:
```powershell
Start-Process "src/EZEditor/bin/Debug/net9.0-windows7.0/EZEditor.exe" -ArgumentList '"samples/sample.json"'
```
Expected: the JSON tree renders exactly as before; expand/collapse via right-click on a node AND on empty tree space both work; editing a value flips the status bar to show `[JSON]  ●`. Close the window.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test EZEditor.slnx`
Expected: all tests pass.

- [ ] **Step 6: Commit (gated)**

```bash
git add src/EZEditor/MainWindow.xaml
git commit -m "refactor: select editor via ContentControl + JsonDocument DataTemplate"
```

---

## Phase 3 — CSV (spreadsheet grid)

### Task 7: `CsvDocumentService` — RFC-4180 parse/serialize + delimiter detection

**Files:**
- Create: `src/EZEditor/Services/CsvDocumentService.cs`
- Test: `tests/EZEditor.Tests/CsvDocumentServiceTests.cs`

**Interfaces:**
- Produces: `sealed class EZEditor.Services.CsvDocumentService` with:
  - `static char DetectDelimiter(string text)` — returns `,`, `;`, or `\t` (most frequent on the first non-empty line; default `,`).
  - `List<List<string>> ParseRows(string text, char delimiter)` — RFC-4180: quoted fields, `""` escape, embedded delimiter/newline inside quotes. Trailing final newline does not add an empty row.
  - `string Serialize(IReadOnlyList<string> header, IReadOnlyList<IReadOnlyList<string>> rows, char delimiter, bool hasHeader)` — quotes a field iff it contains the delimiter, `"`, `\r`, or `\n`; joins rows with `\r\n`; emits the header row first when `hasHeader`.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/EZEditor.Tests/CsvDocumentServiceTests.cs
using EZEditor.Services;

namespace EZEditor.Tests;

public class CsvDocumentServiceTests
{
    private readonly CsvDocumentService _svc = new();

    [Fact]
    public void ParseRows_SimpleGrid()
    {
        var rows = _svc.ParseRows("name,age\nAlice,30\nBob,25", ',');
        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { "name", "age" }, rows[0].ToArray());
        Assert.Equal(new[] { "Bob", "25" }, rows[2].ToArray());
    }

    [Fact]
    public void ParseRows_QuotedFieldWithCommaAndQuote()
    {
        // field: She said "hi", and left   -> "She said ""hi"", and left"
        var rows = _svc.ParseRows("a,b\n\"She said \"\"hi\"\", and left\",2", ',');
        Assert.Equal("She said \"hi\", and left", rows[1][0]);
        Assert.Equal("2", rows[1][1]);
    }

    [Fact]
    public void ParseRows_QuotedFieldWithEmbeddedNewline()
    {
        var rows = _svc.ParseRows("a\n\"line1\nline2\"", ',');
        Assert.Equal(2, rows.Count);
        Assert.Equal("line1\nline2", rows[1][0]);
    }

    [Fact]
    public void ParseRows_IgnoresTrailingNewline()
    {
        var rows = _svc.ParseRows("a,b\n1,2\n", ',');
        Assert.Equal(2, rows.Count);
    }

    [Theory]
    [InlineData("a;b;c\n1;2;3", ';')]
    [InlineData("a\tb\n1\t2", '\t')]
    [InlineData("a,b\n1,2", ',')]
    public void DetectDelimiter_PicksMostFrequentOnFirstLine(string text, char expected)
        => Assert.Equal(expected, CsvDocumentService.DetectDelimiter(text));

    [Fact]
    public void Serialize_QuotesOnlyWhenNeeded_AndRoundTrips()
    {
        var header = new[] { "name", "note" };
        var rows = new IReadOnlyList<string>[]
        {
            new[] { "Alice", "plain" },
            new[] { "Bob", "has,comma" },
            new[] { "Cara", "has \"quote\"" },
        };
        var text = _svc.Serialize(header, rows, ',', hasHeader: true);
        Assert.Equal(
            "name,note\r\nAlice,plain\r\nBob,\"has,comma\"\r\nCara,\"has \"\"quote\"\"\"",
            text);

        var reparsed = _svc.ParseRows(text, ',');
        Assert.Equal("has,comma", reparsed[2][1]);
        Assert.Equal("has \"quote\"", reparsed[3][1]);
    }

    [Fact]
    public void Serialize_NoHeader_OmitsHeaderRow()
    {
        var rows = new IReadOnlyList<string>[] { new[] { "1", "2" } };
        var text = _svc.Serialize(new[] { "a", "b" }, rows, ',', hasHeader: false);
        Assert.Equal("1,2", text);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test EZEditor.slnx --filter CsvDocumentServiceTests`
Expected: FAIL — `CsvDocumentService` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
// src/EZEditor/Services/CsvDocumentService.cs
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test EZEditor.slnx --filter CsvDocumentServiceTests`
Expected: PASS (all cases).

- [ ] **Step 5: Commit (gated)**

```bash
git add src/EZEditor/Services/CsvDocumentService.cs tests/EZEditor.Tests/CsvDocumentServiceTests.cs
git commit -m "feat: add CsvDocumentService (RFC-4180 parse/serialize + delimiter detect)"
```

### Task 8: `CsvRow` + `CsvDocument` model and edit operations

**Files:**
- Create: `src/EZEditor/ViewModels/CsvRow.cs`
- Create: `src/EZEditor/ViewModels/CsvDocument.cs`
- Test: `tests/EZEditor.Tests/CsvDocumentTests.cs`

**Interfaces:**
- Produces: `sealed class EZEditor.ViewModels.CsvRow : ObservableObject` — ctor `CsvRow(IEnumerable<string> cells)`; `string this[int index]` (get returns `""` past end; set grows the row and raises `PropertyChanged("Item[]")`); `IReadOnlyList<string> Cells`; `int Count`; `internal void AddCell(string = "")`; `internal void RemoveCellAt(int)`.
- Produces: `sealed class EZEditor.ViewModels.CsvDocument : EditableDocument` — ctor `CsvDocument(CsvParseResult parsed, CsvDocumentService svc)`; `ObservableCollection<string> Columns`; `ObservableCollection<CsvRow> Rows`; `char Delimiter`; `bool HasHeader`; `event EventHandler? ColumnsChanged`; methods `AddRow()`, `DeleteRow(CsvRow)`, `AddColumn(string name)`, `DeleteColumn(int index)`, `RenameColumn(int index, string name)`; `override Format => DocumentFormat.Csv`; `override Serialize()` ⇒ `svc.Serialize(Columns, Rows-as-cells, Delimiter, HasHeader)`.
- Produces: `sealed record EZEditor.Services.CsvParseResult(List<string> Columns, List<CsvRow> Rows, char Delimiter, bool HasHeader)` and `CsvDocumentService.Parse(string text, bool hasHeader = true)` returning it. (Add `Parse` to the service from Task 7.)

- [ ] **Step 1: Add `CsvParseResult` + `CsvDocumentService.Parse` (so the model has a loader)**

Append to `src/EZEditor/Services/CsvDocumentService.cs` (inside the namespace, after the class or as a new file `CsvParseResult.cs` — keep it next to the service):

```csharp
// src/EZEditor/Services/CsvParseResult.cs
using EZEditor.ViewModels;

namespace EZEditor.Services;

public sealed record CsvParseResult(
    List<string> Columns,
    List<CsvRow> Rows,
    char Delimiter,
    bool HasHeader);
```

Add this method inside `CsvDocumentService`:
```csharp
    // Builds columns + CsvRow list. With hasHeader, row 0 supplies column names and
    // is removed from the data rows; otherwise columns are named "Column1..N".
    public CsvParseResult Parse(string text, bool hasHeader = true)
    {
        var delimiter = DetectDelimiter(text);
        var raw = ParseRows(text, delimiter);
        var width = raw.Count == 0 ? 0 : raw.Max(r => r.Count);

        List<string> columns;
        IEnumerable<List<string>> dataRows;
        if (hasHeader && raw.Count > 0)
        {
            columns = Enumerable.Range(0, width)
                .Select(i => i < raw[0].Count && raw[0][i].Length > 0 ? raw[0][i] : $"Column{i + 1}")
                .ToList();
            dataRows = raw.Skip(1);
        }
        else
        {
            columns = Enumerable.Range(0, width).Select(i => $"Column{i + 1}").ToList();
            dataRows = raw;
        }

        var rows = dataRows.Select(r => new EZEditor.ViewModels.CsvRow(r)).ToList();
        return new CsvParseResult(columns, rows, delimiter, hasHeader);
    }
```

- [ ] **Step 2: Write the failing tests**

```csharp
// tests/EZEditor.Tests/CsvDocumentTests.cs
using System.ComponentModel;
using EZEditor.Services;
using EZEditor.ViewModels;

namespace EZEditor.Tests;

public class CsvDocumentTests
{
    private static CsvDocument Make(string text, bool hasHeader = true)
    {
        var svc = new CsvDocumentService();
        return new CsvDocument(svc.Parse(text, hasHeader), svc);
    }

    [Fact]
    public void Parse_HeaderRow_BecomesColumns_NotData()
    {
        var doc = Make("name,age\nAlice,30");
        Assert.Equal(new[] { "name", "age" }, doc.Columns.ToArray());
        Assert.Single(doc.Rows);
        Assert.Equal("Alice", doc.Rows[0][0]);
        Assert.Equal(DocumentFormat.Csv, doc.Format);
    }

    [Fact]
    public void CsvRow_Indexer_GrowsAndNotifies()
    {
        var row = new CsvRow(new[] { "a" });
        string? prop = null;
        ((INotifyPropertyChanged)row).PropertyChanged += (_, e) => prop = e.PropertyName;
        row[3] = "x";
        Assert.Equal("Item[]", prop);
        Assert.Equal("x", row[3]);
        Assert.Equal("", row[2]); // grown with blanks
    }

    [Fact]
    public void EditingCell_RaisesChanged()
    {
        var doc = Make("a,b\n1,2");
        var fired = 0;
        doc.Changed += (_, _) => fired++;
        doc.Rows[0][0] = "99";
        Assert.True(fired >= 1);
    }

    [Fact]
    public void AddRow_AddsBlankRow_WithColumnWidth_AndRaisesChanged()
    {
        var doc = Make("a,b\n1,2");
        var fired = 0;
        doc.Changed += (_, _) => fired++;
        doc.AddRow();
        Assert.Equal(2, doc.Rows.Count);
        Assert.Equal("", doc.Rows[1][0]);
        Assert.True(fired >= 1);
    }

    [Fact]
    public void AddColumn_AppendsHeader_AndRaisesColumnsChanged()
    {
        var doc = Make("a,b\n1,2");
        var colsChanged = 0;
        doc.ColumnsChanged += (_, _) => colsChanged++;
        doc.AddColumn("c");
        Assert.Equal(new[] { "a", "b", "c" }, doc.Columns.ToArray());
        Assert.True(colsChanged >= 1);
    }

    [Fact]
    public void DeleteColumn_RemovesHeaderAndCells()
    {
        var doc = Make("a,b,c\n1,2,3");
        doc.DeleteColumn(1);
        Assert.Equal(new[] { "a", "c" }, doc.Columns.ToArray());
        Assert.Equal("1", doc.Rows[0][0]);
        Assert.Equal("3", doc.Rows[0][1]);
    }

    [Fact]
    public void Serialize_RoundTripsThroughModel()
    {
        var doc = Make("name,note\nAlice,\"has,comma\"");
        var text = doc.Serialize();
        Assert.Equal("name,note\r\nAlice,\"has,comma\"", text);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test EZEditor.slnx --filter "CsvDocumentTests|CsvRow"`
Expected: FAIL — `CsvRow`/`CsvDocument` do not exist.

- [ ] **Step 4: Write `CsvRow`**

```csharp
// src/EZEditor/ViewModels/CsvRow.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace EZEditor.ViewModels;

public sealed class CsvRow : ObservableObject
{
    private readonly List<string> _cells;

    public CsvRow(IEnumerable<string> cells) => _cells = cells.ToList();

    public IReadOnlyList<string> Cells => _cells;
    public int Count => _cells.Count;

    // "Item[]" is WPF's indexer-changed sentinel; kept as a literal so this VM stays WPF-free.
    public string this[int index]
    {
        get => index >= 0 && index < _cells.Count ? _cells[index] : string.Empty;
        set
        {
            while (_cells.Count <= index) _cells.Add(string.Empty);
            if (_cells[index] == value) return;
            _cells[index] = value;
            OnPropertyChanged("Item[]");
        }
    }

    internal void AddCell(string value = "") { _cells.Add(value); OnPropertyChanged("Item[]"); }
    internal void RemoveCellAt(int index)
    {
        if (index >= 0 && index < _cells.Count) _cells.RemoveAt(index);
        OnPropertyChanged("Item[]");
    }
}
```

- [ ] **Step 5: Write `CsvDocument`**

```csharp
// src/EZEditor/ViewModels/CsvDocument.cs
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using EZEditor.Services;

namespace EZEditor.ViewModels;

public sealed class CsvDocument : EditableDocument
{
    private readonly CsvDocumentService _svc;

    public CsvDocument(CsvParseResult parsed, CsvDocumentService svc)
    {
        _svc = svc;
        Delimiter = parsed.Delimiter;
        HasHeader = parsed.HasHeader;
        Columns = new ObservableCollection<string>(parsed.Columns);
        Rows = new ObservableCollection<CsvRow>(parsed.Rows);

        foreach (var r in Rows) r.PropertyChanged += OnRowChanged;
        Rows.CollectionChanged += OnRowsChanged;
    }

    public ObservableCollection<string> Columns { get; }
    public ObservableCollection<CsvRow> Rows { get; }
    public char Delimiter { get; set; }
    public bool HasHeader { get; set; }

    // Raised when the column set changes so the DataGrid can rebuild its columns.
    public event EventHandler? ColumnsChanged;

    public override DocumentFormat Format => DocumentFormat.Csv;

    public override string Serialize()
    {
        var rows = Rows.Select(r => (IReadOnlyList<string>)Enumerable
            .Range(0, Columns.Count).Select(i => r[i]).ToList()).ToList();
        return _svc.Serialize(Columns, rows, Delimiter, HasHeader);
    }

    public void AddRow()
    {
        var row = new CsvRow(Enumerable.Repeat(string.Empty, Columns.Count));
        Rows.Add(row);
    }

    public void DeleteRow(CsvRow row) => Rows.Remove(row);

    public void AddColumn(string name)
    {
        Columns.Add(name);
        foreach (var r in Rows) r.AddCell();
        ColumnsChanged?.Invoke(this, EventArgs.Empty);
        OnChanged();
    }

    public void RenameColumn(int index, string name)
    {
        if (index < 0 || index >= Columns.Count) return;
        Columns[index] = name;
        ColumnsChanged?.Invoke(this, EventArgs.Empty);
        OnChanged();
    }

    public void DeleteColumn(int index)
    {
        if (index < 0 || index >= Columns.Count) return;
        Columns.RemoveAt(index);
        foreach (var r in Rows) r.RemoveCellAt(index);
        ColumnsChanged?.Invoke(this, EventArgs.Empty);
        OnChanged();
    }

    private void OnRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => OnChanged();

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (CsvRow r in e.NewItems) r.PropertyChanged += OnRowChanged;
        if (e.OldItems is not null)
            foreach (CsvRow r in e.OldItems) r.PropertyChanged -= OnRowChanged;
        OnChanged();
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test EZEditor.slnx --filter "CsvDocumentTests|CsvRow"`
Expected: PASS.

- [ ] **Step 7: Commit (gated)**

```bash
git add src/EZEditor/ViewModels/CsvRow.cs src/EZEditor/ViewModels/CsvDocument.cs src/EZEditor/Services/CsvParseResult.cs src/EZEditor/Services/CsvDocumentService.cs tests/EZEditor.Tests/CsvDocumentTests.cs
git commit -m "feat: add CsvRow + CsvDocument model with row/column edits"
```

### Task 9: Wire CSV into `DocumentFactory`

**Files:**
- Modify: `src/EZEditor/Services/DocumentFactory.cs`
- Test: `tests/EZEditor.Tests/DocumentFactoryTests.cs` (add a case)

**Interfaces:**
- Consumes: `CsvDocumentService`, `CsvDocument`.

- [ ] **Step 1: Add the failing test**

Append to `DocumentFactoryTests`:
```csharp
    [Fact]
    public void LoadAuto_CsvFile_ReturnsCsvDocument()
    {
        var path = Path.Combine(Path.GetTempPath(), $"df_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, "name,age\nAlice,30");
        try
        {
            var doc = new DocumentFactory().LoadAuto(path);
            Assert.IsType<EZEditor.ViewModels.CsvDocument>(doc);
        }
        finally { File.Delete(path); }
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test EZEditor.slnx --filter DocumentFactoryTests`
Expected: FAIL — `Create` throws `NotSupportedException` for CSV.

- [ ] **Step 3: Wire CSV in `DocumentFactory`**

Add the field `private readonly CsvDocumentService _csv = new();` and change the CSV switch arm in `Create`:
```csharp
        DocumentFormat.Csv => new CsvDocument(_csv.Parse(text), _csv),
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test EZEditor.slnx --filter DocumentFactoryTests`
Expected: PASS.

- [ ] **Step 5: Commit (gated)**

```bash
git add src/EZEditor/Services/DocumentFactory.cs tests/EZEditor.Tests/DocumentFactoryTests.cs
git commit -m "feat: wire CSV into DocumentFactory"
```

### Task 10: CSV `DataGrid` view + dynamic columns + dark theme styling

Verified by build + smoke run.

**Files:**
- Modify: `src/EZEditor/MainWindow.xaml` (add `CsvDocument` DataTemplate)
- Modify: `src/EZEditor/MainWindow.xaml.cs` (build columns on grid load + on `ColumnsChanged`)
- Modify: `src/EZEditor/Themes/Theme.xaml` (DataGrid styles)
- Create: `samples/sample.csv`

- [ ] **Step 1: Add a CSV sample fixture**

```text
// samples/sample.csv
name,age,city,active
Alice,30,"Berlin, DE",true
Bob,25,London,false
Cara,41,"New ""NY"" York",true
```
(Write the file without the leading `// …` comment line.)

- [ ] **Step 2: Add DataGrid styling to `Theme.xaml`**

Before the closing `</ResourceDictionary>`, add:
```xml
    <!-- ============================== DataGrid ============================= -->
    <Style TargetType="DataGrid">
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="Foreground" Value="{StaticResource TextPrimary}" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="RowBackground" Value="Transparent" />
        <Setter Property="AlternatingRowBackground" Value="#12151D" />
        <Setter Property="GridLinesVisibility" Value="All" />
        <Setter Property="HorizontalGridLinesBrush" Value="{StaticResource Hairline}" />
        <Setter Property="VerticalGridLinesBrush" Value="{StaticResource Hairline}" />
        <Setter Property="FontFamily" Value="{StaticResource MonoFont}" />
        <Setter Property="FontSize" Value="13" />
        <Setter Property="RowHeaderWidth" Value="0" />
        <Setter Property="CanUserAddRows" Value="False" />
        <Setter Property="CanUserDeleteRows" Value="False" />
        <Setter Property="AutoGenerateColumns" Value="False" />
        <Setter Property="SelectionUnit" Value="Cell" />
        <Setter Property="HeadersVisibility" Value="Column" />
    </Style>

    <Style TargetType="DataGridColumnHeader">
        <Setter Property="Background" Value="{StaticResource Toolbar}" />
        <Setter Property="Foreground" Value="{StaticResource KeyColor}" />
        <Setter Property="FontFamily" Value="{StaticResource UiFont}" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="Padding" Value="10,6" />
        <Setter Property="BorderBrush" Value="{StaticResource Hairline}" />
        <Setter Property="BorderThickness" Value="0,0,1,1" />
    </Style>

    <Style TargetType="DataGridCell">
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Padding" Value="8,4" />
        <Setter Property="Foreground" Value="{StaticResource TextPrimary}" />
        <Style.Triggers>
            <Trigger Property="IsSelected" Value="True">
                <Setter Property="Background" Value="{StaticResource RowSelected}" />
                <Setter Property="Foreground" Value="{StaticResource TextPrimary}" />
            </Trigger>
        </Style.Triggers>
    </Style>
```

- [ ] **Step 3: Add the `CsvDocument` DataTemplate to `MainWindow.xaml`**

Inside `<Window.Resources>` (after the JSON template) add:
```xml
        <DataTemplate DataType="{x:Type vm:CsvDocument}">
            <Border Margin="14" CornerRadius="10" Background="{StaticResource Surface}"
                    BorderBrush="{StaticResource Hairline}" BorderThickness="1">
                <DataGrid x:Name="CsvGrid" ItemsSource="{Binding Rows}"
                          Loaded="OnCsvGridLoaded"
                          Tag="{Binding}"
                          Margin="2" Padding="6">
                    <DataGrid.ContextMenu>
                        <ContextMenu Tag="{Binding}">
                            <MenuItem Header="Add row" Click="OnCsvAddRow" />
                            <MenuItem Header="Delete selected row" Click="OnCsvDeleteRow" />
                            <Separator/>
                            <MenuItem Header="Add column" Click="OnCsvAddColumn" />
                        </ContextMenu>
                    </DataGrid.ContextMenu>
                </DataGrid>
            </Border>
        </DataTemplate>
```

- [ ] **Step 4: Add CSV code-behind to `MainWindow.xaml.cs`**

Add `using System.Windows.Controls;` is already present. Add `using System.Windows.Data;` and `using EZEditor.ViewModels;` (the latter is present). Add these members to `MainWindow`:
```csharp
    // Build DataGrid columns from CsvDocument.Columns; rebind on ColumnsChanged.
    private void OnCsvGridLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGrid grid || grid.DataContext is not CsvDocument doc) return;
        BuildCsvColumns(grid, doc);
        doc.ColumnsChanged -= GridColumnsChanged;            // avoid double-subscribe on re-load
        doc.ColumnsChanged += GridColumnsChanged;
        grid.Tag = doc;
        _csvGridForDoc = grid;
    }

    private DataGrid? _csvGridForDoc;

    private void GridColumnsChanged(object? sender, EventArgs e)
    {
        if (_csvGridForDoc is { } grid && sender is CsvDocument doc)
            BuildCsvColumns(grid, doc);
    }

    private static void BuildCsvColumns(DataGrid grid, CsvDocument doc)
    {
        grid.Columns.Clear();
        for (var i = 0; i < doc.Columns.Count; i++)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = doc.Columns[i],
                Binding = new Binding($"[{i}]")
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
        }
    }

    private static CsvDocument? CsvFromMenu(object sender)
        => (sender as FrameworkElement)?.DataContext as CsvDocument
           ?? (((sender as MenuItem)?.Parent as ContextMenu)?.Tag as CsvDocument);

    private void OnCsvAddRow(object sender, RoutedEventArgs e) => CsvFromMenu(sender)?.AddRow();

    private void OnCsvDeleteRow(object sender, RoutedEventArgs e)
    {
        if (CsvFromMenu(sender) is not { } doc || _csvGridForDoc is not { } grid) return;
        if (grid.CurrentItem is CsvRow row) doc.DeleteRow(row);
    }

    private void OnCsvAddColumn(object sender, RoutedEventArgs e)
        => CsvFromMenu(sender)?.AddColumn($"Column{(CsvFromMenu(sender)!.Columns.Count + 1)}");
```
Note: `[{i}]` binds each column to `CsvRow`'s integer indexer; the indexer raises `"Item[]"` so edits propagate and mark the document dirty.

- [ ] **Step 5: Build**

Run:
```powershell
Start-Process taskkill -ArgumentList '/F','/IM','EZEditor.exe' -Verb RunAs -Wait
dotnet build EZEditor.slnx
```
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 6: Smoke-run with the CSV sample**

Run:
```powershell
Start-Process "src/EZEditor/bin/Debug/net9.0-windows7.0/EZEditor.exe" -ArgumentList '"samples/sample.csv"'
```
Expected: a dark grid with columns `name age city active`; `Berlin, DE` and `New "NY" York` render correctly in single cells; editing a cell flips status to `[CSV]  ●`; right-click → Add row / Add column / Delete selected row work; Save then reopen round-trips (commas/quotes preserved). Close the window.

- [ ] **Step 7: Run the full suite**

Run: `dotnet test EZEditor.slnx`
Expected: all tests pass.

- [ ] **Step 8: Commit (gated)**

```bash
git add src/EZEditor/MainWindow.xaml src/EZEditor/MainWindow.xaml.cs src/EZEditor/Themes/Theme.xaml samples/sample.csv
git commit -m "feat: CSV DataGrid editor with dynamic columns + dark styling"
```

---

## Phase 4 — XML (faithful element tree)

The XML VM tree is a *view* over a live `XDocument` loaded with `LoadOptions.PreserveWhitespace`. Edits mutate the underlying `XObject`; serialization writes the `XDocument`, so declaration, attributes, namespaces, comments, CDATA, and element order survive round-trips. Pure-whitespace text nodes stay in the `XDocument` (faithful output) but are not shown as tree nodes.

### Task 11: `XmlNodeKind` + `XmlNodeViewModel`

**Files:**
- Create: `src/EZEditor/Models/XmlNodeKind.cs`
- Create: `src/EZEditor/ViewModels/XmlNodeViewModel.cs`
- Test: `tests/EZEditor.Tests/XmlNodeViewModelTests.cs`

**Interfaces:**
- Produces: `enum EZEditor.Models.XmlNodeKind { Element, Attribute, Text, Comment, CData }`
- Produces: `sealed class EZEditor.ViewModels.XmlNodeViewModel : ObservableObject` — ctor `XmlNodeViewModel(XmlNodeKind kind, XObject xobj, XmlNodeViewModel? parent, string? name = null, string? value = null)`; props `XmlNodeKind Kind`, `string? Name` (set ⇒ writes through to `XElement.Name`), `string? Value` (set ⇒ writes through to `XText`/`XComment`/`XAttribute`), `XObject XObject`, `XmlNodeViewModel? Parent`, `ObservableCollection<XmlNodeViewModel> Attributes`, `ObservableCollection<XmlNodeViewModel> Children`, `bool IsExpanded`, `bool IsFilteredOut`, `string DisplayName`; `event EventHandler? Changed` (bubbles to parent); methods `void RaiseChanged()`, `void SetExpandedRecursive(bool)`, `bool ApplyFilter(string?)`, `void Delete()`, `XmlNodeViewModel AddChildElement(string name)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/EZEditor.Tests/XmlNodeViewModelTests.cs
using System.Xml.Linq;
using EZEditor.Models;
using EZEditor.ViewModels;

namespace EZEditor.Tests;

public class XmlNodeViewModelTests
{
    [Fact]
    public void EditingValue_WritesThroughToXObject()
    {
        var el = new XElement("a", new XText("old"));
        var textNode = el.Nodes().OfType<XText>().First();
        var vm = new XmlNodeViewModel(XmlNodeKind.Text, textNode, null, value: "old");
        vm.Value = "new";
        Assert.Equal("new", textNode.Value);
    }

    [Fact]
    public void EditingName_RenamesElement_PreservingNamespace()
    {
        XNamespace ns = "http://example.com";
        var el = new XElement(ns + "old");
        var vm = new XmlNodeViewModel(XmlNodeKind.Element, el, null, name: "old");
        vm.Name = "renamed";
        Assert.Equal(ns + "renamed", el.Name);
    }

    [Fact]
    public void EditingAttributeValue_WritesThrough()
    {
        var attr = new XAttribute("id", "1");
        _ = new XElement("a", attr);
        var vm = new XmlNodeViewModel(XmlNodeKind.Attribute, attr, null, name: "id", value: "1");
        vm.Value = "2";
        Assert.Equal("2", attr.Value);
    }

    [Fact]
    public void Changed_BubblesToParent()
    {
        var parentEl = new XElement("p", new XElement("c", new XText("v")));
        var pvm = new XmlNodeViewModel(XmlNodeKind.Element, parentEl, null, name: "p");
        var childEl = parentEl.Elements().First();
        var cvm = new XmlNodeViewModel(XmlNodeKind.Element, childEl, pvm, name: "c");
        pvm.Children.Add(cvm);

        var fired = 0;
        pvm.Changed += (_, _) => fired++;
        cvm.Name = "c2";
        Assert.True(fired >= 1);
    }

    [Fact]
    public void DisplayName_PrefixesAttributesWithAt()
    {
        var attr = new XAttribute("id", "1");
        var vm = new XmlNodeViewModel(XmlNodeKind.Attribute, attr, null, name: "id", value: "1");
        Assert.Equal("@id", vm.DisplayName);
    }

    [Fact]
    public void ApplyFilter_MatchesNameValueAndAttributes()
    {
        var el = new XElement("root", new XElement("city", new XText("Berlin")));
        var rvm = new XmlNodeViewModel(XmlNodeKind.Element, el, null, name: "root");
        var cityEl = el.Elements().First();
        var cvm = new XmlNodeViewModel(XmlNodeKind.Element, cityEl, rvm, name: "city");
        var txt = new XmlNodeViewModel(XmlNodeKind.Text, cityEl.Nodes().OfType<XText>().First(), cvm, value: "Berlin");
        cvm.Children.Add(txt);
        rvm.Children.Add(cvm);

        rvm.ApplyFilter("berlin");
        Assert.False(cvm.IsFilteredOut);  // matched via descendant text
        Assert.False(txt.IsFilteredOut);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test EZEditor.slnx --filter XmlNodeViewModelTests`
Expected: FAIL — types do not exist.

- [ ] **Step 3: Write `XmlNodeKind`**

```csharp
// src/EZEditor/Models/XmlNodeKind.cs
namespace EZEditor.Models;

public enum XmlNodeKind { Element, Attribute, Text, Comment, CData }
```

- [ ] **Step 4: Write `XmlNodeViewModel`**

```csharp
// src/EZEditor/ViewModels/XmlNodeViewModel.cs
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using EZEditor.Models;

namespace EZEditor.ViewModels;

public sealed partial class XmlNodeViewModel : ObservableObject
{
    public XmlNodeViewModel(XmlNodeKind kind, XObject xobj, XmlNodeViewModel? parent,
        string? name = null, string? value = null)
    {
        _kind = kind;
        XObject = xobj;
        Parent = parent;
        _name = name;     // set backing fields directly so construction never raises Changed
        _value = value;
        Attributes = new ObservableCollection<XmlNodeViewModel>();
        Children = new ObservableCollection<XmlNodeViewModel>();
        Children.CollectionChanged += OnChildrenChanged;
    }

    public XObject XObject { get; }
    public XmlNodeViewModel? Parent { get; }
    public ObservableCollection<XmlNodeViewModel> Attributes { get; }
    public ObservableCollection<XmlNodeViewModel> Children { get; }

    [ObservableProperty] private XmlNodeKind _kind;
    [ObservableProperty] private string? _name;
    [ObservableProperty] private string? _value;
    [ObservableProperty] private bool _isExpanded = true;
    [ObservableProperty] private bool _isFilteredOut;

    public bool IsElement => Kind == XmlNodeKind.Element;
    public bool HasAttributes => Attributes.Count > 0;

    public string DisplayName => Kind switch
    {
        XmlNodeKind.Attribute => $"@{Name}",
        XmlNodeKind.Element => Name ?? "(element)",
        XmlNodeKind.Comment => "(comment)",
        XmlNodeKind.CData => "(cdata)",
        _ => "(text)",
    };

    public event EventHandler? Changed;

    public void RaiseChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
        Parent?.RaiseChanged();
    }

    partial void OnValueChanged(string? value)
    {
        switch (XObject)
        {
            case XText t: t.Value = value ?? string.Empty; break;   // XCData derives from XText
            case XComment c: c.Value = value ?? string.Empty; break;
            case XAttribute a: a.Value = value ?? string.Empty; break;
        }
        RaiseChanged();
    }

    partial void OnNameChanged(string? value)
    {
        if (XObject is XElement el && !string.IsNullOrWhiteSpace(value))
        {
            var local = value.Contains(':') ? value[(value.IndexOf(':') + 1)..] : value;
            el.Name = XName.Get(local, el.Name.NamespaceName);
        }
        RaiseChanged();
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e) => RaiseChanged();

    public XmlNodeViewModel AddChildElement(string name)
    {
        if (XObject is not XElement el)
            throw new InvalidOperationException("Only elements can have child elements.");
        var child = new XElement(XName.Get(name));
        el.Add(child);
        var vm = new XmlNodeViewModel(XmlNodeKind.Element, child, this, name: name);
        Children.Add(vm);
        return vm;
    }

    public void Delete()
    {
        switch (XObject)
        {
            case XAttribute a: a.Remove(); Parent?.Attributes.Remove(this); break;
            default:
                if (XObject is XNode n) n.Remove();
                Parent?.Children.Remove(this);
                break;
        }
        Parent?.RaiseChanged();
    }

    public void SetExpandedRecursive(bool expanded)
    {
        IsExpanded = expanded;
        foreach (var c in Children) c.SetExpandedRecursive(expanded);
    }

    private bool? _expandedBeforeFilter;

    public bool ApplyFilter(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            IsFilteredOut = false;
            if (_expandedBeforeFilter is bool prev) { IsExpanded = prev; _expandedBeforeFilter = null; }
            foreach (var c in Children) c.ApplyFilter(null);
            return true;
        }

        bool Match(string? s) => s is not null && s.Contains(text, StringComparison.OrdinalIgnoreCase);
        var selfMatch = Match(Name) || Match(Value)
            || Attributes.Any(a => Match(a.Name) || Match(a.Value));

        var childMatch = false;
        foreach (var c in Children)
        {
            var m = selfMatch ? ShowSubtree(c) : c.ApplyFilter(text);
            childMatch |= m;
        }

        var matched = selfMatch || childMatch;
        IsFilteredOut = !matched;
        if (matched && Children.Count > 0)
        {
            _expandedBeforeFilter ??= IsExpanded;
            IsExpanded = true;
        }
        return matched;
    }

    private static bool ShowSubtree(XmlNodeViewModel n)
    {
        n.IsFilteredOut = false;
        foreach (var c in n.Children) ShowSubtree(c);
        return true;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test EZEditor.slnx --filter XmlNodeViewModelTests`
Expected: PASS (6 tests).

- [ ] **Step 6: Commit (gated)**

```bash
git add src/EZEditor/Models/XmlNodeKind.cs src/EZEditor/ViewModels/XmlNodeViewModel.cs tests/EZEditor.Tests/XmlNodeViewModelTests.cs
git commit -m "feat: add XmlNodeKind + XmlNodeViewModel (live XObject write-through)"
```

### Task 12: `XmlDocumentService` — faithful parse/serialize

**Files:**
- Create: `src/EZEditor/Services/XmlDocumentService.cs`
- Create: `src/EZEditor/Services/XmlParseResult.cs`
- Test: `tests/EZEditor.Tests/XmlDocumentServiceTests.cs`

**Interfaces:**
- Produces: `sealed record EZEditor.Services.XmlParseResult(XDocument Document, XmlNodeViewModel Root)`.
- Produces: `sealed class EZEditor.Services.XmlDocumentService` with `XmlParseResult Parse(string text)` (throws `XmlException` on malformed input), `string Serialize(XDocument doc)` (declaration preserved, whitespace preserved, no re-indent).

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/EZEditor.Tests/XmlDocumentServiceTests.cs
using System.Xml;
using EZEditor.Services;

namespace EZEditor.Tests;

public class XmlDocumentServiceTests
{
    private readonly XmlDocumentService _svc = new();

    [Fact]
    public void Parse_BuildsElementTree_WithAttributesAndChildren()
    {
        var r = _svc.Parse("<root id=\"1\"><city>Berlin</city></root>");
        Assert.Equal("root", r.Root.Name);
        Assert.Single(r.Root.Attributes);
        Assert.Equal("id", r.Root.Attributes[0].Name);
        Assert.Equal("1", r.Root.Attributes[0].Value);
        Assert.Single(r.Root.Children);
        Assert.Equal("city", r.Root.Children[0].Name);
    }

    [Fact]
    public void Parse_Malformed_Throws()
        => Assert.Throws<XmlException>(() => _svc.Parse("<root><unclosed></root>"));

    [Fact]
    public void Serialize_PreservesDeclarationAttributesCommentNamespace()
    {
        var src = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n"
                + "<note xmlns:x=\"urn:x\" id=\"7\">\r\n  <!-- hi -->\r\n  <x:body>text</x:body>\r\n</note>";
        var r = _svc.Parse(src);
        var outText = _svc.Serialize(r.Document);
        Assert.Contains("<?xml version=\"1.0\" encoding=\"utf-8\"?>", outText);
        Assert.Contains("id=\"7\"", outText);
        Assert.Contains("<!-- hi -->", outText);
        Assert.Contains("xmlns:x=\"urn:x\"", outText);
        Assert.Contains("<x:body>text</x:body>", outText);
    }

    [Fact]
    public void Serialize_IsIdempotent()
    {
        var src = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<r a=\"1\"><c>v</c></r>";
        var once = _svc.Serialize(_svc.Parse(src).Document);
        var twice = _svc.Serialize(_svc.Parse(once).Document);
        Assert.Equal(once, twice);
    }

    [Fact]
    public void Serialize_AppliesValueEdit()
    {
        var r = _svc.Parse("<r><c>old</c></r>");
        // c -> text child
        var textVm = r.Root.Children[0].Children[0];
        textVm.Value = "new";
        Assert.Contains("<c>new</c>", _svc.Serialize(r.Document));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test EZEditor.slnx --filter XmlDocumentServiceTests`
Expected: FAIL — service does not exist.

- [ ] **Step 3: Write `XmlParseResult`**

```csharp
// src/EZEditor/Services/XmlParseResult.cs
using System.Xml.Linq;
using EZEditor.ViewModels;

namespace EZEditor.Services;

public sealed record XmlParseResult(XDocument Document, XmlNodeViewModel Root);
```

- [ ] **Step 4: Write `XmlDocumentService`**

```csharp
// src/EZEditor/Services/XmlDocumentService.cs
using System.Text;
using System.Xml;
using System.Xml.Linq;
using EZEditor.Models;
using EZEditor.ViewModels;

namespace EZEditor.Services;

public sealed class XmlDocumentService
{
    private const int AutoExpandDepth = 2;

    public XmlParseResult Parse(string text)
    {
        // PreserveWhitespace keeps insignificant whitespace as XText nodes so output is faithful.
        var doc = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
        if (doc.Root is null) throw new XmlException("XML has no root element.");
        return new XmlParseResult(doc, BuildElement(doc.Root, null, 0));
    }

    private static XmlNodeViewModel BuildElement(XElement el, XmlNodeViewModel? parent, int depth)
    {
        var vm = new XmlNodeViewModel(XmlNodeKind.Element, el, parent, name: PrefixedName(el))
        {
            IsExpanded = depth < AutoExpandDepth
        };

        foreach (var a in el.Attributes())
        {
            if (a.IsNamespaceDeclaration) continue; // shown via element ns; preserved in the XDocument
            vm.Attributes.Add(new XmlNodeViewModel(XmlNodeKind.Attribute, a, vm,
                name: PrefixedAttrName(a), value: a.Value));
        }

        foreach (var node in el.Nodes())
        {
            switch (node)
            {
                case XElement child:
                    vm.Children.Add(BuildElement(child, vm, depth + 1));
                    break;
                case XCData cd:
                    vm.Children.Add(new XmlNodeViewModel(XmlNodeKind.CData, cd, vm, value: cd.Value));
                    break;
                case XComment cm:
                    vm.Children.Add(new XmlNodeViewModel(XmlNodeKind.Comment, cm, vm, value: cm.Value));
                    break;
                case XText tx when !string.IsNullOrWhiteSpace(tx.Value):
                    vm.Children.Add(new XmlNodeViewModel(XmlNodeKind.Text, tx, vm, value: tx.Value));
                    break;
            }
        }
        return vm;
    }

    private static string PrefixedName(XElement el)
    {
        var prefix = el.GetPrefixOfNamespace(el.Name.Namespace);
        return string.IsNullOrEmpty(prefix) ? el.Name.LocalName : $"{prefix}:{el.Name.LocalName}";
    }

    private static string PrefixedAttrName(XAttribute a)
    {
        if (a.Name.Namespace == XNamespace.None) return a.Name.LocalName;
        var prefix = a.Parent?.GetPrefixOfNamespace(a.Name.Namespace);
        return string.IsNullOrEmpty(prefix) ? a.Name.LocalName : $"{prefix}:{a.Name.LocalName}";
    }

    public string Serialize(XDocument doc)
    {
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,        // declaration emitted manually to preserve its exact text
            Indent = false,
            NewLineHandling = NewLineHandling.None,
        };
        var sb = new StringBuilder();
        using (var xw = XmlWriter.Create(sb, settings)) doc.Save(xw);
        var body = sb.ToString();
        return doc.Declaration is null
            ? body
            : doc.Declaration + "\r\n" + body.TrimStart('\r', '\n');
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test EZEditor.slnx --filter XmlDocumentServiceTests`
Expected: PASS (5 tests). If `Serialize_IsIdempotent` fails on a stray leading newline, confirm the `body.TrimStart('\r','\n')` is present.

- [ ] **Step 6: Commit (gated)**

```bash
git add src/EZEditor/Services/XmlDocumentService.cs src/EZEditor/Services/XmlParseResult.cs tests/EZEditor.Tests/XmlDocumentServiceTests.cs
git commit -m "feat: add XmlDocumentService (faithful parse/serialize)"
```

### Task 13: `XmlDocument` + wire into `DocumentFactory`

**Files:**
- Create: `src/EZEditor/ViewModels/XmlDocument.cs`
- Modify: `src/EZEditor/Services/DocumentFactory.cs`
- Test: `tests/EZEditor.Tests/XmlDocumentTests.cs`; add a case to `DocumentFactoryTests`

**Interfaces:**
- Produces: `sealed class EZEditor.ViewModels.XmlDocument : EditableDocument` — ctor `XmlDocument(XmlParseResult parsed, XmlDocumentService svc)`; `ObservableCollection<XmlNodeViewModel> Roots`; `XmlNodeViewModel Root => Roots[0]`; `override Format => DocumentFormat.Xml`; `override Serialize()`; `override ApplyFilter(text)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/EZEditor.Tests/XmlDocumentTests.cs
using EZEditor.Services;
using EZEditor.ViewModels;

namespace EZEditor.Tests;

public class XmlDocumentTests
{
    private static XmlDocument Make(string xml)
    {
        var svc = new XmlDocumentService();
        return new XmlDocument(svc.Parse(xml), svc);
    }

    [Fact]
    public void Format_IsXml_AndRootExposed()
    {
        var doc = Make("<r><c>v</c></r>");
        Assert.Equal(DocumentFormat.Xml, doc.Format);
        Assert.Equal("r", doc.Root.Name);
        Assert.Single(doc.Roots);
    }

    [Fact]
    public void EditingValue_RaisesChanged_AndSerializes()
    {
        var doc = Make("<r><c>old</c></r>");
        var fired = 0;
        doc.Changed += (_, _) => fired++;
        doc.Root.Children[0].Children[0].Value = "new"; // c -> text
        Assert.True(fired >= 1);
        Assert.Contains("<c>new</c>", doc.Serialize());
    }

    [Fact]
    public void ApplyFilter_DelegatesToRoot()
    {
        var doc = Make("<r><a>x</a><b>y</b></r>");
        doc.ApplyFilter("a");
        Assert.False(doc.Root.Children.First(c => c.Name == "a").IsFilteredOut);
        Assert.True(doc.Root.Children.First(c => c.Name == "b").IsFilteredOut);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test EZEditor.slnx --filter XmlDocumentTests`
Expected: FAIL — `XmlDocument` does not exist.

- [ ] **Step 3: Write `XmlDocument`**

```csharp
// src/EZEditor/ViewModels/XmlDocument.cs
using System.Collections.ObjectModel;
using System.Xml.Linq;
using EZEditor.Services;

namespace EZEditor.ViewModels;

public sealed class XmlDocument : EditableDocument
{
    private readonly XmlDocumentService _svc;
    private readonly XDocument _doc;

    public XmlDocument(XmlParseResult parsed, XmlDocumentService svc)
    {
        _svc = svc;
        _doc = parsed.Document;
        Roots = new ObservableCollection<XmlNodeViewModel> { parsed.Root };
        parsed.Root.Changed += (_, _) => OnChanged();
    }

    public ObservableCollection<XmlNodeViewModel> Roots { get; }
    public XmlNodeViewModel Root => Roots[0];

    public override DocumentFormat Format => DocumentFormat.Xml;
    public override string Serialize() => _svc.Serialize(_doc);
    public override void ApplyFilter(string? text) => Root.ApplyFilter(text);
}
```

- [ ] **Step 4: Wire XML into `DocumentFactory` + add a factory test**

In `DocumentFactory`, add `private readonly XmlDocumentService _xml = new();` and change the XML switch arm in `Create`:
```csharp
        DocumentFormat.Xml => new XmlDocument(_xml.Parse(text), _xml),
```
Append to `DocumentFactoryTests`:
```csharp
    [Fact]
    public void LoadAuto_XmlFile_ReturnsXmlDocument()
    {
        var path = Path.Combine(Path.GetTempPath(), $"df_{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, "<root><a>1</a></root>");
        try
        {
            var doc = new DocumentFactory().LoadAuto(path);
            Assert.IsType<EZEditor.ViewModels.XmlDocument>(doc);
        }
        finally { File.Delete(path); }
    }
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test EZEditor.slnx --filter "XmlDocumentTests|DocumentFactoryTests"`
Expected: PASS.

- [ ] **Step 6: Commit (gated)**

```bash
git add src/EZEditor/ViewModels/XmlDocument.cs src/EZEditor/Services/DocumentFactory.cs tests/EZEditor.Tests/XmlDocumentTests.cs tests/EZEditor.Tests/DocumentFactoryTests.cs
git commit -m "feat: add XmlDocument + wire XML into DocumentFactory"
```

### Task 14: XML `TreeView` view + theme colors

Verified by build + smoke run. Reuses the shared `JsonTreeItem` container style (bindings: `IsExpanded`, `IsSelected`, `IsFilteredOut`), so `XmlNodeViewModel` gains an `IsSelected` property here.

**Files:**
- Modify: `src/EZEditor/ViewModels/XmlNodeViewModel.cs` (add `IsSelected`)
- Modify: `src/EZEditor/Converters/Converters.cs` (`KindToVisibilityConverter` → enum-agnostic)
- Modify: `src/EZEditor/Themes/Theme.xaml` (add `AttrColor` brush)
- Modify: `src/EZEditor/MainWindow.xaml` (add XML `HierarchicalDataTemplate` + `XmlDocument` editor template)
- Modify: `src/EZEditor/MainWindow.xaml.cs` (XML node add/delete/expand handlers)
- Create: `samples/sample.xml`

- [ ] **Step 1: Add `IsSelected` to `XmlNodeViewModel`**

In `XmlNodeViewModel`, add next to the other `[ObservableProperty]` fields:
```csharp
    [ObservableProperty] private bool _isSelected;
```

- [ ] **Step 2: Make `KindToVisibilityConverter` enum-agnostic**

In `src/EZEditor/Converters/Converters.cs`, replace the `Convert` body of `KindToVisibilityConverter` with:
```csharp
    public object Convert(object? value, Type t, object? parameter, CultureInfo c)
        => value is not null && parameter is string s && value.ToString() == s
            ? Visibility.Visible : Visibility.Collapsed;
```
(The JSON template still passes `JsonNodeKind`; `ToString()` comparison is unchanged for it. This now also serves `XmlNodeKind`.)

- [ ] **Step 3: Add the `AttrColor` brush to `Theme.xaml`**

After the `KeyColor` brush in the palette block add:
```xml
    <SolidColorBrush x:Key="AttrColor"    Color="#E5C07B" />
```

- [ ] **Step 4: Add the XML sample fixture**

```xml
// samples/sample.xml
<?xml version="1.0" encoding="utf-8"?>
<catalog xmlns:meta="urn:demo:meta" updated="2026-06-30">
  <!-- demo catalog -->
  <book id="b1" meta:rating="5">
    <title>Clean Code</title>
    <author>Robert C. Martin</author>
    <notes><![CDATA[Has <markup> & symbols]]></notes>
  </book>
  <book id="b2">
    <title>The Pragmatic Programmer</title>
    <author>Hunt &amp; Thomas</author>
  </book>
</catalog>
```
(Write the file without the leading `// …` comment line.)

- [ ] **Step 5: Add the XML node template + document template to `MainWindow.xaml`**

Inside `<Window.Resources>` (after the CSV template) add the XML node `HierarchicalDataTemplate` and the `XmlDocument` editor template:
```xml
        <HierarchicalDataTemplate DataType="{x:Type vm:XmlNodeViewModel}" ItemsSource="{Binding Children}">
            <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                <StackPanel.ContextMenu>
                    <ContextMenu>
                        <MenuItem Header="Add child element" Click="OnXmlAddChild"/>
                        <MenuItem Header="Delete" Click="OnXmlDelete"/>
                        <Separator/>
                        <MenuItem Header="Expand all" Click="OnXmlExpandAll"/>
                        <MenuItem Header="Collapse all" Click="OnXmlCollapseAll"/>
                    </ContextMenu>
                </StackPanel.ContextMenu>

                <!-- Element: editable name + inline attributes -->
                <TextBox Style="{StaticResource KeyEditor}"
                         Text="{Binding Name, UpdateSourceTrigger=LostFocus}"
                         Visibility="{Binding Kind, Converter={StaticResource KindToVis}, ConverterParameter=Element}"/>
                <ItemsControl ItemsSource="{Binding Attributes}"
                              Visibility="{Binding Kind, Converter={StaticResource KindToVis}, ConverterParameter=Element}">
                    <ItemsControl.ItemsPanel>
                        <ItemsPanelTemplate><StackPanel Orientation="Horizontal"/></ItemsPanelTemplate>
                    </ItemsControl.ItemsPanel>
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <StackPanel Orientation="Horizontal" Margin="8,0,0,0" VerticalAlignment="Center">
                                <TextBlock Text="{Binding Name}" Foreground="{StaticResource AttrColor}"
                                           FontFamily="{StaticResource MonoFont}" FontSize="12.5" VerticalAlignment="Center"/>
                                <TextBlock Text="=" Foreground="{StaticResource TextMuted}"
                                           FontFamily="{StaticResource MonoFont}" VerticalAlignment="Center"/>
                                <TextBox Style="{StaticResource ValueEditor}" Foreground="{StaticResource StringColor}"
                                         Text="{Binding Value, UpdateSourceTrigger=LostFocus}"/>
                            </StackPanel>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>

                <!-- Text -->
                <TextBox Style="{StaticResource ValueEditor}" Foreground="{StaticResource StringColor}"
                         Text="{Binding Value, UpdateSourceTrigger=PropertyChanged}"
                         Visibility="{Binding Kind, Converter={StaticResource KindToVis}, ConverterParameter=Text}"/>
                <!-- CData -->
                <TextBox Style="{StaticResource ValueEditor}" Foreground="{StaticResource NumberColor}"
                         Text="{Binding Value, UpdateSourceTrigger=PropertyChanged}"
                         Visibility="{Binding Kind, Converter={StaticResource KindToVis}, ConverterParameter=CData}"/>
                <!-- Comment -->
                <TextBlock FontFamily="{StaticResource MonoFont}" FontStyle="Italic" FontSize="12.5"
                           Foreground="{StaticResource NullColor}" VerticalAlignment="Center" Margin="2,0,0,0"
                           Text="{Binding Value, StringFormat='&lt;!-- {0} --&gt;'}"
                           Visibility="{Binding Kind, Converter={StaticResource KindToVis}, ConverterParameter=Comment}"/>
            </StackPanel>
        </HierarchicalDataTemplate>

        <DataTemplate DataType="{x:Type vm:XmlDocument}">
            <Border Margin="14" CornerRadius="10" Background="{StaticResource Surface}"
                    BorderBrush="{StaticResource Hairline}" BorderThickness="1">
                <TreeView x:Name="XmlTree" ItemsSource="{Binding Roots}"
                          ItemContainerStyle="{StaticResource JsonTreeItem}"
                          Padding="10,8" Margin="2"
                          PreviewMouseWheel="OnTreePreviewMouseWheel"
                          ScrollViewer.CanContentScroll="True"
                          VirtualizingPanel.IsVirtualizing="True"
                          VirtualizingPanel.VirtualizationMode="Recycling"
                          VirtualizingPanel.ScrollUnit="Item">
                    <TreeView.Resources>
                        <SolidColorBrush x:Key="{x:Static SystemColors.ControlBrushKey}" Color="#161922"/>
                    </TreeView.Resources>
                </TreeView>
            </Border>
        </DataTemplate>
```
Note: `OnTreePreviewMouseWheel` finds the scroll viewer from the event sender's visual tree — confirm in Step 6 it walks from `e.OriginalSource`/`sender` rather than the hard-coded `Tree` field (see code-behind update below).

- [ ] **Step 6: Add XML node handlers in `MainWindow.xaml.cs`**

(`OnTreePreviewMouseWheel` was already generalized in Task 6, so the XML `TreeView` reuses it.) Add the XML handlers (`using EZEditor.ViewModels;` is already present):
```csharp
    private static XmlNodeViewModel? XmlNodeFrom(object sender)
        => (sender as FrameworkElement)?.DataContext as XmlNodeViewModel;

    private void OnXmlAddChild(object sender, RoutedEventArgs e)
    {
        if (XmlNodeFrom(sender) is { IsElement: true } node)
            node.AddChildElement("newElement");
    }

    private void OnXmlDelete(object sender, RoutedEventArgs e)
    {
        if (XmlNodeFrom(sender) is { Parent: not null } node) node.Delete();
    }

    private void OnXmlExpandAll(object sender, RoutedEventArgs e) => XmlRootOf(sender)?.SetExpandedRecursive(true);

    private void OnXmlCollapseAll(object sender, RoutedEventArgs e)
    {
        if (XmlRootOf(sender) is not { } root) return;
        root.SetExpandedRecursive(false);
        root.IsExpanded = true;
    }

    private static XmlNodeViewModel? XmlRootOf(object sender)
    {
        var n = XmlNodeFrom(sender);
        while (n?.Parent is not null) n = n.Parent;
        return n;
    }
```

- [ ] **Step 7: Build**

Run:
```powershell
Start-Process taskkill -ArgumentList '/F','/IM','EZEditor.exe' -Verb RunAs -Wait
dotnet build EZEditor.slnx
```
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 8: Smoke-run with the XML sample**

Run:
```powershell
Start-Process "src/EZEditor/bin/Debug/net9.0-windows7.0/EZEditor.exe" -ArgumentList '"samples/sample.xml"'
```
Expected: element names render blue; attributes inline in gold (`id="b1" meta:rating="5"`); the comment shows as `<!-- demo catalog -->` in muted italic; CDATA text is editable; editing a value/attribute flips status to `[XML]  ●`; right-click → Add child element / Delete / Expand all / Collapse all work; Save then reopen preserves declaration, namespace prefix, comment, and CDATA. Close the window.

- [ ] **Step 9: Run the full suite**

Run: `dotnet test EZEditor.slnx`
Expected: all tests pass.

- [ ] **Step 10: Commit (gated)**

```bash
git add src/EZEditor/ViewModels/XmlNodeViewModel.cs src/EZEditor/Converters/Converters.cs src/EZEditor/Themes/Theme.xaml src/EZEditor/MainWindow.xaml src/EZEditor/MainWindow.xaml.cs samples/sample.xml
git commit -m "feat: XML faithful tree editor (elements, attributes, comments, CDATA)"
```

---

## Phase 5 — Multi-format dialogs, smoke coverage, docs

### Task 15: Multi-format Open/Save dialogs

The Open dialog must surface `.json` / `.csv` / `.xml`; Save defaults to the open document's natural extension. Cross-format *conversion* on Save As (e.g. saving a JSON tree as XML) is a documented **non-goal** for v1 — Save writes the format the document was opened as; the dialog just defaults the extension.

**Files:**
- Modify: `src/EZEditor/Services/FileDialogService.cs`

- [ ] **Step 1: Rewrite `FileDialogService` with multi-format filters**

```csharp
// src/EZEditor/Services/FileDialogService.cs
using System.IO;
using Microsoft.Win32;

namespace EZEditor.Services;

public class FileDialogService : IFileDialogService
{
    private const string Filter =
        "All supported (*.json;*.csv;*.xml)|*.json;*.csv;*.xml|" +
        "JSON files (*.json)|*.json|" +
        "CSV files (*.csv)|*.csv|" +
        "XML files (*.xml)|*.xml|" +
        "All files (*.*)|*.*";

    public string? OpenFile()
    {
        var dlg = new OpenFileDialog { Filter = Filter, CheckFileExists = true };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? SaveFile(string? suggestedName)
    {
        var ext = string.IsNullOrEmpty(suggestedName) ? ".json" : Path.GetExtension(suggestedName);
        if (string.IsNullOrEmpty(ext)) ext = ".json";
        var dlg = new SaveFileDialog
        {
            Filter = Filter,
            FileName = suggestedName is null ? $"data{ext}" : Path.GetFileName(suggestedName),
            DefaultExt = ext
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
```

- [ ] **Step 2: Build + run the full suite**

Run:
```powershell
Start-Process taskkill -ArgumentList '/F','/IM','EZEditor.exe' -Verb RunAs -Wait
dotnet build EZEditor.slnx
dotnet test EZEditor.slnx
```
Expected: `Build succeeded`; all tests pass. (Manual: File → Open now lists JSON/CSV/XML.)

- [ ] **Step 3: Commit (gated)**

```bash
git add src/EZEditor/Services/FileDialogService.cs
git commit -m "feat: multi-format Open/Save dialog filters"
```

### Task 16: Extend the WPF smoke test to CSV and XML

**Files:**
- Modify: `tests/EZEditor.Tests/ViewSmokeTests.cs`

**Interfaces:**
- Consumes: `MainViewModel`, `DocumentFactory`, `CsvDocument`, `XmlDocument`, `MainWindow`.

- [ ] **Step 1: Write the failing tests (add a visual-tree helper + two facts)**

Add `using System.Windows.Controls;` and `using System.Windows.Media;` at the top of `ViewSmokeTests.cs`, then add inside the class:
```csharp
    private static T? FindVisualChild<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            if (c is T hit) return hit;
            var deeper = FindVisualChild<T>(c);
            if (deeper is not null) return deeper;
        }
        return null;
    }

    private static MainWindow BuildWindow(string fileName, string contents, out MainViewModel vm)
    {
        var app = Application.Current ?? new Application();
        if (app.Resources.MergedDictionaries.Count == 0)
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/EZEditor;component/Themes/Theme.xaml")
            });

        var path = Path.Combine(Path.GetTempPath(), fileName.Replace("*", Guid.NewGuid().ToString("N")));
        File.WriteAllText(path, contents);
        vm = new MainViewModel(new DocumentFactory(), new NoDialogs(), new NoPrompt());
        vm.OpenPath(path);
        File.Delete(path);

        var window = new MainWindow { DataContext = vm };
        window.Measure(new Size(900, 650));
        window.Arrange(new Rect(0, 0, 900, 650));
        window.UpdateLayout();
        return window;
    }

    [Fact]
    public void MainWindow_RealizesDataGrid_ForCsvDocument()
    {
        RunOnSta(() =>
        {
            var window = BuildWindow("vsmoke_*.csv", "name,age\nAlice,30\nBob,25", out var vm);
            Assert.IsType<CsvDocument>(vm.CurrentDocument);
            Assert.NotNull(FindVisualChild<DataGrid>(window));
        });
    }

    [Fact]
    public void MainWindow_RealizesTreeView_ForXmlDocument()
    {
        RunOnSta(() =>
        {
            var window = BuildWindow("vsmoke_*.xml", "<root id=\"1\"><c>v</c></root>", out var vm);
            Assert.IsType<XmlDocument>(vm.CurrentDocument);
            Assert.NotNull(FindVisualChild<TreeView>(window));
        });
    }
```

- [ ] **Step 2: Run to verify they fail (then pass)**

Run: `dotnet test EZEditor.slnx --filter ViewSmokeTests`
Expected: initially FAIL if any template/resource key is missing; after Phases 3–4 are correct, PASS (3 smoke tests: JSON + CSV + XML). If a `FindVisualChild` returns null, the ContentControl DataTemplate for that format did not apply — re-check the `DataType` matches the document class.

- [ ] **Step 3: Commit (gated)**

```bash
git add tests/EZEditor.Tests/ViewSmokeTests.cs
git commit -m "test: smoke-test CSV and XML editor templates realize"
```

### Task 17: Update `CLAUDE.md`, create `README.md`, final verification

**Files:**
- Modify: `CLAUDE.md`
- Create: `README.md`

- [ ] **Step 1: Update `CLAUDE.md` to describe EZEditor (multi-format)**

Apply these edits (the file currently describes a JSON-only "JSONEditor"):
- **Title/intro:** rename "JSONEditor" → "EZEditor"; describe it as a multi-format editor for **JSON, CSV, and XML** that auto-detects format by content.
- **Build/run/test:** solution is `EZEditor.slnx`; app exe is `src/EZEditor/bin/Debug/net9.0-windows7.0/EZEditor.exe`; publish paths use `src/EZEditor/EZEditor.csproj`; update the test-count note (baseline + the new EditableDocument/JsonDocument/Csv/Xml/Factory/smoke tests).
- **Elevated gotchas:** replace `JsonEditor.exe` with `EZEditor.exe` in the taskkill/Start-Process commands.
- **Architecture tree:** add `ViewModels/EditableDocument.cs`, `JsonDocument.cs`, `XmlDocument.cs`, `CsvDocument.cs`, `CsvRow.cs`, `XmlNodeViewModel.cs`; `Models/XmlNodeKind.cs`; `Services/DocumentFactory.cs`, `CsvDocumentService.cs`, `CsvParseResult.cs`, `XmlDocumentService.cs`, `XmlParseResult.cs`; note `MainWindow` now hosts a `ContentControl` that swaps JSON `TreeView` / XML `TreeView` / CSV `DataGrid` by document type.
- **Key types:** document the `EditableDocument` abstraction + `DocumentFactory.Detect` content-sniff (`<` ⇒ XML; structural JSON ⇒ JSON; else CSV; extension tiebreaker when empty).
- **Features:** add CSV grid editing (cells, add/delete row+column, header row, delimiter detect) and faithful XML editing (elements, attributes inline, comments, CDATA, declaration/namespace preserved).
- **Conventions:** keep parse/serialize/edit/filter logic WPF-free (services + node VMs); `System.Xml.Linq` / `System.Text.Json` allowed.
- **Deferred/limitations:** add — cross-format Save As conversion not implemented; XML add-element produces no surrounding indent whitespace; CSV assumes a single delimiter per file.

- [ ] **Step 2: Create `README.md`**

```markdown
# EZEditor

A native **Windows desktop** editor (C#, **WPF**, .NET 9) that opens **JSON, CSV, and XML**
files and edits them in a structured, type-aware UI — never as free-form text — then saves
valid output back. Format is **auto-detected from file content**.

![status](https://img.shields.io/badge/platform-Windows-blue) ·  by **yonka**

## Formats

| Format | Editor | Fidelity |
|--------|--------|----------|
| **JSON** | Collapsible tree with type-aware inline editors (string/number/bool/null/object/array) | Preserves key order + exact number text, 2-space indent |
| **CSV**  | Spreadsheet grid (rows × columns), add/delete row & column, header row | RFC-4180 quoting; delimiter (`,` `;` tab) detected & preserved |
| **XML**  | Faithful element tree: editable element names, inline attributes, comments, CDATA | Preserves declaration, attributes, namespaces, comments, element order |

## Features

- Auto-detect format by content (extension is a tiebreaker only)
- Open / Save / Save As / Reload from disk; "Open Externally" (Notepad++ if installed, else default app)
- Filter box (matches keys/values), dirty `●` indicator, unsaved-changes prompt
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

Author: **yonka** (jonatan@cyray.io). Repository: https://github.com/yonka2019/JSONEditor
```

- [ ] **Step 3: Final full verification across all three formats**

Run:
```powershell
Start-Process taskkill -ArgumentList '/F','/IM','EZEditor.exe' -Verb RunAs -Wait
dotnet build EZEditor.slnx
dotnet test EZEditor.slnx
```
Expected: `Build succeeded`, 0 errors; **all tests pass**. Then smoke each format:
```powershell
Start-Process "src/EZEditor/bin/Debug/net9.0-windows7.0/EZEditor.exe" -ArgumentList '"samples/sample.json"'
Start-Process "src/EZEditor/bin/Debug/net9.0-windows7.0/EZEditor.exe" -ArgumentList '"samples/sample.csv"'
Start-Process "src/EZEditor/bin/Debug/net9.0-windows7.0/EZEditor.exe" -ArgumentList '"samples/sample.xml"'
```
Expected: each opens in its correct editor; the status bar shows `[JSON]` / `[CSV]` / `[XML]`; edit → Save → reopen round-trips each format. Close the windows.

- [ ] **Step 4: Commit (gated)**

```bash
git add CLAUDE.md README.md
git commit -m "docs: EZEditor multi-format README + CLAUDE.md update"
```

---

## Verification summary (run after the full plan)

- `dotnet build EZEditor.slnx` → 0 errors.
- `dotnet test EZEditor.slnx` → all green (existing JSON tests + EditableDocument, JsonDocument, DocumentFactory, CsvDocumentService, CsvDocument/CsvRow, XmlNodeViewModel, XmlDocumentService, XmlDocument, and 3 view-smoke tests).
- Manual: open each of `samples/sample.json|csv|xml`; confirm correct editor, format tag in the status bar, and lossless Save→reopen round-trip.
