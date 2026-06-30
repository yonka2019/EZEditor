using EZEditor.Models;
using EZEditor.Services;

namespace EZEditor.Tests;

public class JsonFileTests
{
    private readonly JsonDocumentService _svc = new();

    [Fact]
    public void Save_Then_Load_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"jsoneditor_{Guid.NewGuid():N}.json");
        try
        {
            var root = _svc.Parse("""{ "a": 1, "b": ["x", true] }""");
            _svc.Save(root, path);

            Assert.True(File.Exists(path));
            var loaded = _svc.Load(path);
            Assert.Equal(new[] { "a", "b" }, loaded.Children.Select(c => c.Name).ToArray());
            Assert.Equal(JsonNodeKind.Boolean, loaded.Children[1].Children[1].Kind);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
