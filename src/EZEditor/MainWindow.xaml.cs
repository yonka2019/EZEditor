using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using EZEditor.ViewModels;

namespace EZEditor;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    // Paint the native title bar dark to match the app (Windows 10 2004+/11).
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        var hwnd = new WindowInteropHelper(this).Handle;
        int useDark = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
    }

    // Guard against closing with unsaved changes (spec §6).
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm && !vm.ConfirmDiscardIfDirty())
            e.Cancel = true;
    }

    // Shift + mouse wheel scrolls the tree horizontally.
    private void OnTreePreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Shift) return;
        var sv = FindScrollViewer((DependencyObject)sender);
        if (sv is null) return;
        sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer sv) return sv;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
            if (found is not null) return found;
        }
        return null;
    }

    // Open the current file externally: Notepad++ if installed, else the default app.
    // Shows the on-disk content (save first to reflect unsaved edits).
    private void OnOpenExternally(object sender, RoutedEventArgs e)
    {
        var path = (DataContext as MainViewModel)?.CurrentPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            MessageBox.Show("Open or save a file first.", "EZEditor",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var npp = FindNotepadPlusPlus();
            if (npp is not null)
            {
                var psi = new ProcessStartInfo(npp) { UseShellExecute = true };
                psi.ArgumentList.Add(path);
                Process.Start(psi);
            }
            else
            {
                // Fall back to the OS default app for this file type.
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open the file:\n{ex.Message}", "EZEditor",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

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

    // Locate notepad++.exe via the registry App Paths key, then common install dirs.
    private static string? FindNotepadPlusPlus()
    {
        const string subKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\notepad++.exe";
        foreach (var hive in new[] { Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryHive.CurrentUser })
        {
            foreach (var view in new[] { Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Registry32 })
            {
                using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, view);
                using var key = baseKey.OpenSubKey(subKey);
                if (key?.GetValue(null) is string p && File.Exists(p))
                    return p;
            }
        }

        foreach (var env in new[] { "ProgramW6432", "ProgramFiles", "ProgramFiles(x86)" })
        {
            var dir = Environment.GetEnvironmentVariable(env);
            if (!string.IsNullOrEmpty(dir))
            {
                var candidate = Path.Combine(dir, "Notepad++", "notepad++.exe");
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }
}
