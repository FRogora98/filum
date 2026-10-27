using System.Text.Json;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;
using static Filum.Engine.Testing.Builders.Outcomes;

namespace Filum.Engine.Storage.LocalFolder.IntegrationTests;

// Spec 039: a write appends its revision, then writes the file, then saves the state; a crash between two steps is
// simulated by putting back what a later step would have changed.
public sealed class LocalFolderStoreCrashTests : IDisposable
{
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
    public async Task OpenAsync_WhenAWriteStoppedAfterItsRevision_ShouldCompleteItAsTheAgents()
    {
        var (fileBefore, stateBefore) = await TwoWritesAsync();

        // The crash: the revision of the second write is in the log, the file and the state are those before it.
        File.WriteAllText(Disk("notes/plans.md"), fileBefore);
        File.WriteAllText(Disk(".filum/state.json"), stateBefore);

        Assert.Equal(["# Plans", "Second"], Ok(await Open().ReadAsync(_person, "/notes/plans.md", null, null, None)).Lines);
        Assert.Equal("# Plans\nSecond\n", File.ReadAllText(Disk("notes/plans.md")));
        Assert.Equal(2, Log().Count);
        Assert.Equal(MemoryAuthor.Agent, Log()[^1].GetProperty("author").GetString());
    }

    [Fact]
    public async Task OpenAsync_WhenAWriteStoppedAfterItsRevisionAndTheStateIsLost_ShouldCompleteItAsTheAgents()
    {
        var (fileBefore, _) = await TwoWritesAsync();

        File.WriteAllText(Disk("notes/plans.md"), fileBefore);
        File.Delete(Disk(".filum/state.json"));

        Assert.Equal(["# Plans", "Second"], Ok(await Open().ReadAsync(_person, "/notes/plans.md", null, null, None)).Lines);
        Assert.Equal(2, Log().Count);
        Assert.Equal(MemoryAuthor.Agent, Log()[^1].GetProperty("author").GetString());
    }

    [Fact]
    public async Task OpenAsync_WhenAWriteStoppedBeforeItsRevision_ShouldChangeNothing()
    {
        var memory = Open();
        Ok(await memory.WriteAsync(_person, Agent, "/notes/plans.md", "# Plans\nFirst\n", None));

        Assert.Equal(["# Plans", "First"], Ok(await Open().ReadAsync(_person, "/notes/plans.md", null, null, None)).Lines);
        Assert.Single(Log());
    }

    [Fact]
    public async Task OpenAsync_WhenTheFileWasEditedByHand_ShouldStillAdoptItAsThePersons()
    {
        await TwoWritesAsync();
        File.WriteAllText(Disk("notes/plans.md"), "# Plans\nChanged by hand\n");

        Assert.Equal(["# Plans", "Changed by hand"], Ok(await Open().ReadAsync(_person, "/notes/plans.md", null, null, None)).Lines);
        Assert.Equal((MemoryOperation.Outside, MemoryAuthor.Person), (Log()[^1].GetProperty("operation").GetString(), Log()[^1].GetProperty("author").GetString()));
    }

    [Fact]
    public async Task OpenAsync_WhenADeletionStoppedAfterItsRevision_ShouldCompleteIt()
    {
        var memory = Open();
        Ok(await memory.WriteAsync(_person, Agent, "/notes/plans.md", "# Plans\nFirst\n", None));
        var stateBefore = File.ReadAllText(Disk(".filum/state.json"));
        Ok(await memory.DeleteAsync(_person, Agent, "/notes/plans.md", None));

        Directory.CreateDirectory(Disk("notes"));
        File.WriteAllText(Disk("notes/plans.md"), "# Plans\nFirst\n");
        File.WriteAllText(Disk(".filum/state.json"), stateBefore);

        Assert.DoesNotContain(Ok(await Open().ListAsync(_person, "/", true, None)), f => f.Path == "/notes/plans.md");
        Assert.False(File.Exists(Disk("notes/plans.md")));
        Assert.Equal(MemoryAuthor.Agent, Log()[^1].GetProperty("author").GetString());
    }

    // Two writes of the same file; the file and the state as they were between them.
    private async Task<(string File, string State)> TwoWritesAsync()
    {
        var memory = Open();
        Ok(await memory.WriteAsync(_person, Agent, "/notes/plans.md", "# Plans\nFirst\n", None));
        var file = File.ReadAllText(Disk("notes/plans.md"));
        var state = File.ReadAllText(Disk(".filum/state.json"));
        Ok(await memory.WriteAsync(_person, Agent, "/notes/plans.md", "# Plans\nSecond\n", None));
        return (file, state);
    }

    private List<JsonElement> Log() =>
        File.ReadAllLines(Disk(".filum/revisions.jsonl")).Where(l => l.Length > 0).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();

    private IMemoryService Open() => MemoryServices.On(new LocalFolderStore(_folder, _person));

    private string Disk(string relative) => Path.Combine(_folder, relative.Replace('/', Path.DirectorySeparatorChar));
}
