namespace EZEditor.Services;

public enum PromptResult { Yes, No, Cancel }

public interface IUserPrompt
{
    void Error(string message);
    PromptResult ConfirmDiscard();
}
