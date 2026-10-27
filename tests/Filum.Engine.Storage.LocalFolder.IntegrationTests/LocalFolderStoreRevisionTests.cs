using System.Diagnostics;
using System.Globalization;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;
using Xunit.Abstractions;
using static Filum.Engine.Testing.Builders.Outcomes;

namespace Filum.Engine.Storage.LocalFolder.IntegrationTests;

// A revision of a file that grows a row at a time keeps only what changed, so the log grows with the changes, not with
// the square of the file.
public sealed class LocalFolderStoreRevisionTests(ITestOutputHelper output) : IDisposable
{
    // FILUM_SCALE_FACTS=5000 measures the larger case by hand; the default keeps the run short.
    private const string ScaleVariable = "FILUM_SCALE_FACTS";
    private const int DefaultFacts = 1_000;

    // A fact row and its revision line, with room to spare: a whole-file revision would need far more.
    private const int MaxLogBytesPerFact = 2_000;

    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly MemoryActor Agent = MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid());

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "filum-tests", Guid.NewGuid().ToString("N"));
    private readonly Guid _person = Guid.NewGuid();

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Filum.Engine.Testing.Builders.TemporaryFolders.Delete(_folder);
        }
    }

    [Fact]
    public async Task RecordFactAsync_WhenManyFactsAreRecorded_ShouldKeepTheLogInProportionToTheFacts()
    {
        var facts = int.TryParse(Environment.GetEnvironmentVariable(ScaleVariable), CultureInfo.InvariantCulture, out var n) ? n : DefaultFacts;
        var memory = Open();
        var clock = Stopwatch.StartNew();

        for (var i = 0; i < facts; i++)
        {
            Ok(await memory.RecordFactAsync(_person, Agent, $"Subject {i % (facts / 2 + 1)}", "attribute", $"value {i}", null, null, [], correction: i > facts / 2, None));
        }

        var bytes = new FileInfo(Disk(".filum/revisions.jsonl")).Length;
        output.WriteLine($"{facts} facts: revisions.jsonl {bytes:N0} bytes, {clock.Elapsed.TotalSeconds:0.0} s");
        Assert.True(bytes < (long)facts * MaxLogBytesPerFact, $"{bytes:N0} bytes for {facts} facts");
    }

    [Fact]
    public async Task GetRevisionAsync_WhenAFileGrewARowAtATime_ShouldGiveEveryVersionExactly()
    {
        var memory = Open();
        var revisions = new List<long>();
        for (var i = 0; i < 150; i++)
        {
            revisions.Add(Ok(await memory.AddRowsAsync(_person, Agent, "/lists/items.csv", [new Dictionary<string, string> { ["item"] = $"item {i}" }], None)).RevisionId);
        }

        var reopened = Open();

        foreach (var (revision, rows) in revisions.Select((r, i) => (r, i + 1)))
        {
            var version = Ok(await reopened.GetRevisionAsync(_person, revision, None));
            Assert.Equal("item\n" + string.Concat(Enumerable.Range(0, rows).Select(i => $"item {i}\n")), version.Content);
        }
    }

    [Fact]
    public async Task UndoAsync_WhenTheRevisionIsAChangeOnAChange_ShouldBringBackItsBefore()
    {
        var memory = Open();
        Ok(await memory.WriteAsync(_person, Agent, "/notes/a.md", "one\n", None));
        Ok(await memory.AppendAsync(_person, Agent, "/notes/a.md", "two", None));
        var third = Ok(await memory.AppendAsync(_person, Agent, "/notes/a.md", "three", None));

        Ok(await Open().UndoAsync(_person, MemoryActor.Person, third.RevisionId, None));

        Assert.Equal("one\ntwo\n", File.ReadAllText(Disk("notes/a.md")));
    }

    [Fact]
    public async Task SaveAsync_WhenTheFileWasEditedByHandBetweenWrites_ShouldStillKeepEveryVersionExactly()
    {
        var memory = Open();
        Ok(await memory.WriteAsync(_person, Agent, "/notes/a.md", "one\n", None));
        File.WriteAllText(Disk("notes/a.md"), "one\nby hand\n");
        var after = Ok(await Open().AppendAsync(_person, Agent, "/notes/a.md", "two", None));

        var history = Ok(await Open().GetHistoryAsync(_person, "/notes/a.md", None));

        Assert.Equal(3, history.Count);
        Assert.Equal("one\nby hand\ntwo\n", Ok(await Open().GetRevisionAsync(_person, after.RevisionId, None)).Content);
    }

    private IMemoryService Open() => MemoryServices.On(new LocalFolderStore(_folder, _person));

    private string Disk(string relative) => Path.Combine(_folder, relative.Replace('/', Path.DirectorySeparatorChar));
}
