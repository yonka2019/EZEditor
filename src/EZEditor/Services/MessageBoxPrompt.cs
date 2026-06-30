using System.Windows;

namespace EZEditor.Services;

public class MessageBoxPrompt : IUserPrompt
{
    public void Error(string message) =>
        MessageBox.Show(message, "EZEditor", MessageBoxButton.OK, MessageBoxImage.Error);

    public PromptResult ConfirmDiscard()
    {
        var r = MessageBox.Show(
            "You have unsaved changes. Discard them?",
            "EZEditor",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        return r == MessageBoxResult.Yes ? PromptResult.Yes : PromptResult.Cancel;
    }
}
