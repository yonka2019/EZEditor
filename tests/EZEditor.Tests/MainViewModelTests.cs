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
