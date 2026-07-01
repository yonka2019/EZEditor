using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

    private static void ResetApplicationSingleton()
    {
        // WPF Application is a per-AppDomain singleton guarded by two static fields.
        // When the first STA test thread ends, Application.Current is non-null but its
        // dispatcher thread is dead; we must clear both fields so a new Application() works.
        const System.Reflection.BindingFlags f =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        typeof(Application).GetField("_appInstance", f)?.SetValue(null, null);
        typeof(Application).GetField("_appCreatedInThisAppDomain", f)?.SetValue(null, false);
    }

    private static MainWindow BuildWindow(string fileName, string contents, out MainViewModel vm)
    {
        // Each STA test thread needs its own Application so that Window.Show() uses the
        // current thread's dispatcher. Application.Current is process-wide; once its owning
        // thread has finished, the Application cannot pump layout for a new thread's window.
        // Reset both singleton guards so a fresh Application can be created on each thread.
        var existing = Application.Current;
        if (existing != null && !existing.Dispatcher.Thread.IsAlive)
            ResetApplicationSingleton();

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
        // A bare Measure/Arrange on a never-shown Window does NOT expand a ContentControl's
        // DataTemplate (its ContentPresenter is only built once connected to a presentation
        // source). Show it off-screen so the inner editor (TreeView / DataGrid) actually
        // realizes — which is the whole point of a view smoke test.
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -32000;
        window.Top = -32000;
        window.Width = 900;
        window.Height = 650;
        window.ShowInTaskbar = false;
        window.ShowActivated = false;
        window.Show();
        window.UpdateLayout();
        return window;
    }

    [Fact]
    public void MainWindow_RealizesDataGrid_ForCsvDocument()
    {
        RunOnSta(() =>
        {
            var window = BuildWindow("vsmoke_*.csv", "name,age\nAlice,30\nBob,25", out var vm);
            try
            {
                Assert.IsType<CsvDocument>(vm.CurrentDocument);
                var root = window.Content as DependencyObject ?? window;
                Assert.NotNull(FindVisualChild<DataGrid>(root));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void MainWindow_RealizesTreeView_ForJsonDocument()
    {
        RunOnSta(() =>
        {
            var window = BuildWindow("vsmoke_*.json", """{ "a": 1, "b": [true, null, "x"], "c": { "d": 2.5 } }""", out var vm);
            try
            {
                Assert.IsType<JsonDocument>(vm.CurrentDocument);
                var root = window.Content as DependencyObject ?? window;
                Assert.NotNull(FindVisualChild<TreeView>(root));
            }
            finally { window.Close(); }
        });
    }
}
