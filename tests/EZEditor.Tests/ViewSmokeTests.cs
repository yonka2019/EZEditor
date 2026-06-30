using System;
using System.IO;
using System.Threading;
using System.Windows;
using EZEditor;
using EZEditor.Services;
using EZEditor.ViewModels;

namespace EZEditor.Tests;

// Constructs the real WPF MainWindow (on an STA thread) bound to a sample document and
// forces layout. This catches XAML regressions that unit tests can't: a missing
// StaticResource key, a broken ControlTemplate/DataTemplate, or an unresolved converter
// all throw here when the templates are applied.
public class ViewSmokeTests
{
    private sealed class NoDialogs : IFileDialogService
    {
        public string? OpenFile() => null;
        public string? SaveFile(string? suggestedName) => null;
    }

    private sealed class NoPrompt : IUserPrompt
    {
        public void Error(string message) { }
        public PromptResult ConfirmDiscard() => PromptResult.Yes;
    }

    private static void RunOnSta(Action action)
    {
        Exception? captured = null;
        var t = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        })
        { IsBackground = true };
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (captured != null) throw captured;
    }

    [Fact]
    public void MainWindow_BuildsAndLaysOut_WithoutResourceOrTemplateErrors()
    {
        RunOnSta(() =>
        {
            var app = Application.Current ?? new Application();
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/EZEditor;component/Themes/Theme.xaml")
            });

            var path = Path.Combine(Path.GetTempPath(), $"vsmoke_{Guid.NewGuid():N}.json");
            File.WriteAllText(path, """{ "a": 1, "b": [true, null, "x"], "c": { "d": 2.5 } }""");
            try
            {
                var vm = new MainViewModel(new JsonDocumentService(), new NoDialogs(), new NoPrompt());
                vm.OpenPath(path);

                var window = new MainWindow { DataContext = vm };
                // Force template application + layout without showing the window.
                window.Measure(new Size(800, 600));
                window.Arrange(new Rect(0, 0, 800, 600));
                window.UpdateLayout();

                Assert.NotNull(window.Content);
                Assert.Single(vm.Roots);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        });
    }
}
