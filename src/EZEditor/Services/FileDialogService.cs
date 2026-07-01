using System.IO;
using Microsoft.Win32;

namespace EZEditor.Services;

public class FileDialogService : IFileDialogService
{
    private const string Filter =
        "All supported (*.json;*.csv;*.xml)|*.json;*.csv;*.xml|" +
        "JSON files (*.json)|*.json|" +
        "CSV files (*.csv)|*.csv|" +
        "XML files (*.xml)|*.xml|" +
        "All files (*.*)|*.*";

    public string? OpenFile()
    {
        var dlg = new OpenFileDialog { Filter = Filter, CheckFileExists = true };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? SaveFile(string? suggestedName)
    {
        var ext = string.IsNullOrEmpty(suggestedName) ? ".json" : Path.GetExtension(suggestedName);
        if (string.IsNullOrEmpty(ext)) ext = ".json";
        var dlg = new SaveFileDialog
        {
            Filter = Filter,
            FileName = suggestedName is null ? $"data{ext}" : Path.GetFileName(suggestedName),
            DefaultExt = ext
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
