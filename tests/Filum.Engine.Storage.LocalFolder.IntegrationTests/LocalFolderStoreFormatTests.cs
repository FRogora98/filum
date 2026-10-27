using System.Runtime.CompilerServices;
using System.Text.Json;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;
using static Filum.Engine.Testing.Builders.Outcomes;

namespace Filum.Engine.Storage.LocalFolder.IntegrationTests;

// Every later version must read the v0.7.1 folder in Fixtures/folder-v0.7 as it was and keep writing the same lines, or
// people's memory on their own machines would be lost. Regenerate it only for a deliberate format change, with
// FILUM_UPDATE_FIXTURES=1.
public sealed class LocalFolderStoreFormatTests : IDisposable
{
    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly Guid Owner = Guid.Parse("7a1c2f0e-0000-4000-8000-000000000001");
    private static readonly Guid Chat = Guid.Parse("7a1c2f0e-0000-4000-8000-000000000002");
    private static readonly MemoryActor Agent = MemoryActor.Agent(Chat, Guid.Parse("7a1c2f0e-0000-4000-8000-000000000003"));

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "filum-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Filum.Engine.Testing.Builders.TemporaryFolders.Delete(_folder);
        }
    }

    [Fact]
    public async Task OpenAsync_WhenTheFolderWasWrittenByV07_ShouldReadItsFilesHistoryEventsAndFacts()
    {
        var memory = await OpenFixtureAsync();

        var files = Ok(await memory.ListAsync(Owner, "/", true, None)).Select(f => (f.Path, f.Sensitivity)).Where(f => !f.Path.StartsWith("/skills/", StringComparison.Ordinal));
        Assert.Equal(
            [("/facts.csv", MemorySensitivity.Normal), ("/filum.md", MemorySensitivity.Normal), ("/learned.md", MemorySensitivity.Normal), ("/lists/reading.csv", MemorySensitivity.Normal), ("/notes/diary.md", MemorySensitivity.Private), ("/notes/plans.md", MemorySensitivity.Normal)],
            files.OrderBy(f => f.Path, StringComparer.Ordinal));
        Assert.Equal(["# Plans", "Walk by the river", "Call the library"], Ok(await memory.ReadAsync(Owner, "/notes/plans.md", null, null, None)).Lines);
        Assert.Equal(["Moved from /notes/trip.md", "Added 1 line", "Created"], Ok(await memory.GetHistoryAsync(Owner, "/notes/plans.md", None)).Select(r => r.Summary));
        Assert.Equal(["Sam lives in Turin", "The library opens at nine"], (await memory.EventsAsync(Owner, new EventQuery(), None)).Where(e => e.Kind == MemoryEventKind.Said).Select(e => e.Text).Order(StringComparer.Ordinal));
        Assert.Equal(["Turin"], Ok(await memory.CurrentFactsAsync(Owner, "Sam", None)).Select(f => f.Value));
        Assert.False(File.Exists(Path.Combine(_folder, "notes", "old.md")));
    }

    [Fact]
    public async Task WriteAsync_WhenTheFolderWasWrittenByV07_ShouldAppendLinesOfTheSameShape()
    {
        var memory = await OpenFixtureAsync();
        var before = Keys(Path.Combine(_folder, ".filum", "revisions.jsonl"));
        var eventsBefore = Keys(Path.Combine(_folder, ".filum", "events.jsonl"));

        Ok(await memory.AppendAsync(Owner, Agent, "/notes/plans.md", "Buy stamps", None));
        await memory.RecordAsync(Owner, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.App, "Stamps too", ConversationId: Chat), None);

        // Since 1.2 a revision of a file with a previous one is written as a change: the 0.7 keys in their order, then base and
        // hunks.
        // Since 1.3 (format 3, spec 039) every new line also carries its content's hash, last.
        Assert.Equal(before[0] + ",base,hunks,sha", Keys(Path.Combine(_folder, ".filum", "revisions.jsonl"))[^1]);
        Assert.Equal(eventsBefore[0], Keys(Path.Combine(_folder, ".filum", "events.jsonl"))[^1]);
        Assert.Equal(["# Plans", "Walk by the river", "Call the library", "Buy stamps"], Ok(await memory.ReadAsync(Owner, "/notes/plans.md", null, null, None)).Lines);
    }

    [Fact]
    public async Task OpenAsync_WhenTheStateOfAV07FolderIsMissing_ShouldRebuildTheSameMemory()
    {
        var memory = await OpenFixtureAsync();
        var expected = Ok(await memory.ListAsync(Owner, "/", true, None)).Select(f => (f.Path, f.Sensitivity, f.LineCount)).ToList();

        File.Delete(Path.Combine(_folder, ".filum", "state.json"));
        var rebuilt = Memory(new LocalFolderStore(_folder, Owner));

        Assert.Equal(expected, Ok(await rebuilt.ListAsync(Owner, "/", true, None)).Select(f => (f.Path, f.Sensitivity, f.LineCount)));
    }

    private async Task<IMemoryService> OpenFixtureAsync()
    {
        var fixture = FixtureFolder();
        if (Environment.GetEnvironmentVariable("FILUM_UPDATE_FIXTURES") == "1")
        {
            await WriteFixtureAsync(fixture);
        }

        Copy(fixture, _folder);
        return Memory(new LocalFolderStore(_folder, Owner));
    }

    private static async Task WriteFixtureAsync(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }

        var memory = Memory(new LocalFolderStore(folder, Owner));
        await memory.EnsureCoreAsync(Owner, None);
        await memory.EnsureLearnedAsync(Owner, None);
        var said = await memory.RecordAsync(Owner, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.App, "Sam lives in Turin", ConversationId: Chat), None);
        await memory.RecordAsync(Owner, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.App, "The library opens at nine", ConversationId: Chat), None);
        Ok(await memory.WriteAsync(Owner, Agent, "/notes/trip.md", "# Plans\nWalk by the river\n", None));
        Ok(await memory.AppendAsync(Owner, Agent, "/notes/trip.md", "Call the library", None));
        Ok(await memory.MoveAsync(Owner, Agent, "/notes/trip.md", "/notes/plans.md", None));
        Ok(await memory.AddRowsAsync(Owner, Agent, "/lists/reading.csv", [new Dictionary<string, string> { ["title"] = "First", ["author"] = "Someone" }], None));
        Ok(await memory.RecordFactAsync(Owner, Agent, "Sam", "city", "Turin", "2026-10-01", null, [said.Id], None));
        Ok(await memory.WriteAsync(Owner, MemoryActor.Person, "/notes/diary.md", "# Diary\nA quiet day\n", None));
        Ok(await memory.SetSensitivityAsync(Owner, MemoryActor.Person, "/notes/diary.md", MemorySensitivity.Private, None));
        Ok(await memory.WriteAsync(Owner, Agent, "/notes/old.md", "# Old\n", None));
        Ok(await memory.DeleteAsync(Owner, Agent, "/notes/old.md", None));
        File.Delete(Path.Combine(folder, ".filum", "lock"));
    }

    private static IMemoryService Memory(LocalFolderStore store) => MemoryServices.On(store);

    private static List<string> Keys(string jsonl) =>
        File.ReadAllLines(jsonl).Where(l => l.Length > 0)
            .Select(l => string.Join(",", JsonDocument.Parse(l).RootElement.EnumerateObject().Select(p => p.Name)))
            .ToList();

    private static void Copy(string from, string to)
    {
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        Directory.CreateDirectory(Path.Combine(to, ".filum", "tmp"));
    }

    private static string FixtureFolder([CallerFilePath] string source = "") =>
        Environment.GetEnvironmentVariable("FILUM_UPDATE_FIXTURES") == "1"
            ? Path.Combine(Path.GetDirectoryName(source)!, "Fixtures", "folder-v0.7")
            : Path.Combine(AppContext.BaseDirectory, "Fixtures", "folder-v0.7");
}
