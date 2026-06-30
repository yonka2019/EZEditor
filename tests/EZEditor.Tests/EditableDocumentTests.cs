// tests/EZEditor.Tests/EditableDocumentTests.cs
using System.Text;
using EZEditor.ViewModels;

namespace EZEditor.Tests;

public class EditableDocumentTests
{
    private sealed class FakeDoc : EditableDocument
    {
        public string Text = "hello";
        public override DocumentFormat Format => DocumentFormat.Json;
        public override string Serialize() => Text;
        public void Edit(string t) { Text = t; OnChanged(); }
    }

    [Fact]
    public void OnChanged_RaisesChangedEvent()
    {
        var doc = new FakeDoc();
        var fired = 0;
        doc.Changed += (_, _) => fired++;
        doc.Edit("world");
        Assert.Equal(1, fired);
    }

    [Fact]
    public void Save_WritesSerializedTextAsUtf8NoBom()
    {
        var doc = new FakeDoc { Text = "abc" };
        var path = Path.Combine(Path.GetTempPath(), $"ed_{Guid.NewGuid():N}.txt");
        try
        {
            doc.Save(path);
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(new byte[] { 0x61, 0x62, 0x63 }, bytes); // no BOM
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
