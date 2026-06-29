using System.Windows;

namespace JsonEditor.Services;

public class MessageBoxPrompt : IUserPrompt
{
    public void Error(string message) =>
        MessageBox.Show(message, "JSON Editor", MessageBoxButton.OK, MessageBoxImage.Error);

    public PromptResult ConfirmDiscard()
    {
        var r = MessageBox.Show(
            "You have unsaved changes. Discard them?",
            "JSON Editor",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        return r == MessageBoxResult.Yes ? PromptResult.Yes : PromptResult.Cancel;
    }
}
