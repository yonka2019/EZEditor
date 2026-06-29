# Type-Aware JSON Editor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A native Windows WPF desktop app that opens a JSON file, shows it in a beautiful Fluent-styled collapsible tree, and edits values/keys/structure with type-aware controls, then saves valid JSON back.

**Architecture:** JSON is loaded into a tree of `JsonNodeViewModel` objects (each knows its `Kind`, key/index, scalar value, and children). The UI binds to this tree; a `DataTemplateSelector` renders a type-appropriate editor per node. All parse/serialize/edit logic lives in UI-independent classes (`JsonDocumentService`, `JsonNodeViewModel`) so it is unit-testable; the WPF shell is verified by running the app.

**Tech Stack:** .NET 10, WPF (`net10.0-windows`), C#, CommunityToolkit.Mvvm, WPF-UI (Fluent theme), System.Text.Json, xUnit.

## Global Constraints

- Target framework: `net10.0-windows` for **both** the app and test projects.
- `<Nullable>enable</Nullable>` and `<LangVersion>latest</LangVersion>` in every project.
- MVVM only — no code-behind logic beyond view wiring; use `[ObservableProperty]` / `[RelayCommand]`.
- All file/dialog access goes through interfaces (`IFileDialogService`, `IUserPrompt`) so view-models are testable with fakes.
- JSON output is pretty-printed with **2-space** indentation; object **key insertion order is preserved** on round-trip; number text is preserved verbatim.
- **No git commits.** This is not a git repo and the user's global rule forbids auto-commits. Each task ends with a **Checkpoint** (build + tests green) instead of a commit. Only initialize git / commit if the user explicitly asks.
- Solution file: `JSONEditor.sln`. App project: `src/JsonEditor/JsonEditor.csproj`. Tests: `tests/JsonEditor.Tests/JsonEditor.Tests.csproj`.

---

### Task 1: Solution & project scaffolding

**Files:**
- Create: `JSONEditor.sln`
- Create: `src/JsonEditor/JsonEditor.csproj`
- Create: `tests/JsonEditor.Tests/JsonEditor.Tests.csproj`
- Create: `tests/JsonEditor.Tests/SmokeTest.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: a buildable solution with app + test projects wired together and packages restored.

- [ ] **Step 1: Create solution and projects**

Run from `C:\Code\JSONEditor`:
```bash
dotnet new sln -n JSONEditor
dotnet new wpf -n JsonEditor -o src/JsonEditor -f net10.0-windows
dotnet new xunit -n JsonEditor.Tests -o tests/JsonEditor.Tests -f net10.0-windows
dotnet sln add src/JsonEditor/JsonEditor.csproj
dotnet sln add tests/JsonEditor.Tests/JsonEditor.Tests.csproj
dotnet add tests/JsonEditor.Tests/JsonEditor.Tests.csproj reference src/JsonEditor/JsonEditor.csproj
```

- [ ] **Step 2: Add NuGet packages**

```bash
dotnet add src/JsonEditor/JsonEditor.csproj package CommunityToolkit.Mvvm
dotnet add src/JsonEditor/JsonEditor.csproj package WPF-UI
```

- [ ] **Step 3: Set project properties**

Edit `src/JsonEditor/JsonEditor.csproj` so the first `<PropertyGroup>` contains:
```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net10.0-windows</TargetFramework>
  <UseWPF>true</UseWPF>
  <Nullable>enable</Nullable>
  <LangVersion>latest</LangVersion>
  <ImplicitUsings>enable</ImplicitUsings>
  <RootNamespace>JsonEditor</RootNamespace>
</PropertyGroup>
```

Edit `tests/JsonEditor.Tests/JsonEditor.Tests.csproj` `<PropertyGroup>` to add:
```xml
<Nullable>enable</Nullable>
<LangVersion>latest</LangVersion>
```

- [ ] **Step 4: Write a smoke test**

Create `tests/JsonEditor.Tests/SmokeTest.cs`:
```csharp
namespace JsonEditor.Tests;

public class SmokeTest
{
    [Fact]
    public void Solution_Builds_And_Tests_Run()
    {
        Assert.True(true);
    }
}
```

- [ ] **Step 5: Build and run tests**

Run: `dotnet build JSONEditor.sln` then `dotnet test JSONEditor.sln`
Expected: build succeeds; 1 test passes.

- [ ] **Step 6: Checkpoint** — confirm build + test green before moving on.

---

### Task 2: `JsonNodeKind` enum and `JsonNodeViewModel` core

**Files:**
- Create: `src/JsonEditor/Models/JsonNodeKind.cs`
- Create: `src/JsonEditor/ViewModels/JsonNodeViewModel.cs`
- Test: `tests/JsonEditor.Tests/JsonNodeViewModelTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `enum JsonNodeKind { Object, Array, String, Number, Boolean, Null }`
  - `class JsonNodeViewModel : ObservableObject` with:
    - ctor `JsonNodeViewModel(JsonNodeKind kind, string? name = null, string? value = null, JsonNodeViewModel? parent = null)`
    - `JsonNodeViewModel? Parent { get; }`
    - `string? Name { get; set; }` (object key; `null` for array elements / root)
    - `JsonNodeKind Kind { get; set; }`
    - `string? Value { get; set; }` (scalar text for String/Number/Boolean; `null` otherwise)
    - `ObservableCollection<JsonNodeViewModel> Children { get; }`
    - `bool IsExpanded { get; set; }`, `bool IsSelected { get; set; }`
    - `bool IsContainer => Kind is JsonNodeKind.Object or JsonNodeKind.Array;`
    - `bool IsObjectMember => Parent is { Kind: JsonNodeKind.Object };` (true when this node's key is editable)
    - `string DisplayName` — `Name` for object members, `[index]` for array elements, `"(root)"` otherwise
    - `bool IsFilteredOut { get; set; }` — UI uses this to hide nodes during key filtering (does NOT raise `Changed`)
    - `bool ApplyFilter(string? text)` — recursively sets `IsFilteredOut`; returns true if this node or a descendant matches; empty/null text clears the filter
    - `event EventHandler? Changed;` raised on any Value/Name/Kind/Children change and bubbled to `Parent`

- [ ] **Step 1: Write the failing test**

Create `tests/JsonEditor.Tests/JsonNodeViewModelTests.cs`:
```csharp
using JsonEditor.Models;
using JsonEditor.ViewModels;

namespace JsonEditor.Tests;

public class JsonNodeViewModelTests
{
    [Fact]
    public void IsContainer_TrueForObjectAndArray()
    {
        Assert.True(new JsonNodeViewModel(JsonNodeKind.Object).IsContainer);
        Assert.True(new JsonNodeViewModel(JsonNodeKind.Array).IsContainer);
        Assert.False(new JsonNodeViewModel(JsonNodeKind.String, value: "x").IsContainer);
    }

    [Fact]
    public void DisplayName_UsesNameForObjectMembers_AndIndexForArrayElements()
    {
        var arr = new JsonNodeViewModel(JsonNodeKind.Array);
        var a = new JsonNodeViewModel(JsonNodeKind.String, value: "a", parent: arr);
        var b = new JsonNodeViewModel(JsonNodeKind.String, value: "b", parent: arr);
        arr.Children.Add(a);
        arr.Children.Add(b);
        Assert.Equal("[0]", a.DisplayName);
        Assert.Equal("[1]", b.DisplayName);

        var obj = new JsonNodeViewModel(JsonNodeKind.Object);
        var m = new JsonNodeViewModel(JsonNodeKind.String, name: "key", value: "v", parent: obj);
        Assert.Equal("key", m.DisplayName);
    }

    [Fact]
    public void Changed_BubblesFromChildToRoot()
    {
        var root = new JsonNodeViewModel(JsonNodeKind.Object);
        var child = new JsonNodeViewModel(JsonNodeKind.String, name: "k", value: "v", parent: root);
        root.Children.Add(child);

        var fired = 0;
        root.Changed += (_, _) => fired++;
        child.Value = "changed";

        Assert.True(fired >= 1);
    }

    [Fact]
    public void IsObjectMember_TrueOnlyForObjectChildren()
    {
        var obj = new JsonNodeViewModel(JsonNodeKind.Object);
        var member = new JsonNodeViewModel(JsonNodeKind.String, "k", "v", obj);
        obj.Children.Add(member);

        var arr = new JsonNodeViewModel(JsonNodeKind.Array);
        var elem = new JsonNodeViewModel(JsonNodeKind.String, value: "x", parent: arr);
        arr.Children.Add(elem);

        Assert.True(member.IsObjectMember);
        Assert.False(elem.IsObjectMember);
        Assert.False(obj.IsObjectMember); // root
    }

    [Fact]
    public void ApplyFilter_HidesNonMatchingKeys_KeepsMatchAndAncestors()
    {
        var root = new JsonNodeViewModel(JsonNodeKind.Object);
        var user = new JsonNodeViewModel(JsonNodeKind.Object, "user", parent: root);
        root.Children.Add(user);
        var name = new JsonNodeViewModel(JsonNodeKind.String, "name", "Alice", user);
        var age = new JsonNodeViewModel(JsonNodeKind.Number, "age", "30", user);
        user.Children.Add(name);
        user.Children.Add(age);

        var matched = root.ApplyFilter("name");

        Assert.True(matched);
        Assert.False(root.IsFilteredOut);  // ancestor kept
        Assert.False(user.IsFilteredOut);  // ancestor kept
        Assert.False(name.IsFilteredOut);  // match
        Assert.True(age.IsFilteredOut);    // non-match hidden
    }

    [Fact]
    public void ApplyFilter_Empty_ClearsAll()
    {
        var root = new JsonNodeViewModel(JsonNodeKind.Object);
        var a = new JsonNodeViewModel(JsonNodeKind.String, "a", "1", root);
        root.Children.Add(a);
        a.IsFilteredOut = true;

        root.ApplyFilter("");

        Assert.False(a.IsFilteredOut);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter JsonNodeViewModelTests`
Expected: FAIL — types `JsonNodeKind` / `JsonNodeViewModel` do not exist.

- [ ] **Step 3: Write the enum**

Create `src/JsonEditor/Models/JsonNodeKind.cs`:
```csharp
namespace JsonEditor.Models;

public enum JsonNodeKind
{
    Object,
    Array,
    String,
    Number,
    Boolean,
    Null
}
```

- [ ] **Step 4: Write the view-model**

Create `src/JsonEditor/ViewModels/JsonNodeViewModel.cs`:
```csharp
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using JsonEditor.Models;

namespace JsonEditor.ViewModels;

public partial class JsonNodeViewModel : ObservableObject
{
    public JsonNodeViewModel(
        JsonNodeKind kind,
        string? name = null,
        string? value = null,
        JsonNodeViewModel? parent = null)
    {
        _kind = kind;
        _name = name;
        _value = value;
        Parent = parent;
        Children = new ObservableCollection<JsonNodeViewModel>();
        Children.CollectionChanged += OnChildrenChanged;
    }

    public JsonNodeViewModel? Parent { get; }

    public ObservableCollection<JsonNodeViewModel> Children { get; }

    [ObservableProperty] private string? _name;
    [ObservableProperty] private JsonNodeKind _kind;
    [ObservableProperty] private string? _value;
    [ObservableProperty] private bool _isExpanded = true;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isFilteredOut;

    public bool IsContainer => Kind is JsonNodeKind.Object or JsonNodeKind.Array;

    public bool IsObjectMember => Parent is { Kind: JsonNodeKind.Object };

    public string DisplayName
    {
        get
        {
            if (Name is not null) return Name;
            if (Parent is { Kind: JsonNodeKind.Array })
                return $"[{Parent.Children.IndexOf(this)}]";
            return "(root)";
        }
    }

    public event EventHandler? Changed;

    public void RaiseChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
        Parent?.RaiseChanged();
    }

    partial void OnValueChanged(string? value) => RaiseChanged();
    partial void OnNameChanged(string? value) => RaiseChanged();

    partial void OnKindChanged(JsonNodeKind value)
    {
        OnPropertyChanged(nameof(IsContainer));
        RaiseChanged();
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Array element indices shift when the collection changes.
        foreach (var c in Children)
            c.OnPropertyChanged(nameof(DisplayName));
        RaiseChanged();
    }

    // Recursively applies a case-insensitive key filter. Returns true if this node
    // or any descendant matches; clears the filter when text is empty.
    public bool ApplyFilter(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            IsFilteredOut = false;
            foreach (var c in Children) c.ApplyFilter(null);
            return true;
        }

        var selfMatch = Name is not null &&
                        Name.Contains(text, StringComparison.OrdinalIgnoreCase);

        var childMatch = false;
        foreach (var c in Children)
        {
            var m = selfMatch ? ShowSubtree(c) : c.ApplyFilter(text);
            childMatch |= m;
        }

        var matched = selfMatch || childMatch;
        IsFilteredOut = !matched;
        if (matched) IsExpanded = true;
        return matched;
    }

    private static bool ShowSubtree(JsonNodeViewModel n)
    {
        n.IsFilteredOut = false;
        foreach (var c in n.Children) ShowSubtree(c);
        return true;
    }

    // Expose protected OnPropertyChanged for sibling index refresh.
    internal new void OnPropertyChanged(string propertyName) => base.OnPropertyChanged(propertyName);
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter JsonNodeViewModelTests`
Expected: PASS (6 tests).

- [ ] **Step 6: Checkpoint** — build + tests green.

---

### Task 3: `JsonDocumentService.Parse` (JSON text → VM tree)

**Files:**
- Create: `src/JsonEditor/Services/JsonDocumentService.cs`
- Test: `tests/JsonEditor.Tests/JsonParseTests.cs`

**Interfaces:**
- Consumes: `JsonNodeViewModel`, `JsonNodeKind`.
- Produces:
  - `class JsonDocumentService`
  - `JsonNodeViewModel Parse(string json)` — builds a VM tree; throws `System.Text.Json.JsonException` on invalid JSON. Root has `Name == null`.

- [ ] **Step 1: Write the failing test**

Create `tests/JsonEditor.Tests/JsonParseTests.cs`:
```csharp
using System.Text.Json;
using JsonEditor.Models;
using JsonEditor.Services;

namespace JsonEditor.Tests;

public class JsonParseTests
{
    private readonly JsonDocumentService _svc = new();

    [Fact]
    public void Parse_Object_WithEachScalarKind()
    {
        var root = _svc.Parse("""
            { "s": "hi", "n": 30, "b": true, "z": null }
            """);

        Assert.Equal(JsonNodeKind.Object, root.Kind);
        Assert.Equal(4, root.Children.Count);

        Assert.Equal(JsonNodeKind.String, root.Children[0].Kind);
        Assert.Equal("hi", root.Children[0].Value);

        Assert.Equal(JsonNodeKind.Number, root.Children[1].Kind);
        Assert.Equal("30", root.Children[1].Value);

        Assert.Equal(JsonNodeKind.Boolean, root.Children[2].Kind);
        Assert.Equal("true", root.Children[2].Value);

        Assert.Equal(JsonNodeKind.Null, root.Children[3].Kind);
        Assert.Null(root.Children[3].Value);
    }

    [Fact]
    public void Parse_NestedArrayAndObject()
    {
        var root = _svc.Parse("""{ "list": [1, {"x": "y"}] }""");
        var list = root.Children[0];
        Assert.Equal(JsonNodeKind.Array, list.Kind);
        Assert.Equal(2, list.Children.Count);
        Assert.Equal(JsonNodeKind.Number, list.Children[0].Kind);
        Assert.Equal(JsonNodeKind.Object, list.Children[1].Kind);
        Assert.Equal("x", list.Children[1].Children[0].Name);
    }

    [Fact]
    public void Parse_PreservesKeyOrder()
    {
        var root = _svc.Parse("""{ "z": 1, "a": 2, "m": 3 }""");
        Assert.Equal(new[] { "z", "a", "m" }, root.Children.Select(c => c.Name).ToArray());
    }

    [Fact]
    public void Parse_InvalidJson_Throws()
    {
        Assert.Throws<JsonException>(() => _svc.Parse("{ not json "));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter JsonParseTests`
Expected: FAIL — `JsonDocumentService` does not exist.

- [ ] **Step 3: Write the parser**

Create `src/JsonEditor/Services/JsonDocumentService.cs`:
```csharp
using System.Text.Json;
using JsonEditor.Models;
using JsonEditor.ViewModels;

namespace JsonEditor.Services;

public class JsonDocumentService
{
    public JsonNodeViewModel Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        return Build(doc.RootElement, name: null, parent: null);
    }

    private static JsonNodeViewModel Build(JsonElement el, string? name, JsonNodeViewModel? parent)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var node = new JsonNodeViewModel(JsonNodeKind.Object, name, parent: parent);
                foreach (var prop in el.EnumerateObject())
                    node.Children.Add(Build(prop.Value, prop.Name, node));
                return node;
            }
            case JsonValueKind.Array:
            {
                var node = new JsonNodeViewModel(JsonNodeKind.Array, name, parent: parent);
                foreach (var item in el.EnumerateArray())
                    node.Children.Add(Build(item, null, node));
                return node;
            }
            case JsonValueKind.String:
                return new JsonNodeViewModel(JsonNodeKind.String, name, el.GetString(), parent);
            case JsonValueKind.Number:
                return new JsonNodeViewModel(JsonNodeKind.Number, name, el.GetRawText(), parent);
            case JsonValueKind.True:
                return new JsonNodeViewModel(JsonNodeKind.Boolean, name, "true", parent);
            case JsonValueKind.False:
                return new JsonNodeViewModel(JsonNodeKind.Boolean, name, "false", parent);
            default: // Null / Undefined
                return new JsonNodeViewModel(JsonNodeKind.Null, name, null, parent);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter JsonParseTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Checkpoint** — build + tests green.

---

### Task 4: `JsonDocumentService.Serialize` + round-trip

**Files:**
- Modify: `src/JsonEditor/Services/JsonDocumentService.cs`
- Test: `tests/JsonEditor.Tests/JsonSerializeTests.cs`

**Interfaces:**
- Consumes: `Parse` (Task 3), `JsonNodeViewModel`.
- Produces: `string Serialize(JsonNodeViewModel root)` — UTF-8 pretty JSON, 2-space indent, preserving key order and raw number text.

- [ ] **Step 1: Write the failing test**

Create `tests/JsonEditor.Tests/JsonSerializeTests.cs`:
```csharp
using JsonEditor.Models;
using JsonEditor.Services;
using JsonEditor.ViewModels;

namespace JsonEditor.Tests;

public class JsonSerializeTests
{
    private readonly JsonDocumentService _svc = new();

    [Fact]
    public void Serialize_UsesTwoSpaceIndent()
    {
        var root = new JsonNodeViewModel(JsonNodeKind.Object);
        root.Children.Add(new JsonNodeViewModel(JsonNodeKind.String, "k", "v", root));
        var json = _svc.Serialize(root);
        Assert.Contains("\n  \"k\": \"v\"", json.Replace("\r\n", "\n"));
    }

    [Fact]
    public void RoundTrip_PreservesData_Order_And_NumberText()
    {
        const string input = """{"z":"hi","n":30,"big":12345678901234567890,"b":false,"arr":[1,null,"x"]}""";
        var root = _svc.Parse(input);
        var output = _svc.Serialize(root);

        // Re-parse output and compare structure/values.
        var reparsed = _svc.Parse(output);
        Assert.Equal(new[] { "z", "n", "big", "b", "arr" }, reparsed.Children.Select(c => c.Name).ToArray());
        Assert.Equal("12345678901234567890", reparsed.Children[2].Value); // big number text preserved
        Assert.Equal(JsonNodeKind.Null, reparsed.Children[4].Children[1].Kind);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter JsonSerializeTests`
Expected: FAIL — `Serialize` not defined.

- [ ] **Step 3: Implement Serialize**

Add to `src/JsonEditor/Services/JsonDocumentService.cs` (inside the class), and add `using System.Text;` and `using System.Globalization;` at the top:
```csharp
public string Serialize(JsonNodeViewModel root)
{
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
           {
               Indented = true,
               IndentCharacter = ' ',
               IndentSize = 2
           }))
    {
        Write(writer, root);
    }
    return Encoding.UTF8.GetString(stream.ToArray());
}

private static void Write(Utf8JsonWriter w, JsonNodeViewModel node)
{
    switch (node.Kind)
    {
        case JsonNodeKind.Object:
            w.WriteStartObject();
            foreach (var c in node.Children)
            {
                w.WritePropertyName(c.Name ?? string.Empty);
                Write(w, c);
            }
            w.WriteEndObject();
            break;
        case JsonNodeKind.Array:
            w.WriteStartArray();
            foreach (var c in node.Children)
                Write(w, c);
            w.WriteEndArray();
            break;
        case JsonNodeKind.String:
            w.WriteStringValue(node.Value ?? string.Empty);
            break;
        case JsonNodeKind.Number:
            // WriteRawValue validates by default and would throw on invalid text,
            // so fall back to 0 for empty/invalid numbers to keep Save crash-free.
            w.WriteRawValue(IsValidJsonNumber(node.Value) ? node.Value! : "0");
            break;
        case JsonNodeKind.Boolean:
            w.WriteBooleanValue(string.Equals(node.Value, "true", StringComparison.OrdinalIgnoreCase));
            break;
        default:
            w.WriteNullValue();
            break;
    }
}

private static bool IsValidJsonNumber(string? s)
    => !string.IsNullOrWhiteSpace(s)
       && double.TryParse(s, NumberStyles.Float | NumberStyles.AllowLeadingSign,
              CultureInfo.InvariantCulture, out var d)
       && double.IsFinite(d);
```

> Note: `IndentCharacter` / `IndentSize` on `JsonWriterOptions` require .NET 9+. We target .NET 10, so they are available.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter JsonSerializeTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Checkpoint** — build + tests green.

---

### Task 5: Edit operations on `JsonNodeViewModel`

**Files:**
- Modify: `src/JsonEditor/ViewModels/JsonNodeViewModel.cs`
- Test: `tests/JsonEditor.Tests/JsonNodeEditTests.cs`

**Interfaces:**
- Consumes: `JsonNodeViewModel`, `JsonNodeKind`.
- Produces methods on `JsonNodeViewModel`:
  - `JsonNodeViewModel AddChild(JsonNodeKind kind)` — valid only on containers; for Object assigns a unique key (`newKey`, `newKey2`, …), for Array `Name == null`; returns the new child.
  - `void Delete()` — removes this node from `Parent.Children` (no-op if root).
  - `void Rename(string newName)` — sets `Name` (intended for object members).
  - `void ChangeKind(JsonNodeKind newKind)` — converts in place (see rules below).

- [ ] **Step 1: Write the failing test**

Create `tests/JsonEditor.Tests/JsonNodeEditTests.cs`:
```csharp
using JsonEditor.Models;
using JsonEditor.ViewModels;

namespace JsonEditor.Tests;

public class JsonNodeEditTests
{
    [Fact]
    public void AddChild_ToObject_AssignsUniqueKeys()
    {
        var obj = new JsonNodeViewModel(JsonNodeKind.Object);
        var a = obj.AddChild(JsonNodeKind.String);
        var b = obj.AddChild(JsonNodeKind.String);
        Assert.Equal("newKey", a.Name);
        Assert.Equal("newKey2", b.Name);
        Assert.Equal(2, obj.Children.Count);
    }

    [Fact]
    public void AddChild_ToArray_LeavesNameNull()
    {
        var arr = new JsonNodeViewModel(JsonNodeKind.Array);
        var item = arr.AddChild(JsonNodeKind.Number);
        Assert.Null(item.Name);
        Assert.Equal(JsonNodeKind.Number, item.Kind);
    }

    [Fact]
    public void Delete_RemovesFromParent()
    {
        var obj = new JsonNodeViewModel(JsonNodeKind.Object);
        var a = obj.AddChild(JsonNodeKind.String);
        a.Delete();
        Assert.Empty(obj.Children);
    }

    [Fact]
    public void ChangeKind_StringToNumber_KeepsParseableValue()
    {
        var n = new JsonNodeViewModel(JsonNodeKind.String, value: "42");
        n.ChangeKind(JsonNodeKind.Number);
        Assert.Equal(JsonNodeKind.Number, n.Kind);
        Assert.Equal("42", n.Value);
    }

    [Fact]
    public void ChangeKind_StringToNumber_NonNumericResetsToZero()
    {
        var n = new JsonNodeViewModel(JsonNodeKind.String, value: "hello");
        n.ChangeKind(JsonNodeKind.Number);
        Assert.Equal("0", n.Value);
    }

    [Fact]
    public void ChangeKind_ToContainer_ClearsValueAndChildren()
    {
        var n = new JsonNodeViewModel(JsonNodeKind.String, value: "x");
        n.ChangeKind(JsonNodeKind.Object);
        Assert.Equal(JsonNodeKind.Object, n.Kind);
        Assert.Null(n.Value);
        Assert.Empty(n.Children);
    }

    [Fact]
    public void ChangeKind_ToBoolean_TruthyMapping()
    {
        var n = new JsonNodeViewModel(JsonNodeKind.String, value: "true");
        n.ChangeKind(JsonNodeKind.Boolean);
        Assert.Equal("true", n.Value);

        var m = new JsonNodeViewModel(JsonNodeKind.String, value: "nope");
        m.ChangeKind(JsonNodeKind.Boolean);
        Assert.Equal("false", m.Value);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter JsonNodeEditTests`
Expected: FAIL — `AddChild` / `Delete` / `ChangeKind` not defined.

- [ ] **Step 3: Implement edit operations**

Add to `src/JsonEditor/ViewModels/JsonNodeViewModel.cs` (inside the class). Add `using System.Globalization;` at the top:
```csharp
public JsonNodeViewModel AddChild(JsonNodeKind kind)
{
    if (!IsContainer)
        throw new InvalidOperationException("Cannot add a child to a non-container node.");

    string? childName = null;
    if (Kind == JsonNodeKind.Object)
    {
        childName = "newKey";
        var i = 2;
        while (Children.Any(c => c.Name == childName))
            childName = $"newKey{i++}";
    }

    var value = kind switch
    {
        JsonNodeKind.String => string.Empty,
        JsonNodeKind.Number => "0",
        JsonNodeKind.Boolean => "false",
        _ => (string?)null
    };

    var child = new JsonNodeViewModel(kind, childName, value, this);
    Children.Add(child);
    return child;
}

public void Delete() => Parent?.Children.Remove(this);

public void Rename(string newName) => Name = newName;

public void ChangeKind(JsonNodeKind newKind)
{
    if (newKind == Kind) return;

    switch (newKind)
    {
        case JsonNodeKind.String:
            Value = Value ?? string.Empty;
            Children.Clear();
            break;
        case JsonNodeKind.Number:
            Value = double.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _)
                ? Value
                : "0";
            Children.Clear();
            break;
        case JsonNodeKind.Boolean:
            Value = string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
            Children.Clear();
            break;
        case JsonNodeKind.Null:
            Value = null;
            Children.Clear();
            break;
        case JsonNodeKind.Object:
        case JsonNodeKind.Array:
            Value = null;
            Children.Clear();
            break;
    }

    Kind = newKind;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter JsonNodeEditTests`
Expected: PASS (7 tests).

- [ ] **Step 5: Checkpoint** — build + tests green.

---

### Task 6: File load/save

**Files:**
- Modify: `src/JsonEditor/Services/JsonDocumentService.cs`
- Test: `tests/JsonEditor.Tests/JsonFileTests.cs`

**Interfaces:**
- Consumes: `Parse`, `Serialize`.
- Produces:
  - `JsonNodeViewModel Load(string path)` — reads file text, returns VM tree (propagates `JsonException` on invalid content).
  - `void Save(JsonNodeViewModel root, string path)` — serializes and writes UTF-8 (no BOM).

- [ ] **Step 1: Write the failing test**

Create `tests/JsonEditor.Tests/JsonFileTests.cs`:
```csharp
using JsonEditor.Models;
using JsonEditor.Services;

namespace JsonEditor.Tests;

public class JsonFileTests
{
    private readonly JsonDocumentService _svc = new();

    [Fact]
    public void Save_Then_Load_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"jsoneditor_{Guid.NewGuid():N}.json");
        try
        {
            var root = _svc.Parse("""{ "a": 1, "b": ["x", true] }""");
            _svc.Save(root, path);

            Assert.True(File.Exists(path));
            var loaded = _svc.Load(path);
            Assert.Equal(new[] { "a", "b" }, loaded.Children.Select(c => c.Name).ToArray());
            Assert.Equal(JsonNodeKind.Boolean, loaded.Children[1].Children[1].Kind);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter JsonFileTests`
Expected: FAIL — `Load` / `Save` not defined.

- [ ] **Step 3: Implement Load/Save**

Add to `src/JsonEditor/Services/JsonDocumentService.cs`:
```csharp
public JsonNodeViewModel Load(string path) => Parse(File.ReadAllText(path));

public void Save(JsonNodeViewModel root, string path)
{
    var json = Serialize(root);
    File.WriteAllText(path, json, new System.Text.UTF8Encoding(false));
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter JsonFileTests`
Expected: PASS.

- [ ] **Step 5: Checkpoint** — build + tests green.

---

### Task 7: `MainViewModel` with dialog/prompt abstractions

**Files:**
- Create: `src/JsonEditor/Services/IFileDialogService.cs`
- Create: `src/JsonEditor/Services/IUserPrompt.cs`
- Create: `src/JsonEditor/ViewModels/MainViewModel.cs`
- Test: `tests/JsonEditor.Tests/MainViewModelTests.cs`

**Interfaces:**
- Consumes: `JsonDocumentService`, `JsonNodeViewModel`.
- Produces:
  - `interface IFileDialogService { string? OpenFile(); string? SaveFile(string? suggestedName); }`
  - `enum PromptResult { Yes, No, Cancel }`
  - `interface IUserPrompt { void Error(string message); PromptResult ConfirmDiscard(); }`
  - `class MainViewModel : ObservableObject` with:
    - ctor `MainViewModel(JsonDocumentService svc, IFileDialogService dialogs, IUserPrompt prompt)`
    - `ObservableCollection<JsonNodeViewModel> Roots { get; }` (single root, wrapped so the `TreeView` shows one top node)
    - `string? CurrentPath { get; }`, `bool IsDirty { get; }`, `string? FilterText { get; set; }`, `string StatusText { get; }`
    - commands: `OpenCommand`, `SaveCommand`, `SaveAsCommand`, `AddChildCommand`, `DeleteNodeCommand`
    - `void OpenPath(string path)` — load file, replace root, clear dirty (used by both dialog + tests)

- [ ] **Step 1: Write the failing test**

Create `tests/JsonEditor.Tests/MainViewModelTests.cs`:
```csharp
using JsonEditor.Models;
using JsonEditor.Services;
using JsonEditor.ViewModels;

namespace JsonEditor.Tests;

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
        return new MainViewModel(new JsonDocumentService(), d, p);
    }

    [Fact]
    public void OpenPath_LoadsRoot_AndIsNotDirty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mvm_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "a": 1 }""");
        try
        {
            var vm = Make(out _, out _);
            vm.OpenPath(path);
            Assert.Single(vm.Roots);
            Assert.Equal(JsonNodeKind.Object, vm.Roots[0].Kind);
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
            vm.Roots[0].Children[0].Value = "2";
            Assert.True(vm.IsDirty);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Open_InvalidJson_ShowsError()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mvm_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ broken ");
        try
        {
            var vm = Make(out var d, out var p);
            d.OpenPath = path;
            vm.OpenCommand.Execute(null);
            Assert.NotNull(p.LastError);
            Assert.Empty(vm.Roots);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void FilterText_AppliesKeyFilterToTree()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mvm_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "name": "Alice", "age": 30 }""");
        try
        {
            var vm = Make(out _, out _);
            vm.OpenPath(path);
            vm.FilterText = "age";

            var name = vm.Roots[0].Children.First(c => c.Name == "name");
            var age = vm.Roots[0].Children.First(c => c.Name == "age");
            Assert.True(name.IsFilteredOut);
            Assert.False(age.IsFilteredOut);
            Assert.False(vm.IsDirty); // filtering must not dirty the document
        }
        finally { File.Delete(path); }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter MainViewModelTests`
Expected: FAIL — types not defined.

- [ ] **Step 3: Write the service interfaces**

Create `src/JsonEditor/Services/IFileDialogService.cs`:
```csharp
namespace JsonEditor.Services;

public interface IFileDialogService
{
    string? OpenFile();
    string? SaveFile(string? suggestedName);
}
```

Create `src/JsonEditor/Services/IUserPrompt.cs`:
```csharp
namespace JsonEditor.Services;

public enum PromptResult { Yes, No, Cancel }

public interface IUserPrompt
{
    void Error(string message);
    PromptResult ConfirmDiscard();
}
```

- [ ] **Step 4: Write `MainViewModel`**

Create `src/JsonEditor/ViewModels/MainViewModel.cs`:
```csharp
using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JsonEditor.Models;
using JsonEditor.Services;

namespace JsonEditor.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly JsonDocumentService _svc;
    private readonly IFileDialogService _dialogs;
    private readonly IUserPrompt _prompt;

    public MainViewModel(JsonDocumentService svc, IFileDialogService dialogs, IUserPrompt prompt)
    {
        _svc = svc;
        _dialogs = dialogs;
        _prompt = prompt;
        Roots = new ObservableCollection<JsonNodeViewModel>();
    }

    public ObservableCollection<JsonNodeViewModel> Roots { get; }

    [ObservableProperty] private string? _currentPath;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string? _filterText;

    public JsonNodeViewModel? Root => Roots.Count > 0 ? Roots[0] : null;

    public string StatusText =>
        (CurrentPath ?? "No file") + (IsDirty ? "  ●" : string.Empty);

    partial void OnCurrentPathChanged(string? value) => OnPropertyChanged(nameof(StatusText));
    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(StatusText));
    partial void OnFilterTextChanged(string? value) => Root?.ApplyFilter(value);

    public void OpenPath(string path)
    {
        var root = _svc.Load(path);
        SetRoot(root);
        CurrentPath = path;
        IsDirty = false;
    }

    private void SetRoot(JsonNodeViewModel root)
    {
        if (Root is not null) Root.Changed -= OnTreeChanged;
        Roots.Clear();
        Roots.Add(root);
        root.Changed += OnTreeChanged;
    }

    private void OnTreeChanged(object? sender, EventArgs e) => IsDirty = true;

    // Returns true if it is safe to proceed (no changes, or user chose to discard).
    // Public so the window's Closing handler can guard against losing unsaved edits.
    public bool ConfirmDiscardIfDirty()
    {
        if (!IsDirty) return true;
        return _prompt.ConfirmDiscard() switch
        {
            PromptResult.Yes => true,
            _ => false
        };
    }

    [RelayCommand]
    private void Open()
    {
        if (!ConfirmDiscardIfDirty()) return;
        var path = _dialogs.OpenFile();
        if (path is null) return;
        try
        {
            OpenPath(path);
        }
        catch (JsonException ex)
        {
            _prompt.Error($"Could not parse JSON:\n{ex.Message}");
        }
        catch (IOException ex)
        {
            _prompt.Error($"Could not open file:\n{ex.Message}");
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (Root is null) return;
        if (CurrentPath is null) { SaveAs(); return; }
        Persist(CurrentPath);
    }

    [RelayCommand]
    private void SaveAs()
    {
        if (Root is null) return;
        var path = _dialogs.SaveFile(CurrentPath);
        if (path is null) return;
        Persist(path);
    }

    private void Persist(string path)
    {
        try
        {
            _svc.Save(Root!, path);
            CurrentPath = path;
            IsDirty = false;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _prompt.Error($"Could not save file:\n{ex.Message}");
        }
    }

    [RelayCommand]
    private void AddChild(JsonNodeViewModel? node)
    {
        if (node is { IsContainer: true })
            node.AddChild(JsonNodeKind.String);
    }

    [RelayCommand]
    private void DeleteNode(JsonNodeViewModel? node)
    {
        if (node is not null && node.Parent is not null)
            node.Delete();
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter MainViewModelTests`
Expected: PASS (4 tests). Then run full suite: `dotnet test JSONEditor.sln` — all green.

- [ ] **Step 6: Checkpoint** — build + full test suite green.

---

### Task 8: WPF shell — App bootstrap, MainWindow, type-aware templates, Fluent theme

**Files:**
- Modify: `src/JsonEditor/App.xaml`
- Modify: `src/JsonEditor/App.xaml.cs`
- Modify: `src/JsonEditor/MainWindow.xaml`
- Modify: `src/JsonEditor/MainWindow.xaml.cs`
- Create: `src/JsonEditor/Services/FileDialogService.cs`
- Create: `src/JsonEditor/Services/MessageBoxPrompt.cs`
- Create: `src/JsonEditor/Converters/Converters.cs`

**Interfaces:**
- Consumes: `MainViewModel`, `IFileDialogService`, `IUserPrompt`, `JsonDocumentService`.
- Produces: the runnable WPF UI. Verified by running the app (manual), not unit tests.

- [ ] **Step 1: Implement the real dialog + prompt services**

Create `src/JsonEditor/Services/FileDialogService.cs`:
```csharp
using Microsoft.Win32;

namespace JsonEditor.Services;

public class FileDialogService : IFileDialogService
{
    private const string Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";

    public string? OpenFile()
    {
        var dlg = new OpenFileDialog { Filter = Filter, CheckFileExists = true };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? SaveFile(string? suggestedName)
    {
        var dlg = new SaveFileDialog
        {
            Filter = Filter,
            FileName = suggestedName is null ? "data.json" : System.IO.Path.GetFileName(suggestedName),
            DefaultExt = ".json"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
```

Create `src/JsonEditor/Services/MessageBoxPrompt.cs`:
```csharp
using System.Windows;

namespace JsonEditor.Services;

public class MessageBoxPrompt : IUserPrompt
{
    public void Error(string message) =>
        MessageBox.Show(message, "JSON Editor", MessageBoxButton.OK, MessageBoxImage.Error);

    public PromptResult ConfirmDiscard()
    {
        var r = MessageBox.Show(
            "You have unsaved changes. Discard them?",
            "JSON Editor",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        return r == MessageBoxResult.Yes ? PromptResult.Yes : PromptResult.Cancel;
    }
}
```

- [ ] **Step 2: Add value converters (kind→visibility, string↔bool, bool→visibility, inverse)**

Create `src/JsonEditor/Converters/Converters.cs`:
```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using JsonEditor.Models;

namespace JsonEditor.Converters;

// Visible when the bound JsonNodeKind equals the kind named in ConverterParameter.
public class KindToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? parameter, CultureInfo c)
        => value is JsonNodeKind k && parameter is string s && k.ToString() == s
            ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

// Two-way bridge between a "true"/"false" string Value and a bool (for ToggleSwitch.IsChecked).
public class StringBoolConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
        => string.Equals(value as string, "true", StringComparison.OrdinalIgnoreCase);
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c)
        => value is true ? "true" : "false";
}

// Visible when the bound bool is TRUE (editable key box for object members).
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
        => value is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

// Visible when the bound bool is FALSE (index/root label, and showing non-filtered nodes).
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
        => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}
```

- [ ] **Step 3: Wire App bootstrap with WPF-UI theme**

Replace `src/JsonEditor/App.xaml` with:
```xml
<Application x:Class="JsonEditor.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ui:ThemesDictionary Theme="Dark" />
                <ui:ControlsDictionary />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

Replace `src/JsonEditor/App.xaml.cs` with:
```csharp
using System.Windows;
using JsonEditor.Services;
using JsonEditor.ViewModels;

namespace JsonEditor;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var vm = new MainViewModel(new JsonDocumentService(), new FileDialogService(), new MessageBoxPrompt());
        var window = new MainWindow { DataContext = vm };
        window.Show();
    }
}
```

If `App.xaml` has `StartupUri="MainWindow.xaml"`, remove that attribute (we start the window in code).

- [ ] **Step 4: Build the MainWindow with toolbar, tree, type-aware templates, status bar**

Replace `src/JsonEditor/MainWindow.xaml` with:
```xml
<ui:FluentWindow x:Class="JsonEditor.MainWindow"
    x:Name="Root"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
    xmlns:vm="clr-namespace:JsonEditor.ViewModels"
    xmlns:conv="clr-namespace:JsonEditor.Converters"
    Title="JSON Editor" Width="900" Height="650"
    WindowBackdropType="Mica" ExtendsContentIntoTitleBar="True">

    <Window.Resources>
        <conv:KindToVisibilityConverter x:Key="KindToVis" />
        <conv:StringBoolConverter x:Key="StrBool" />
        <conv:BoolToVisibilityConverter x:Key="BoolToVis" />
        <conv:InverseBoolToVisibilityConverter x:Key="InverseBoolToVis" />

        <HierarchicalDataTemplate DataType="{x:Type vm:JsonNodeViewModel}" ItemsSource="{Binding Children}">
            <StackPanel Orientation="Horizontal" Margin="0,2">
                <!-- Key: editable TextBox for object members, read-only label otherwise (array index / root) -->
                <TextBox Text="{Binding Name, UpdateSourceTrigger=LostFocus}" MinWidth="90" Margin="0,0,8,0"
                         Visibility="{Binding IsObjectMember, Converter={StaticResource BoolToVis}}"/>
                <TextBlock Text="{Binding DisplayName}" VerticalAlignment="Center"
                           FontWeight="SemiBold" Margin="0,0,8,0" MinWidth="60"
                           Visibility="{Binding IsObjectMember, Converter={StaticResource InverseBoolToVis}}"/>

                <!-- String -->
                <TextBox Text="{Binding Value, UpdateSourceTrigger=PropertyChanged}" MinWidth="160"
                         Visibility="{Binding Kind, Converter={StaticResource KindToVis}, ConverterParameter=String}"/>
                <!-- Number -->
                <TextBox Text="{Binding Value, UpdateSourceTrigger=PropertyChanged}" MinWidth="100"
                         Visibility="{Binding Kind, Converter={StaticResource KindToVis}, ConverterParameter=Number}"/>
                <!-- Boolean (two-way via "true"/"false" string bridge) -->
                <ui:ToggleSwitch IsChecked="{Binding Value, Converter={StaticResource StrBool}, Mode=TwoWay}"
                                 Visibility="{Binding Kind, Converter={StaticResource KindToVis}, ConverterParameter=Boolean}"/>
                <!-- Null -->
                <TextBlock Text="null" Opacity="0.6" VerticalAlignment="Center"
                           Visibility="{Binding Kind, Converter={StaticResource KindToVis}, ConverterParameter=Null}"/>
                <!-- Object / Array child-count hints -->
                <TextBlock VerticalAlignment="Center" Opacity="0.6"
                           Text="{Binding Children.Count, StringFormat='{}{{ {0} }}'}"
                           Visibility="{Binding Kind, Converter={StaticResource KindToVis}, ConverterParameter=Object}"/>
                <TextBlock VerticalAlignment="Center" Opacity="0.6"
                           Text="{Binding Children.Count, StringFormat='[ {0} ]'}"
                           Visibility="{Binding Kind, Converter={StaticResource KindToVis}, ConverterParameter=Array}"/>
            </StackPanel>
        </HierarchicalDataTemplate>
    </Window.Resources>

    <Grid Margin="12,40,12,12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Toolbar -->
        <StackPanel Orientation="Horizontal" Grid.Row="0" Margin="0,0,0,8">
            <ui:Button Content="Open" Icon="{ui:SymbolIcon FolderOpen24}" Command="{Binding OpenCommand}" Margin="0,0,8,0"/>
            <ui:Button Content="Save" Icon="{ui:SymbolIcon Save24}" Command="{Binding SaveCommand}" Margin="0,0,8,0"/>
            <ui:Button Content="Save As" Command="{Binding SaveAsCommand}" Margin="0,0,16,0"/>
            <ui:TextBox PlaceholderText="Filter keys..." Width="200"
                        Text="{Binding FilterText, UpdateSourceTrigger=PropertyChanged}"/>
        </StackPanel>

        <!-- Tree -->
        <ui:Card Grid.Row="1">
            <TreeView ItemsSource="{Binding Roots}" Background="Transparent" BorderThickness="0">
                <TreeView.ItemContainerStyle>
                    <Style TargetType="TreeViewItem">
                        <Setter Property="IsExpanded" Value="{Binding IsExpanded, Mode=TwoWay}"/>
                        <Setter Property="IsSelected" Value="{Binding IsSelected, Mode=TwoWay}"/>
                        <Setter Property="Visibility"
                                Value="{Binding IsFilteredOut, Converter={StaticResource InverseBoolToVis}}"/>
                    </Style>
                </TreeView.ItemContainerStyle>
            </TreeView>
        </ui:Card>

        <!-- Status bar -->
        <TextBlock Grid.Row="2" Margin="4,8,0,0" Opacity="0.7" Text="{Binding StatusText}"/>
    </Grid>
</ui:FluentWindow>
```

Replace `src/JsonEditor/MainWindow.xaml.cs` with:
```csharp
using System.ComponentModel;
using JsonEditor.ViewModels;
using Wpf.Ui.Controls;

namespace JsonEditor;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    // Guard against closing with unsaved changes (spec §6).
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm && !vm.ConfirmDiscardIfDirty())
            e.Cancel = true;
    }
}
```

- [ ] **Step 5: Build and run the app**

Run: `dotnet build JSONEditor.sln`
Expected: build succeeds with no errors.

Run: `dotnet run --project src/JsonEditor/JsonEditor.csproj`
Expected: a dark Fluent window titled "JSON Editor" opens with Open/Save/Save As buttons and an empty tree.

- [ ] **Step 6: Manual smoke test**

Create a sample file `samples/sample.json` (create the `samples` folder):
```json
{
  "name": "Alice",
  "age": 30,
  "admin": true,
  "roles": ["editor", "viewer"],
  "note": null
}
```
In the running app: click **Open**, choose `samples/sample.json`. Verify the tree shows the object with string/number/boolean/array/null nodes rendered with the right controls. Edit `name`, confirm the status bar shows the dirty `●`. Click **Save**, reopen, confirm the change persisted.

- [ ] **Step 7: Checkpoint** — app builds, runs, and the manual smoke test passes.

---

### Task 9: Type-change & add/delete UI affordances (context menu)

**Files:**
- Modify: `src/JsonEditor/MainWindow.xaml`

**Interfaces:**
- Consumes: `MainViewModel.AddChildCommand`, `DeleteNodeCommand`, and `JsonNodeViewModel.ChangeKind` (exposed via a small command added here).

- [ ] **Step 1: Add per-kind "change type" relay commands to MainViewModel**

XAML menu items pass the bound node as `CommandParameter`, so add one command per target kind. Add to `src/JsonEditor/ViewModels/MainViewModel.cs`:
```csharp
[RelayCommand] private void MakeString(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.String);
[RelayCommand] private void MakeNumber(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.Number);
[RelayCommand] private void MakeBoolean(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.Boolean);
[RelayCommand] private void MakeNull(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.Null);
[RelayCommand] private void MakeObject(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.Object);
[RelayCommand] private void MakeArray(JsonNodeViewModel? n) => n?.ChangeKind(JsonNodeKind.Array);
```

- [ ] **Step 2: Add a context menu to tree items**

In `src/JsonEditor/MainWindow.xaml`, inside the `StackPanel` of the `HierarchicalDataTemplate`, add a context menu. The window already has `x:Name="Root"` (added in Task 8), so bind menu commands to its DataContext (the `MainViewModel`) via `{x:Reference Root}`:
```xml
<StackPanel.ContextMenu>
    <ContextMenu>
        <MenuItem Header="Add child"
                  Command="{Binding DataContext.AddChildCommand, Source={x:Reference Root}}"
                  CommandParameter="{Binding}"/>
        <MenuItem Header="Delete"
                  Command="{Binding DataContext.DeleteNodeCommand, Source={x:Reference Root}}"
                  CommandParameter="{Binding}"/>
        <Separator/>
        <MenuItem Header="Change type">
            <MenuItem Header="String"  Command="{Binding DataContext.MakeStringCommand,  Source={x:Reference Root}}" CommandParameter="{Binding}"/>
            <MenuItem Header="Number"  Command="{Binding DataContext.MakeNumberCommand,  Source={x:Reference Root}}" CommandParameter="{Binding}"/>
            <MenuItem Header="Boolean" Command="{Binding DataContext.MakeBooleanCommand, Source={x:Reference Root}}" CommandParameter="{Binding}"/>
            <MenuItem Header="Null"    Command="{Binding DataContext.MakeNullCommand,    Source={x:Reference Root}}" CommandParameter="{Binding}"/>
            <MenuItem Header="Object"  Command="{Binding DataContext.MakeObjectCommand,  Source={x:Reference Root}}" CommandParameter="{Binding}"/>
            <MenuItem Header="Array"   Command="{Binding DataContext.MakeArrayCommand,   Source={x:Reference Root}}" CommandParameter="{Binding}"/>
        </MenuItem>
    </ContextMenu>
</StackPanel.ContextMenu>
```

- [ ] **Step 3: Build and run**

Run: `dotnet build JSONEditor.sln` then `dotnet run --project src/JsonEditor/JsonEditor.csproj`
Expected: build succeeds; right-clicking a node shows Add child / Delete / Change type.

- [ ] **Step 4: Manual verification**

Open `samples/sample.json`. Right-click `roles` → Add child (adds a string item). Right-click `age` → Change type → Boolean (value becomes `false`). Right-click a node → Delete. Save and reopen to confirm changes persist as valid JSON.

- [ ] **Step 5: Checkpoint** — app builds, runs, edit affordances work end-to-end.

---

## Notes for the implementer

- If `WPF-UI` package XML namespace differs by version, the schema URL `http://schemas.lepo.co/wpfui/2022/xaml` is correct for WPF-UI 3.x. If a control name (`FluentWindow`, `ui:Card`, `ui:Button`, `ui:ToggleSwitch`, `SymbolIcon`) errors, check the installed WPF-UI version's docs and adjust; the view-model layer is unaffected.
- The boolean editor is a two-way `ui:ToggleSwitch` bridged to the `"true"`/`"false"` string `Value` via `StringBoolConverter`.
- Key filtering is fully wired: `MainViewModel.FilterText` → `Root.ApplyFilter` sets each node's `IsFilteredOut`, and the `TreeViewItem.Visibility` setter hides filtered nodes. Filtering never marks the document dirty.
- Number fields accept free text; invalid/empty numbers serialize as `0` (guarded in `JsonDocumentService`). A red-border `ValidationRule` is a possible polish item but not required for v1.

## Implementation deviations (recorded during execution, 2026-06-29)

- **Solution format:** the .NET 10 SDK created `JSONEditor.slnx` (new XML solution format), not `JSONEditor.sln`. All `dotnet` commands use `JSONEditor.slnx`.
- **WPF template framework flag:** `dotnet new wpf` rejects `-f net10.0-windows`; use `-f net10.0` (the template emits `net10.0-windows` itself). The xUnit project was created with `-f net10.0` then retargeted to `net10.0-windows` so it can reference the WPF project.
- **Parse error test:** `System.Text.Json` throws an internal subclass of `JsonException`, so the test uses `Assert.ThrowsAny<JsonException>` rather than `Assert.Throws<JsonException>`.
- **WPF-UI version:** resolved to 4.3.0 (newer than the plan's assumed 3.x). All referenced controls (`FluentWindow`, `Card`, `Button`, `ToggleSwitch`, `TitleBar`, `TextBox`, theme dictionaries) compiled unchanged. Added a `ui:TitleBar` so the extended-into-titlebar window keeps working caption buttons.
- **No git:** per the user's global rule and the non-repo workspace, no commits were made.
