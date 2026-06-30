using EZEditor.ViewModels;

namespace EZEditor.Services;

public sealed record CsvParseResult(
    List<string> Columns,
    List<CsvRow> Rows,
    char Delimiter,
    bool HasHeader);
