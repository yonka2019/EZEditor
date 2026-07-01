using System.ComponentModel;
using EZEditor.Services;
using EZEditor.ViewModels;

namespace EZEditor.Tests;

public class CsvDocumentTests
{
    private static CsvDocument Make(string text, bool hasHeader = true)
    {
        var svc = new CsvDocumentService();
        return new CsvDocument(svc.Parse(text, hasHeader), svc);
    }

    [Fact]
    public void Parse_HeaderRow_BecomesColumns_NotData()
    {
        var doc = Make("name,age\nAlice,30");
        Assert.Equal(new[] { "name", "age" }, doc.Columns.ToArray());
        Assert.Single(doc.Rows);
        Assert.Equal("Alice", doc.Rows[0][0]);
        Assert.Equal(DocumentFormat.Csv, doc.Format);
    }

    [Fact]
    public void CsvRow_Indexer_GrowsAndNotifies()
    {
        var row = new CsvRow(new[] { "a" });
        string? prop = null;
        ((INotifyPropertyChanged)row).PropertyChanged += (_, e) => prop = e.PropertyName;
        row[3] = "x";
        Assert.Equal("Item[]", prop);
        Assert.Equal("x", row[3]);
        Assert.Equal("", row[2]); // grown with blanks
    }

    [Fact]
    public void EditingCell_RaisesChanged()
    {
        var doc = Make("a,b\n1,2");
        var fired = 0;
        doc.Changed += (_, _) => fired++;
        doc.Rows[0][0] = "99";
        Assert.True(fired >= 1);
    }

    [Fact]
    public void AddRow_AddsBlankRow_WithColumnWidth_AndRaisesChanged()
    {
        var doc = Make("a,b\n1,2");
        var fired = 0;
        doc.Changed += (_, _) => fired++;
        doc.AddRow();
        Assert.Equal(2, doc.Rows.Count);
        Assert.Equal("", doc.Rows[1][0]);
        Assert.True(fired >= 1);
    }

    [Fact]
    public void AddColumn_AppendsHeader_AndRaisesColumnsChanged()
    {
        var doc = Make("a,b\n1,2");
        var colsChanged = 0;
        doc.ColumnsChanged += (_, _) => colsChanged++;
        doc.AddColumn("c");
        Assert.Equal(new[] { "a", "b", "c" }, doc.Columns.ToArray());
        Assert.True(colsChanged >= 1);
    }

    [Fact]
    public void DeleteColumn_RemovesHeaderAndCells()
    {
        var doc = Make("a,b,c\n1,2,3");
        doc.DeleteColumn(1);
        Assert.Equal(new[] { "a", "c" }, doc.Columns.ToArray());
        Assert.Equal("1", doc.Rows[0][0]);
        Assert.Equal("3", doc.Rows[0][1]);
    }

    [Fact]
    public void Serialize_RoundTripsThroughModel()
    {
        var doc = Make("name,note\nAlice,\"has,comma\"");
        var text = doc.Serialize();
        Assert.Equal("name,note\r\nAlice,\"has,comma\"", text);
    }

    [Fact]
    public void Parse_NoHeader_NamesColumns_AndKeepsAllRows()
    {
        var doc = Make("a,b\n1,2", hasHeader: false);
        Assert.Equal(new[] { "Column1", "Column2" }, doc.Columns.ToArray());
        Assert.Equal(2, doc.Rows.Count);
        Assert.Equal("a", doc.Rows[0][0]);
    }

    [Fact]
    public void RenameColumn_UpdatesHeader_AndRaisesColumnsChanged()
    {
        var doc = Make("a,b\n1,2");
        var colsChanged = 0;
        doc.ColumnsChanged += (_, _) => colsChanged++;
        doc.RenameColumn(0, "renamed");
        Assert.Equal("renamed", doc.Columns[0]);
        Assert.Equal(1, colsChanged);
    }

    [Fact]
    public void AddColumn_FiresChangedExactlyOnce()
    {
        var doc = Make("a,b\n1,2\n3,4");
        var fired = 0;
        doc.Changed += (_, _) => fired++;
        doc.AddColumn("c");
        Assert.Equal(1, fired);
    }

    [Fact]
    public void DeleteRow_UnsubscribesRow_NoChangedAfter()
    {
        var doc = Make("a,b\n1,2");
        var row = doc.Rows[0];
        doc.DeleteRow(row);
        var fired = 0;
        doc.Changed += (_, _) => fired++;
        row[0] = "zzz";
        Assert.Equal(0, fired);
    }

    [Fact]
    public void ApplyFilter_HidesRowsWithoutMatch_CaseInsensitive()
    {
        var doc = Make("name,age\nAlice,30\nBob,25");
        doc.ApplyFilter("alice");
        Assert.False(doc.Rows[0].IsFilteredOut); // "Alice" matches (case-insensitive)
        Assert.True(doc.Rows[1].IsFilteredOut);  // "Bob","25" do not
    }

    [Fact]
    public void ApplyFilter_MatchesAnyCell()
    {
        var doc = Make("name,age\nAlice,30\nBob,25");
        doc.ApplyFilter("25");
        Assert.True(doc.Rows[0].IsFilteredOut);
        Assert.False(doc.Rows[1].IsFilteredOut); // matches the age cell "25"
    }

    [Fact]
    public void ApplyFilter_Empty_ShowsAllRows()
    {
        var doc = Make("name,age\nAlice,30\nBob,25");
        doc.ApplyFilter("Bob");
        doc.ApplyFilter("");
        Assert.All(doc.Rows, r => Assert.False(r.IsFilteredOut));
    }

    [Fact]
    public void ApplyFilter_DoesNotDirtyDocument()
    {
        var doc = Make("name,age\nAlice,30\nBob,25");
        var fired = 0;
        doc.Changed += (_, _) => fired++;
        doc.ApplyFilter("Alice");
        Assert.Equal(0, fired); // filtering must never mark the document dirty
    }
}
