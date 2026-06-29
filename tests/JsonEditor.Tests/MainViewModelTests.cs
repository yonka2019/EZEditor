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
    public void SaveAs_WritesToChosenPath_AndClearsDirty()
    {
        var src = Path.Combine(Path.GetTempPath(), $"src_{Guid.NewGuid():N}.json");
        var dest = Path.Combine(Path.GetTempPath(), $"dest_{Guid.NewGuid():N}.json");
        File.WriteAllText(src, """{ "a": 1 }""");
        try
        {
            var vm = Make(out var d, out _);
            vm.OpenPath(src);
            vm.Roots[0].Children[0].Value = "2";
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
            var vm = Make(out _, out _); // FakePrompt.Discard defaults to Yes
            vm.OpenPath(path);
            vm.Roots[0].Children[0].Value = "999";
            Assert.True(vm.IsDirty);

            vm.ReloadCommand.Execute(null);

            Assert.False(vm.IsDirty);
            Assert.Equal("1", vm.Roots[0].Children[0].Value);
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
