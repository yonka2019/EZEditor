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
