using JsonEditor.Models;
using JsonEditor.ViewModels;

namespace JsonEditor.Tests;

public class JsonNodeViewModelTests
{
    [Fact]
    public void IsContainer_TrueForObjectAndArray()
    {
        Assert.True(new JsonNodeViewModel(JsonNodeKind.Object).IsContainer);
        Assert.True(new JsonNodeViewModel(JsonNodeKind.Array).IsContainer);
        Assert.False(new JsonNodeViewModel(JsonNodeKind.String, value: "x").IsContainer);
    }

    [Fact]
    public void DisplayName_UsesNameForObjectMembers_AndIndexForArrayElements()
    {
        var arr = new JsonNodeViewModel(JsonNodeKind.Array);
        var a = new JsonNodeViewModel(JsonNodeKind.String, value: "a", parent: arr);
        var b = new JsonNodeViewModel(JsonNodeKind.String, value: "b", parent: arr);
        arr.Children.Add(a);
        arr.Children.Add(b);
        Assert.Equal("[0]", a.DisplayName);
        Assert.Equal("[1]", b.DisplayName);

        var obj = new JsonNodeViewModel(JsonNodeKind.Object);
        var m = new JsonNodeViewModel(JsonNodeKind.String, name: "key", value: "v", parent: obj);
        Assert.Equal("key", m.DisplayName);
    }

    [Fact]
    public void Changed_BubblesFromChildToRoot()
    {
        var root = new JsonNodeViewModel(JsonNodeKind.Object);
        var child = new JsonNodeViewModel(JsonNodeKind.String, name: "k", value: "v", parent: root);
        root.Children.Add(child);

        var fired = 0;
        root.Changed += (_, _) => fired++;
        child.Value = "changed";

        Assert.True(fired >= 1);
    }

    [Fact]
    public void IsObjectMember_TrueOnlyForObjectChildren()
    {
        var obj = new JsonNodeViewModel(JsonNodeKind.Object);
        var member = new JsonNodeViewModel(JsonNodeKind.String, "k", "v", obj);
        obj.Children.Add(member);

        var arr = new JsonNodeViewModel(JsonNodeKind.Array);
        var elem = new JsonNodeViewModel(JsonNodeKind.String, value: "x", parent: arr);
        arr.Children.Add(elem);

        Assert.True(member.IsObjectMember);
        Assert.False(elem.IsObjectMember);
        Assert.False(obj.IsObjectMember); // root
    }

    [Fact]
    public void ApplyFilter_HidesNonMatchingKeys_KeepsMatchAndAncestors()
    {
        var root = new JsonNodeViewModel(JsonNodeKind.Object);
        var user = new JsonNodeViewModel(JsonNodeKind.Object, "user", parent: root);
        root.Children.Add(user);
        var name = new JsonNodeViewModel(JsonNodeKind.String, "name", "Alice", user);
        var age = new JsonNodeViewModel(JsonNodeKind.Number, "age", "30", user);
        user.Children.Add(name);
        user.Children.Add(age);

        var matched = root.ApplyFilter("name");

        Assert.True(matched);
        Assert.False(root.IsFilteredOut);  // ancestor kept
        Assert.False(user.IsFilteredOut);  // ancestor kept
        Assert.False(name.IsFilteredOut);  // match
        Assert.True(age.IsFilteredOut);    // non-match hidden
    }

    [Fact]
    public void ApplyFilter_Empty_ClearsAll()
    {
        var root = new JsonNodeViewModel(JsonNodeKind.Object);
        var a = new JsonNodeViewModel(JsonNodeKind.String, "a", "1", root);
        root.Children.Add(a);
        a.IsFilteredOut = true;

        root.ApplyFilter("");

        Assert.False(a.IsFilteredOut);
    }
}
