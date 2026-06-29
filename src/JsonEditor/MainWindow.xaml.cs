using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using JsonEditor.ViewModels;

namespace JsonEditor;

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
}
