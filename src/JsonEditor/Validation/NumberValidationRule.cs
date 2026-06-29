using System.Globalization;
using System.Windows.Controls;
using JsonEditor.Services;

namespace JsonEditor.Validation;

// Flags a number field whose text is not a valid number, so the editor can show
// a red border + tooltip instead of silently accepting an incompatible value.
public class NumberValidationRule : ValidationRule
{
    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        var s = value as string;
        if (string.IsNullOrWhiteSpace(s))
            return new ValidationResult(false, "Enter a number");

        return JsonDocumentService.IsValidNumber(s)
            ? ValidationResult.ValidResult
            : new ValidationResult(false, $"“{s}” is not a valid number");
    }
}
