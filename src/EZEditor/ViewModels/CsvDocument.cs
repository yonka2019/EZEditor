using System.Collections.ObjectModel;
using System.Collections.Specialized;
using EZEditor.Services;

namespace EZEditor.ViewModels;

public sealed class CsvDocument : EditableDocument
{
    private readonly CsvDocumentService _svc;

    public CsvDocument(CsvParseResult parsed, CsvDocumentService svc)
    {
        _svc = svc;
        Delimiter = parsed.Delimiter;
        HasHeader = parsed.HasHeader;
        Columns = new ObservableCollection<string>(parsed.Columns);
        Rows = new ObservableCollection<CsvRow>(parsed.Rows);

        foreach (var r in Rows) r.PropertyChanged += OnRowChanged;
        Rows.CollectionChanged += OnRowsChanged;
    }

    public ObservableCollection<string> Columns { get; }
    public ObservableCollection<CsvRow> Rows { get; }
    public char Delimiter { get; set; }
    public bool HasHeader { get; set; }

    // Raised when the column set changes so the DataGrid can rebuild its columns.
    public event EventHandler? ColumnsChanged;

    public override DocumentFormat Format => DocumentFormat.Csv;

    // Hide rows whose cells don't contain the search text (case-insensitive). Empty text
    // clears the filter. Setting IsFilteredOut must NOT dirty the document — see OnRowChanged.
    public override void ApplyFilter(string? text)
    {
        var hasText = !string.IsNullOrWhiteSpace(text);
        foreach (var row in Rows)
            row.IsFilteredOut = hasText && !RowMatches(row, text!);
    }

    private static bool RowMatches(CsvRow row, string text)
    {
        for (var i = 0; i < row.Count; i++)
            if (row[i].Contains(text, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public override string Serialize()
    {
        var rows = Rows.Select(r => (IReadOnlyList<string>)Enumerable
            .Range(0, Columns.Count).Select(i => r[i]).ToList()).ToList();
        return _svc.Serialize(Columns, rows, Delimiter, HasHeader);
    }

    public void AddRow()
    {
        var row = new CsvRow(Enumerable.Repeat(string.Empty, Columns.Count));
        Rows.Add(row);
    }

    public void DeleteRow(CsvRow row) => Rows.Remove(row);

    public void AddColumn(string name)
    {
        Columns.Add(name);
        foreach (var r in Rows) r.AddCell(notify: false);
        ColumnsChanged?.Invoke(this, EventArgs.Empty);
        OnChanged();
    }

    public void RenameColumn(int index, string name)
    {
        if (index < 0 || index >= Columns.Count) return;
        Columns[index] = name;
        ColumnsChanged?.Invoke(this, EventArgs.Empty);
        OnChanged();
    }

    public void DeleteColumn(int index)
    {
        if (index < 0 || index >= Columns.Count) return;
        Columns.RemoveAt(index);
        foreach (var r in Rows) r.RemoveCellAt(index, notify: false);
        ColumnsChanged?.Invoke(this, EventArgs.Empty);
        OnChanged();
    }

    // Only a cell edit ("Item[]") dirties the document; IsFilteredOut (filtering) must not.
    private void OnRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item[]") OnChanged();
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (CsvRow r in e.NewItems) r.PropertyChanged += OnRowChanged;
        if (e.OldItems is not null)
            foreach (CsvRow r in e.OldItems) r.PropertyChanged -= OnRowChanged;
        OnChanged();
    }
}
