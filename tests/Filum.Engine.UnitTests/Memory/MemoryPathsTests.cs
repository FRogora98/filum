using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Memory;

namespace Filum.Engine.UnitTests.Memory;

public sealed class MemoryPathsTests
{
    [Theory]
    [InlineData("/filum.md")]
    [InlineData("/notes/garden.md")]
    [InlineData("/collections/reading.csv")]
    [InlineData("/note/città-2026_v1.md")]
    [InlineData("/a.b/c.csv")]
    public void Validate_WhenThePathIsValid_ShouldAcceptIt(string path) => Assert.Null(MemoryPaths.Validate(path));

    [Theory]
    [InlineData(null, "required")]
    [InlineData("", "required")]
    [InlineData("notes.md", "start with '/'")]
    [InlineData("/notes//a.md", "empty segment")]
    [InlineData("/notes/../a.md", "'..'")]
    [InlineData("/./a.md", "'..'")]
    [InlineData("/notes/a b.md", "may only contain")]
    [InlineData("/notes/a.txt", "Only .md")]
    [InlineData("/notes/", "empty segment")]
    public void Validate_WhenThePathIsInvalid_ShouldRefuseItWithAReason(string? path, string reason) =>
        Assert.Contains(reason, MemoryPaths.Validate(path));

    [Fact]
    public void Validate_WhenThePathIsOver200Characters_ShouldRefuseIt()
    {
        Assert.Null(MemoryPaths.Validate("/" + new string('a', 196) + ".md"));
        Assert.Contains("too long", MemoryPaths.Validate("/" + new string('a', 197) + ".md"));
    }

    [Fact]
    public void KindOf_WhenGivenAnExtension_ShouldDecideTheKind()
    {
        Assert.Equal(MemoryFileKind.Collection, MemoryPaths.KindOf("/x/list.csv"));
        Assert.Equal(MemoryFileKind.Document, MemoryPaths.KindOf("/x/note.md"));
        Assert.True(MemoryPaths.IsCore("/filum.md"));
        Assert.False(MemoryPaths.IsCore("/notes/filum.md"));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("/", true)]
    [InlineData("/notes", true)]
    [InlineData("/notes/", true)]
    [InlineData("notes", false)]
    [InlineData("/notes/../x", false)]
    public void ValidatePrefix_WhenGivenAPrefix_ShouldCheckIt(string? prefix, bool valid) =>
        Assert.Equal(valid, MemoryPaths.ValidatePrefix(prefix) is null);
}
