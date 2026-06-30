using System.IO;
using System.Windows;
using EZEditor.Services;
using EZEditor.ViewModels;

namespace EZEditor;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var vm = new MainViewModel(new JsonDocumentService(), new FileDialogService(), new MessageBoxPrompt());

        // Support "open with" / command-line file argument (and file associations).
        if (e.Args.Length > 0 && File.Exists(e.Args[0]))
        {
            try { vm.OpenPath(e.Args[0]); }
            catch { /* ignore a bad file passed on launch; user can open another */ }
        }

        var window = new MainWindow { DataContext = vm };
        window.Show();
    }
}
