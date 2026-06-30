namespace EZEditor.Services;

public interface IFileDialogService
{
    string? OpenFile();
    string? SaveFile(string? suggestedName);
}
