using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

namespace Filum.Engine.Tests;

/// <summary>What the local folder guarantees beyond the memory's contract (spec 012).</summary>
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
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task Files_are_real_files_at_their_paths_and_every_change_is_one_line_of_the_log()
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
    public async Task A_new_store_on_the_same_folder_finds_files_versions_history_and_undo()
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
    public async Task Changes_made_by_hand_become_the_persons_revisions_and_can_be_undone()
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
    public async Task A_deleted_file_leaves_the_disk_and_an_undo_brings_it_back()
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
    public async Task Two_stores_on_one_folder_racing_from_the_same_version_save_exactly_one_change()
    {
        var arrived = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<CancellationToken, Task> together = async _ =>
        {
            if (Interlocked.Increment(ref arrived) == 2)
            {
                gate.SetResult();
            }

            await gate.Task.WaitAsync(TimeSpan.FromSeconds(10));
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
    public async Task A_missing_or_damaged_state_is_rebuilt_from_the_log()
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
    public async Task Another_person_sees_nothing_and_cannot_write()
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
    public async Task A_private_file_stays_private_across_a_restart()
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
    public async Task The_index_of_300_files_is_built_well_under_a_second()
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

    private MemoryService Open(Func<CancellationToken, Task>? beforeSave = null) =>
        new(new LocalFolderStore(_folder, _person), Options.Create(new MemoryOptions()), NullLogger<MemoryService>.Instance, beforeSave);

    private string Disk(string relative) => Path.Combine(_folder, relative.Replace('/', Path.DirectorySeparatorChar));

    private static T Ok<T>(MemoryOutcome<T> outcome) where T : class
    {
        Assert.False(outcome.IsRefused, outcome.Refusal);
        return outcome.Value!;
    }
}
