using System.Diagnostics;
using System.Text.Json;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;
using static Filum.Engine.Testing.Builders.Outcomes;

namespace Filum.Engine.Storage.LocalFolder.IntegrationTests;

public sealed class LocalFolderStoreTests : IDisposable
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
    public async Task WriteAsync_WhenFilesChange_ShouldKeepRealFilesAtTheirPathsAndOneLogLinePerChange()
    {
        var memory = Open();
        Ok(await memory.WriteAsync(_person, Agent, "/notes/plans.md", "# Plans\nWalk by the river\n", None));
        Ok(await memory.AddRowsAsync(_person, Agent, "/lists/reading.csv", [new Dictionary<string, string> { ["title"] = "First" }], None));
        Ok(await memory.AppendAsync(_person, Agent, "/notes/plans.md", "Call the library", None));

        Assert.Equal("# Plans\nWalk by the river\nCall the library\n", File.ReadAllText(Disk("notes/plans.md")));
        Assert.Equal("title\nFirst\n", File.ReadAllText(Disk("lists/reading.csv")));
        var log = File.ReadAllLines(Disk(".filum/revisions.jsonl"));
        Assert.Equal(3, log.Length);
        Assert.All(log, line => JsonDocument.Parse(line));
        Assert.Equal(
            ["lists/reading.csv", "notes/plans.md"],
            Directory.EnumerateFiles(_folder, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(_folder, f).Replace('\\', '/'))
                .Where(f => !f.StartsWith(".filum/", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Open_WhenANewStoreUsesTheSameFolder_ShouldFindFilesVersionsHistoryAndUndo()
    {
        var first = Open();
        Ok(await first.WriteAsync(_person, Agent, "/notes/a.md", "one\n", None));
        var second = Ok(await first.AppendAsync(_person, Agent, "/notes/a.md", "two", None));

        var restarted = Open();

        Assert.Equal(["one", "two"], Ok(await restarted.ReadAsync(_person, "/notes/a.md", null, null, None)).Lines);
        Assert.Equal(["Added 1 line", "Created"], Ok(await restarted.GetHistoryAsync(_person, "/notes/a.md", None)).Select(r => r.Summary));
        Ok(await restarted.UndoAsync(_person, MemoryActor.Person, second.RevisionId, None));
        Assert.Equal("one\n", File.ReadAllText(Disk("notes/a.md")));
    }

    [Fact]
    public async Task ReadAsync_WhenFilesWereChangedByHand_ShouldMakeThemThePersonsRevisionsThatCanBeUndone()
    {
        var memory = Open();
        Ok(await memory.WriteAsync(_person, Agent, "/notes/a.md", "written by Filum\n", None));
        Ok(await memory.WriteAsync(_person, Agent, "/notes/gone.md", "soon removed\n", None));

        File.WriteAllText(Disk("notes/a.md"), "edited by hand\r\nsecond line\r\n");
        Directory.CreateDirectory(Disk("ideas"));
        File.WriteAllText(Disk("ideas/new.md"), "added by hand\n");
        File.Delete(Disk("notes/gone.md"));
        File.WriteAllText(Disk("notes/.hidden.md"), "not memory\n");
        File.WriteAllText(Disk("notes/readme.txt"), "not memory either\n");

        Assert.Equal(["edited by hand", "second line"], Ok(await memory.ReadAsync(_person, "/notes/a.md", null, null, None)).Lines);
        var history = Ok(await memory.GetHistoryAsync(_person, "/notes/a.md", None));
        Assert.Equal(("Changed outside Filum", MemoryAuthor.Person), (history[0].Summary, history[0].Author));
        Assert.Equal(["/ideas/new.md", "/notes/a.md"], Ok(await memory.ListAsync(_person, "/", true, None)).Select(f => f.Path));
        Assert.Equal("Created outside Filum", Ok(await memory.GetHistoryAsync(_person, "/ideas/new.md", None))[0].Summary);

        Ok(await memory.UndoAsync(_person, MemoryActor.Person, history[0].Id, None));
        Assert.Equal("written by Filum\n", File.ReadAllText(Disk("notes/a.md")));

        var deletion = File.ReadAllLines(Disk(".filum/revisions.jsonl"))
            .Select(line => JsonDocument.Parse(line).RootElement)
            .Single(r => r.GetProperty("path").GetString() == "/notes/gone.md" && r.GetProperty("deleted").GetBoolean());
        Assert.Equal((MemoryOperation.Outside, MemoryAuthor.Person), (deletion.GetProperty("operation").GetString(), deletion.GetProperty("author").GetString()));
        Ok(await memory.UndoAsync(_person, MemoryActor.Person, deletion.GetProperty("id").GetInt64(), None));
        Assert.Equal("soon removed\n", File.ReadAllText(Disk("notes/gone.md")));
    }

    [Fact]
    public async Task DeleteAsync_WhenAFileIsDeleted_ShouldRemoveItFromTheDiskAndLetAnUndoBringItBack()
    {
        var memory = Open();
        Ok(await memory.WriteAsync(_person, Agent, "/notes/deep/a.md", "keep me\n", None));

        var deleted = Ok(await memory.DeleteAsync(_person, Agent, "/notes/deep/a.md", None));

        Assert.False(File.Exists(Disk("notes/deep/a.md")));
        Assert.False(Directory.Exists(Disk("notes/deep")));
        Ok(await memory.UndoAsync(_person, MemoryActor.Person, deleted.RevisionId, None));
        Assert.Equal("keep me\n", File.ReadAllText(Disk("notes/deep/a.md")));
    }

    [Fact]
    public async Task AppendAsync_WhenTwoStoresOnOneFolderRaceFromTheSameVersion_ShouldSaveExactlyOneChange()
    {
        var arrived = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<CancellationToken, Task> together = async _ =>
        {
            if (Interlocked.Increment(ref arrived) == 2)
            {
                gate.SetResult();
            }

            await gate.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        };
        Ok(await Open().WriteAsync(_person, Agent, "/notes/a.md", "start\n", None));
        var one = Open(together);
        var two = Open(together);

        var outcomes = await Task.WhenAll(
            one.AppendAsync(_person, Agent, "/notes/a.md", "from one", None),
            two.AppendAsync(_person, Agent, "/notes/a.md", "from two", None));

        Assert.Single(outcomes, o => !o.IsRefused);
        Assert.Contains("changed at the same time", Assert.Single(outcomes, o => o.IsRefused).Refusal);
        var log = File.ReadAllLines(Disk(".filum/revisions.jsonl"));
        Assert.Equal(2, log.Length);
        Assert.All(log, line => JsonDocument.Parse(line));
    }

    [Fact]
    public async Task Open_WhenTheStateIsMissingOrDamaged_ShouldRebuildItFromTheLog()
    {
        var memory = Open();
        Ok(await memory.WriteAsync(_person, Agent, "/notes/a.md", "one\n", None));
        Ok(await memory.SetSensitivityAsync(_person, Agent, "/notes/a.md", MemorySensitivity.Private, None));
        Ok(await memory.WriteAsync(_person, Agent, "/notes/b.md", "two\n", None));
        var before = Ok(await memory.GetHistoryAsync(_person, "/notes/a.md", None)).Select(r => (r.Id, r.Summary)).ToList();

        File.WriteAllText(Disk(".filum/state.json"), "{ not json");
        var rebuilt = Open();

        Assert.Equal(before, Ok(await rebuilt.GetHistoryAsync(_person, "/notes/a.md", None)).Select(r => (r.Id, r.Summary)));
        Assert.Equal(["/notes/b.md"], Ok(await rebuilt.ListAsync(_person, "/", false, None)).Select(f => f.Path));
        Assert.Equal(MemorySensitivity.Private, Ok(await rebuilt.ReadAsync(_person, "/notes/a.md", null, null, None)).Sensitivity);

        File.Delete(Disk(".filum/state.json"));
        Assert.Equal(2, Ok(await Open().ListAsync(_person, "/", true, None)).Count);
    }

    [Fact]
    public async Task WriteAsync_WhenTheStateWasSavedBeforeACrash_ShouldNotReuseRevisionIds()
    {
        var memory = Open();
        Ok(await memory.WriteAsync(_person, Agent, "/notes/a.md", "one\n", None));
        var saved = File.ReadAllText(Disk(".filum/state.json"));
        Ok(await memory.WriteAsync(_person, Agent, "/notes/b.md", "two\n", None));

        File.WriteAllText(Disk(".filum/state.json"), saved);
        Ok(await Open().WriteAsync(_person, Agent, "/notes/c.md", "three\n", None));

        var ids = File.ReadAllLines(Disk(".filum/revisions.jsonl")).Select(l => JsonDocument.Parse(l).RootElement.GetProperty("id").GetInt64()).ToList();
        Assert.Equal(ids.Distinct().Count(), ids.Count);
    }

    [Fact]
    public async Task ForgetConversationAsync_WhenALogLineDoesNotParse_ShouldKeepItAsideWhileRewritingTheLog()
    {
        var memory = Open();
        var conversation = Guid.NewGuid();
        await memory.RecordAsync(_person, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.App, "To forget", ConversationId: conversation), None);
        await memory.RecordAsync(_person, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.App, "To keep", ConversationId: Guid.NewGuid()), None);
        File.AppendAllText(Disk(".filum/events.jsonl"), "{\"id\": 7, \"text\": \"a line edited by ha\n");

        await memory.ForgetConversationAsync(_person, conversation, None);

        Assert.Equal(["{\"id\": 7, \"text\": \"a line edited by ha"], File.ReadAllLines(Disk(".filum/events.jsonl.unreadable")));
        Assert.DoesNotContain(File.ReadAllLines(Disk(".filum/events.jsonl")), l => l.Contains("To forget", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WriteAsync_WhenAnotherPersonUsesTheFolder_ShouldShowThemNothingAndRefuseToWrite()
    {
        var memory = Open();
        Ok(await memory.WriteAsync(_person, Agent, "/notes/a.md", "mine\n", None));
        var other = Guid.NewGuid();

        Assert.Empty(Ok(await memory.ListAsync(other, "/", true, None)));
        Assert.True((await memory.ReadAsync(other, "/notes/a.md", null, null, None)).IsMissing);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => memory.WriteAsync(other, Agent, "/notes/b.md", "theirs\n", None));
        Assert.False(File.Exists(Disk("notes/b.md")));
        Assert.Equal(_person, new LocalFolderStore(_folder).Owner);
    }

    [Fact]
    public async Task Open_WhenAFileIsPrivate_ShouldKeepItPrivateAcrossARestart()
    {
        var memory = Open();
        Ok(await memory.WriteAsync(_person, Agent, "/notes/closed.md", "a shared word\n", None));
        Ok(await memory.WriteAsync(_person, Agent, "/notes/open.md", "a shared word\n", None));
        Ok(await memory.SetSensitivityAsync(_person, Agent, "/notes/closed.md", MemorySensitivity.Private, None));

        var restarted = Open();

        Assert.Equal(["/notes/open.md"], Ok(await restarted.ListAsync(_person, "/", false, None)).Select(f => f.Path));
        Assert.Equal(["/notes/open.md"], Ok(await restarted.SearchAsync(_person, "shared", "/", false, None)).Hits.Select(h => h.Path));
        Assert.Equal(2, Ok(await restarted.ListAsync(_person, "/", true, None)).Count);
        Assert.True(File.Exists(Disk("notes/closed.md")));
    }

    [Fact]
    public async Task BuildIndexAsync_WhenTheFolderHas300Files_ShouldBuildItWellUnderASecond()
    {
        var memory = Open();
        for (var i = 0; i < 300; i++)
        {
            Ok(await memory.WriteAsync(_person, Agent, $"/notes/n{i:D3}.md", $"note {i}\n", None));
        }

        var clock = Stopwatch.StartNew();
        var index = await Open().BuildIndexAsync(_person, None);
        clock.Stop();

        Assert.StartsWith("- /notes/n", index);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1), $"{clock.ElapsedMilliseconds} ms");
        Console.WriteLine($"[spec 012] index of 300 files after a restart: {clock.ElapsedMilliseconds} ms");
    }

    private IMemoryService Open(Func<CancellationToken, Task>? beforeSave = null) =>
        MemoryServices.On(new LocalFolderStore(_folder, _person), beforeSave: beforeSave);

    private string Disk(string relative) => Path.Combine(_folder, relative.Replace('/', Path.DirectorySeparatorChar));
}
