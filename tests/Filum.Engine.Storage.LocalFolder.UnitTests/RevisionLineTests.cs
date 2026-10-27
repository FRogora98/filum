using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Storage;

namespace Filum.Engine.Storage.LocalFolder.UnitTests;

public sealed class RevisionLineTests
{
    private static readonly StoredRevision Revision = new(
        5, Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7"), "/collections/list.csv", "item\nfirst\n", MemorySensitivity.Private,
        false, MemoryOperation.Undo, MemoryAuthor.Person, null, Guid.Parse("16fd2706-8baf-433b-82eb-8c7fada847da"), 4,
        new DateTimeOffset(2026, 10, 8, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Serialize_WhenWritingARevision_ShouldGiveThePublicModelsLineWithTheHashOfItsContentLast()
    {
        var model = JsonLines.Serialize(Revision);

        // Since format 3 (spec 039): the 0.7 line, then sha.
        Assert.Equal(model[..^1] + $",\"sha\":\"{RevisionLine.Hash(Revision.Content)}\"}}", JsonLines.Serialize(RevisionLine.From(Revision)));
    }

    [Theory]
    [InlineData("item\nfirst\n", "item\nfirst\nsecond\n")]
    [InlineData("item\nfirst\nsecond\n", "item\nsecond\n")]
    [InlineData("", "item\n")]
    [InlineData("item\n", "")]
    [InlineData("aaa", "aaaa")]
    [InlineData("abc", "xyz")]
    [InlineData("same\n", "same\n")]
    public void Change_WhenItsContentIsRebuiltFromTheBase_ShouldGiveTheRevisionsContent(string previous, string content)
    {
        var line = JsonLines.Parse<RevisionLine>([JsonLines.Serialize(RevisionLine.Change(Revision with { Content = content }, 4, previous))]).Single();

        Assert.True(line.IsChange);
        Assert.Equal(string.Empty, line.Content);
        Assert.Equal(content, line.ContentAfter(previous));
    }

    [Fact]
    public void Serialize_WhenARowIsAddedToALongFile_ShouldWriteOnlyTheRow()
    {
        var previous = "item\n" + string.Concat(Enumerable.Range(0, 2_000).Select(i => $"row {i}\n"));

        var line = JsonLines.Serialize(RevisionLine.Change(Revision with { Content = previous + "new row\n" }, 4, previous));

        Assert.True(line.Length < 600, line);
        Assert.Contains("\"insert\":\"new row\\n\"", line);
    }

    [Fact]
    public void Serialize_WhenARowChangesInTheMiddleAndAnotherIsAdded_ShouldWriteOnlyThoseRows()
    {
        var rows = Enumerable.Range(0, 2_000).Select(i => $"row {i},\n").ToList();
        var previous = "item,until\n" + string.Concat(rows);
        rows[1_000] = "row 1000,2026-10-15\n";
        var content = "item,until\n" + string.Concat(rows) + "new row,\n";

        var line = RevisionLine.Change(Revision with { Content = content }, 4, previous);

        Assert.True(JsonLines.Serialize(line).Length < 700, JsonLines.Serialize(line));
        Assert.Equal(content, line.ContentAfter(previous));
    }

    [Theory]
    [InlineData("a\nb\nc\nd\n", "a\nx\nc\nd\ny\n")]
    [InlineData("a\nb\nc\n", "c\nb\na\n")]
    [InlineData("a\na\na\n", "a\nb\na\na\n")]
    [InlineData("no final break", "no final break\nnow one\n")]
    public void ContentAfter_WhenSeveralLinesChange_ShouldGiveTheContentExactly(string previous, string content)
    {
        Assert.Equal(content, RevisionLine.Change(Revision with { Content = content }, 4, previous).ContentAfter(previous));
    }

    [Fact]
    public void Parse_WhenALineWasWrittenWhole_ShouldNotBeAChange()
    {
        Assert.False(JsonLines.Parse<RevisionLine>([JsonLines.Serialize(RevisionLine.From(Revision))]).Single().IsChange);
    }

    [Fact]
    public void ToRevision_WhenALineIsReadBack_ShouldGiveTheSameRevision()
    {
        var line = JsonLines.Parse<RevisionLine>([JsonLines.Serialize(RevisionLine.From(Revision))]).Single();

        Assert.Equal(Revision, line.ToRevision());
    }
}
