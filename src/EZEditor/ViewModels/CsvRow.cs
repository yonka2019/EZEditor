using CommunityToolkit.Mvvm.ComponentModel;

namespace EZEditor.ViewModels;

public sealed class CsvRow : ObservableObject
{
    private readonly List<string> _cells;

    public CsvRow(IEnumerable<string> cells) => _cells = cells.ToList();

    public IReadOnlyList<string> Cells => _cells;
    public int Count => _cells.Count;

    // "Item[]" is WPF's indexer-changed sentinel; kept as a literal so this VM stays WPF-free.
    public string this[int index]
    {
        get => index >= 0 && index < _cells.Count ? _cells[index] : string.Empty;
        set
        {
            while (_cells.Count <= index) _cells.Add(string.Empty);
            if (_cells[index] == value) return;
            _cells[index] = value;
            OnPropertyChanged("Item[]");
        }
    }

    internal void AddCell(string value = "", bool notify = true) { _cells.Add(value); if (notify) OnPropertyChanged("Item[]"); }
    internal void RemoveCellAt(int index, bool notify = true)
    {
        if (index < 0 || index >= _cells.Count) return;
        _cells.RemoveAt(index);
        if (notify) OnPropertyChanged("Item[]");
    }
}
