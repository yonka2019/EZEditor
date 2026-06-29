# Type-Aware JSON Editor — Design Spec

**Date:** 2026-06-29
**Status:** Approved (design)

## 1. Purpose

A native Windows desktop application that opens a JSON file, displays it in a
beautiful, modern GUI, and lets the user edit values, keys, and structure with
controls that respect each value's type. The document is edited through a
structured model (never free-form text), so it is always structurally valid.

## 2. Stack

- **.NET 10 + WPF**, C# (`net10.0-windows`).
- **MVVM** via **CommunityToolkit.Mvvm** (`[ObservableProperty]`, `[RelayCommand]`).
- **WPF-UI** NuGet package for a Windows 11 Fluent look (Mica, modern controls,
  light/dark theme).
- **System.Text.Json** (built-in) for parse/serialize.
- **xUnit** for the test project.

## 3. Architecture

### 3.1 View-model tree (the core)

JSON is represented as a tree of `JsonNodeViewModel`, not bound directly to a
raw JSON DOM. Each node carries:

- `Name` — object key, or array index (display only), or root label.
- `Kind` — `JsonNodeKind` enum: `Object, Array, String, Number, Boolean, Null`.
- `Value` — the scalar value for primitive kinds (string/number/bool/null).
- `Children` — `ObservableCollection<JsonNodeViewModel>` for object/array kinds.
- `Parent` — back-reference (for delete / rename / type-change operations).
- UI state: `IsExpanded`, `IsSelected`, validation/error state.

Rationale: a dedicated VM tree gives full control over type-aware editing,
add/delete/rename/convert operations, and dirty tracking, and keeps all logic
unit-testable independently of WPF.

### 3.2 Components

```
JSONEditor.sln
src/JsonEditor/                 WPF app (net10.0-windows)
  App.xaml / App.xaml.cs
  MainWindow.xaml / .cs          shell: toolbar + TreeView + status bar
  ViewModels/
    MainViewModel.cs             open/save commands, dirty state, filter, root node
    JsonNodeViewModel.cs         tree node + per-node edit operations
  Models/
    JsonNodeKind.cs              enum
  Services/
    JsonDocumentService.cs       file <-> JsonNode <-> VM tree conversion
  Resources/
    NodeTemplates.xaml           per-kind DataTemplates
    NodeTemplateSelector.cs      picks editor template by Kind
tests/JsonEditor.Tests/          xUnit
```

## 4. UI

- **Toolbar:** Open (Ctrl+O), Save (Ctrl+S), Save As, and a key **filter/search** box.
- **Center:** `TreeView` with a `HierarchicalDataTemplate`. A `DataTemplateSelector`
  selects the inline editor by `Kind`:
  - `String` → text box
  - `Number` → numeric text box (validated)
  - `Boolean` → toggle switch
  - `Null` → "null" label with a "set value" affordance
  - `Object` / `Array` → expandable header showing child count
- **Per-node actions** (context menu / inline buttons): add child (object key or
  array item), delete, rename key, change type (convert between
  string/number/bool/null/object/array).
- **Status bar:** file path, dirty indicator (`●`), and parse/validation messages.

## 5. Data flow

```
File ──System.Text.Json (JsonNode)──> build VM tree ──user edits──>
serialize VM tree ──> File (pretty-printed, 2-space indent)
```

Editing operates only on the VM tree, so the in-memory document is always valid.
Object key insertion order is preserved on round-trip.

## 6. Error handling

- **Invalid JSON on open:** friendly dialog showing the parse error
  (message + line/position); the app does not crash and keeps any current document.
- **Bad number input:** inline red border on the field; that node's invalid value
  is not committed.
- **Unsaved changes:** prompt (Save / Discard / Cancel) when opening another file
  or closing the window while dirty.

## 7. Testing

xUnit project covering the testable core (UI-independent):

- JSON → VM tree → JSON **round-trip** preserves values, types, and object key order.
- **Type conversions** between every kind behave sensibly (e.g. string→number when
  parseable, object/array→ become empty containers, primitives→null).
- **Add / rename / delete** operations mutate the tree correctly and update dirty state.
- Pretty-print output format (2-space indent) is stable.

The WPF shell itself is verified manually by running the app.

## 8. Scope

**v1 (in scope):** open one file, type-aware tree editing, add/delete/rename/
change-type, save / save-as, key filter, dirty tracking, friendly parse errors.

**Out of scope for v1 (easy fast-follows):** JSON Schema validation, multi-tab /
multi-file, undo/redo, raw-text split view.
