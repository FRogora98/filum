namespace Filum.Engine.Testing;

/// <summary>
/// What <see cref="MemoryService"/> guarantees on any <see cref="IMemoryStore"/>: the engine's tests run it in memory
/// and on a local folder, and every other store runs it in its own tests. Every test uses fresh user ids.
/// </summary>
public abstract class MemoryServiceContract
{
    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly MemoryActor Agent = MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid());

    /// <summary>The limits most tests run with: small, so they are easy to reach.</summary>
    private static readonly MemoryOptions SmallLimits = new() { MaxFileBytes = 2048, MaxFilesPerUser = 7, MaxCoreChars = 300 };

    private readonly Guid _user = Guid.NewGuid();
    private MemoryService? _small;

    /// <summary>
    /// A memory with these limits over the store under test; every call of one test sees the same store.
    /// <paramref name="beforeSave"/> runs right before each save.
    /// </summary>
    protected abstract MemoryService Memory(MemoryOptions limits, Func<CancellationToken, Task>? beforeSave = null);

    private MemoryService _memory => _small ??= Memory(SmallLimits);

    /// <summary>The person every test writes for; a one-person store is opened for them.</summary>
    protected Guid Person => _user;

    /// <summary>
    /// A store that holds one person only (a local folder): another person sees nothing and cannot write, instead of
    /// having a memory of their own next to the first one's.
    /// </summary>
    protected virtual bool OnePersonPerStore => false;

    [Fact]
    public async Task The_core_is_created_once_from_the_template()
    {
        var first = await _memory.EnsureCoreAsync(_user, None);
        var second = await _memory.EnsureCoreAsync(_user, None);

        Assert.Equal(PlatformInstructions.CoreTemplate, first);
        Assert.Equal(first, second);
        var history = Ok(await _memory.GetHistoryAsync(_user, "/filum.md", None));
        var created = Assert.Single(history);
        Assert.Equal((MemoryOperation.CreateCore, MemoryAuthor.Platform), (created.Operation, created.Author));
    }

    [Fact]
    public async Task Written_files_can_be_listed_read_and_searched()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/plans.md", "# Plans\nVisit the old bridge\r\nCall the library\n", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/collections/reading.csv", "title,year\nFirst book,2024\nSecond book,2025\n", None));

        var files = Ok(await _memory.ListAsync(_user, "/", false, None));
        Assert.Equal(["/collections/reading.csv", "/notes/plans.md"], files.Select(f => f.Path));
        Assert.Equal(("title,year", 2), (files[0].Header, files[0].RowCount));
        Assert.Equal((3, (string?)null), (files[1].LineCount, files[1].Header));
        Assert.Equal(["/notes/plans.md"], Ok(await _memory.ListAsync(_user, "/notes", false, None)).Select(f => f.Path));

        var read = Ok(await _memory.ReadAsync(_user, "/notes/plans.md", null, null, None));
        Assert.Equal(["# Plans", "Visit the old bridge", "Call the library"], read.Lines);

        var hits = Ok(await _memory.SearchAsync(_user, "BRIDGE", "/", false, None)).Hits;
        Assert.Equal([("/notes/plans.md", 2, "Visit the old bridge")], hits.Select(h => (h.Path, h.Line, h.Text)));
        Assert.Empty(Ok(await _memory.SearchAsync(_user, "50%_off", "/", false, None)).Hits);
    }

    [Fact]
    public async Task An_edit_replaces_exactly_one_occurrence()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "one two\nthree two\n", None));

        Assert.Contains("not found", Refused(await _memory.EditAsync(_user, Agent, "/notes/a.md", "four", "x", None)));
        Assert.Contains("appears 2 times", Refused(await _memory.EditAsync(_user, Agent, "/notes/a.md", "two", "x", None)));
        Ok(await _memory.EditAsync(_user, Agent, "/notes/a.md", "three two", "three 2", None));

        Assert.Equal(["one two", "three 2"], Ok(await _memory.ReadAsync(_user, "/notes/a.md", null, null, None)).Lines);
    }

    [Fact]
    public async Task Append_adds_rows_and_creates_a_missing_file()
    {
        var created = Ok(await _memory.AppendAsync(_user, Agent, "/collections/list.csv", "item,qty\nfirst,1", None));
        var added = Ok(await _memory.AppendAsync(_user, Agent, "/collections/list.csv", "second,2\nthird,3\n", None));
        Ok(await _memory.AppendAsync(_user, Agent, "/notes/log.md", "a line", None));
        Ok(await _memory.AppendAsync(_user, Agent, "/notes/log.md", "another line", None));

        Assert.True(created.Created);
        Assert.Equal((false, 3, 2), (added.Created, added.RowCount, added.Added));
        Assert.Equal(["a line", "another line"], Ok(await _memory.ReadAsync(_user, "/notes/log.md", null, null, None)).Lines);
    }

    [Fact]
    public async Task Delete_and_move_keep_the_history_and_protect_the_core()
    {
        await _memory.EnsureCoreAsync(_user, None);
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "a\n", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/b.md", "b\n", None));

        Assert.Contains("already a file", Refused(await _memory.MoveAsync(_user, Agent, "/notes/a.md", "/notes/b.md", None)));
        Assert.Contains("cannot be moved", Refused(await _memory.MoveAsync(_user, Agent, "/filum.md", "/core.md", None)));
        Assert.Contains("cannot be moved", Refused(await _memory.MoveAsync(_user, Agent, "/notes/a.md", "/filum.md", None)));
        Assert.Contains("cannot be deleted", Refused(await _memory.DeleteAsync(_user, Agent, "/filum.md", None)));

        var moved = Ok(await _memory.MoveAsync(_user, Agent, "/notes/a.md", "/archive/a.md", None));
        Assert.Equal("/notes/a.md", moved.FromPath);
        Assert.Equal(2, Ok(await _memory.GetHistoryAsync(_user, "/archive/a.md", None)).Count);

        Ok(await _memory.DeleteAsync(_user, Agent, "/notes/b.md", None));
        Assert.Contains("no file", Refused(await _memory.ReadAsync(_user, "/notes/b.md", null, null, None)));
        Assert.Equal(["/archive/a.md", "/filum.md"], Ok(await _memory.ListAsync(_user, "/", false, None)).Select(f => f.Path).Where(p => !Skills.IsSkillPath(p)));
    }

    [Fact]
    public async Task Private_files_are_left_out_unless_asked_for()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/open.md", "a shared word\n", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/closed.md", "a shared word\n", None));
        Ok(await _memory.SetSensitivityAsync(_user, Agent, "/notes/closed.md", MemorySensitivity.Private, None));

        Assert.Equal(["/notes/open.md"], Ok(await _memory.ListAsync(_user, "/", false, None)).Select(f => f.Path));
        Assert.Equal(["/notes/open.md"], Ok(await _memory.SearchAsync(_user, "shared", "/", false, None)).Hits.Select(h => h.Path));
        Assert.Equal(2, Ok(await _memory.ListAsync(_user, "/", true, None)).Count);
        Assert.Equal(2, Ok(await _memory.SearchAsync(_user, "shared", "/", true, None)).Hits.Count);
        Assert.Equal(MemorySensitivity.Private, Ok(await _memory.ReadAsync(_user, "/notes/closed.md", null, null, None)).Sensitivity);

        Assert.Contains("normal, sensitive or private", Refused(await _memory.SetSensitivityAsync(_user, Agent, "/notes/open.md", "secret", None)));
        await _memory.EnsureCoreAsync(_user, None);
        Assert.Contains("cannot change", Refused(await _memory.SetSensitivityAsync(_user, Agent, "/filum.md", MemorySensitivity.Private, None)));
    }

    [Fact]
    public async Task Limits_refuse_the_change_and_leave_memory_as_it_was()
    {
        await _memory.EnsureCoreAsync(_user, None);
        Assert.Contains("over the limit of 2 KB", Refused(await _memory.WriteAsync(_user, Agent, "/notes/big.md", new string('x', 2100), None)));
        Assert.Contains("condense it", Refused(await _memory.WriteAsync(_user, Agent, "/filum.md", new string('x', 301), None)));
        Assert.Equal(PlatformInstructions.CoreTemplate, await _memory.EnsureCoreAsync(_user, None));

        Ok(await _memory.WriteAsync(_user, Agent, "/notes/1.md", "1", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/2.md", "2", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/3.md", "3", None));
        Assert.Contains("memory is full (7 files)", Refused(await _memory.WriteAsync(_user, Agent, "/notes/4.md", "4", None)));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/3.md", "replacing is fine", None));

        Assert.Contains("'..'", Refused(await _memory.WriteAsync(_user, Agent, "/notes/../x.md", "x", None)));
        Assert.Contains("Only .md", Refused(await _memory.WriteAsync(_user, Agent, "/notes/x.txt", "x", None)));
        Assert.Equal(7, Ok(await _memory.ListAsync(_user, "/", true, None)).Count);
    }

    [Fact]
    public async Task A_collection_row_with_the_wrong_number_of_fields_is_refused()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/collections/list.csv", "a,b,c\n1,2,3\n", None));

        Assert.Contains("Line 3 has 2 fields, the header has 3", Refused(await _memory.AppendAsync(_user, Agent, "/collections/list.csv", "4,5", None)));
        Assert.Contains("Line 3 has 4 fields", Refused(await _memory.WriteAsync(_user, Agent, "/collections/list.csv", "a,b,c\n1,2,3\n1,2,3,4\n", None)));

        Assert.Equal(["a,b,c", "1,2,3"], Ok(await _memory.ReadAsync(_user, "/collections/list.csv", null, null, None)).Lines);
    }

    [Fact]
    public async Task Every_change_is_a_revision_with_its_author()
    {
        var turn = MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid());
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        Ok(await _memory.WriteAsync(_user, turn, "/notes/a.md", "first\n", None));
        Ok(await _memory.AppendAsync(_user, MemoryActor.Person, "/notes/a.md", "second", None));

        var history = Ok(await _memory.GetHistoryAsync(_user, "/notes/a.md", None));

        Assert.Equal([(MemoryOperation.Append, MemoryAuthor.Person), (MemoryOperation.Write, MemoryAuthor.Agent)], history.Select(r => (r.Operation, r.Author)));
        Assert.Equal((turn.ConversationId, turn.MessageId), (history[1].ConversationId, history[1].MessageId));
        Assert.All(history, r => Assert.InRange(r.CreatedAt, before, DateTimeOffset.UtcNow.AddSeconds(5)));
        Assert.Equal("first\n", Ok(await _memory.GetRevisionAsync(_user, history[1].Id, None)).Content);
    }

    [Fact]
    public async Task A_long_file_is_read_in_pages()
    {
        var memory = Memory(new MemoryOptions());
        Ok(await memory.WriteAsync(_user, Agent, "/notes/long.md", string.Join("\n", Enumerable.Range(1, 1200).Select(i => $"line {i}")), None));

        var first = Ok(await memory.ReadAsync(_user, "/notes/long.md", null, null, None));
        var rest = Ok(await memory.ReadAsync(_user, "/notes/long.md", 1001, 2000, None));

        Assert.Equal((1, 500, 1200, 500), (first.FromLine, first.ToLine, first.TotalLines, first.Lines.Count));
        Assert.Equal(("line 1001", "line 1200", 200), (rest.Lines[0], rest.Lines[^1], rest.Lines.Count));
        Assert.Contains("has 1200 lines", Refused(await memory.ReadAsync(_user, "/notes/long.md", 1300, null, None)));
    }

    [Fact]
    public async Task Another_persons_memory_does_not_exist()
    {
        var other = Guid.NewGuid();
        var change = Ok(await _memory.WriteAsync(_user, Agent, "/notes/mine.md", "mine\n", None));

        Assert.Empty(Ok(await _memory.ListAsync(other, "/", true, None)));
        Assert.Empty(Ok(await _memory.SearchAsync(other, "mine", "/", true, None)).Hits);
        Assert.Contains("no file", Refused(await _memory.ReadAsync(other, "/notes/mine.md", null, null, None)));
        Assert.Contains("no file", Refused(await _memory.EditAsync(other, Agent, "/notes/mine.md", "mine", "yours", None)));
        Assert.Contains("no file", Refused(await _memory.DeleteAsync(other, Agent, "/notes/mine.md", None)));
        Assert.Contains("no file", Refused(await _memory.GetHistoryAsync(other, "/notes/mine.md", None)));
        Assert.Contains("no change", Refused(await _memory.UndoAsync(other, MemoryActor.Person, change.RevisionId, None)));

        if (OnePersonPerStore)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _memory.WriteAsync(other, Agent, "/notes/mine.md", "theirs\n", None));
        }
        else
        {
            Ok(await _memory.WriteAsync(other, Agent, "/notes/mine.md", "theirs\n", None));
        }

        Assert.Equal(["mine"], Ok(await _memory.ReadAsync(_user, "/notes/mine.md", null, null, None)).Lines);
    }

    [Fact]
    public async Task Two_changes_from_the_same_version_cannot_both_win()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "start\n", None));
        var racing = RacingService(2);

        var results = await Task.WhenAll(
            racing.AppendAsync(_user, Agent, "/notes/a.md", "from one", None),
            racing.AppendAsync(_user, Agent, "/notes/a.md", "from two", None));

        Assert.Single(results, r => !r.IsRefused);
        Assert.Contains("changed at the same time", Assert.Single(results, r => r.IsRefused).Refusal);
        Assert.Equal(2, Ok(await _memory.ReadAsync(_user, "/notes/a.md", null, null, None)).TotalLines);
    }

    [Fact]
    public async Task Two_creations_of_the_same_path_cannot_both_win()
    {
        var racing = RacingService(2);

        var results = await Task.WhenAll(
            racing.WriteAsync(_user, Agent, "/notes/new.md", "one\n", None),
            racing.WriteAsync(_user, Agent, "/notes/new.md", "two\n", None));

        Assert.Single(results, r => !r.IsRefused);
        Assert.Contains("changed at the same time", Assert.Single(results, r => r.IsRefused).Refusal);
        Assert.Single(Ok(await _memory.ListAsync(_user, "/", true, None)));
    }

    [Fact]
    public async Task An_undo_brings_back_the_previous_state_as_a_new_revision()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "v1\n", None));
        var edit = Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "v2\n", None));

        var undo = Ok(await _memory.UndoAsync(_user, MemoryActor.Person, edit.RevisionId, None));

        Assert.Equal(["v1"], Ok(await _memory.ReadAsync(_user, "/notes/a.md", null, null, None)).Lines);
        var history = Ok(await _memory.GetHistoryAsync(_user, "/notes/a.md", None));
        Assert.Equal((undo.RevisionId, MemoryOperation.Undo, MemoryAuthor.Person, (long?)edit.RevisionId), (history[0].Id, history[0].Operation, history[0].Author, history[0].UndoesRevisionId));
        Assert.Equal(3, history.Count);
    }

    [Fact]
    public async Task Undo_takes_back_a_creation_a_delete_and_a_move()
    {
        var created = Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "a\n", None));
        Ok(await _memory.UndoAsync(_user, MemoryActor.Person, created.RevisionId, None));
        Assert.Empty(Ok(await _memory.ListAsync(_user, "/", true, None)));

        Ok(await _memory.WriteAsync(_user, Agent, "/notes/b.md", "b\n", None));
        var deleted = Ok(await _memory.DeleteAsync(_user, Agent, "/notes/b.md", None));
        Ok(await _memory.UndoAsync(_user, MemoryActor.Person, deleted.RevisionId, None));
        Assert.Equal(["b"], Ok(await _memory.ReadAsync(_user, "/notes/b.md", null, null, None)).Lines);

        var moved = Ok(await _memory.MoveAsync(_user, Agent, "/notes/b.md", "/archive/b.md", None));
        Ok(await _memory.UndoAsync(_user, MemoryActor.Person, moved.RevisionId, None));
        Assert.Equal(["/notes/b.md"], Ok(await _memory.ListAsync(_user, "/", true, None)).Select(f => f.Path));
    }

    [Fact]
    public async Task An_undo_is_refused_when_the_file_changed_again_afterwards()
    {
        var first = Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "v1\n", None));
        var later = Ok(await _memory.AppendAsync(_user, MemoryActor.Person, "/notes/a.md", "v2", None));

        var refusal = Refused(await _memory.UndoAsync(_user, MemoryActor.Person, first.RevisionId, None));

        Assert.Contains($"change {later.RevisionId}", refusal);
        Assert.Contains("append by you", refusal);
        Assert.Equal(["v1", "v2"], Ok(await _memory.ReadAsync(_user, "/notes/a.md", null, null, None)).Lines);
    }

    [Fact]
    public async Task A_rollback_takes_back_a_turns_changes_newest_first()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/kept.md", "before\n", None));
        var turn = new List<long>
        {
            Ok(await _memory.AppendAsync(_user, Agent, "/notes/kept.md", "during", None)).RevisionId,
            Ok(await _memory.WriteAsync(_user, Agent, "/notes/new.md", "new\n", None)).RevisionId,
            Ok(await _memory.MoveAsync(_user, Agent, "/notes/kept.md", "/notes/moved.md", None)).RevisionId
        };

        await _memory.RollbackAsync(_user, turn, None);

        Assert.Equal(["/notes/kept.md"], Ok(await _memory.ListAsync(_user, "/", true, None)).Select(f => f.Path));
        Assert.Equal(["before"], Ok(await _memory.ReadAsync(_user, "/notes/kept.md", null, null, None)).Lines);
        Assert.Equal(MemoryOperation.Rollback, Ok(await _memory.GetHistoryAsync(_user, "/notes/kept.md", None))[0].Operation);
    }

    [Fact]
    public async Task A_rollback_keeps_a_file_that_someone_else_changed_since()
    {
        var turn = Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "from the turn\n", None)).RevisionId;
        Ok(await _memory.AppendAsync(_user, MemoryActor.Person, "/notes/a.md", "from the person", None));

        await _memory.RollbackAsync(_user, [turn], None);

        Assert.Equal(["from the turn", "from the person"], Ok(await _memory.ReadAsync(_user, "/notes/a.md", null, null, None)).Lines);
    }

    [Fact]
    public async Task A_restore_brings_back_any_version_even_after_later_changes()
    {
        var first = Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "v1\n", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "v2\n", None));
        Ok(await _memory.AppendAsync(_user, Agent, "/notes/a.md", "v3", None));

        var restored = Ok(await _memory.RestoreAsync(_user, MemoryActor.Person, first.RevisionId, None));

        Assert.Equal(["v1"], Ok(await _memory.ReadAsync(_user, "/notes/a.md", null, null, None)).Lines);
        var newest = Ok(await _memory.GetHistoryAsync(_user, "/notes/a.md", None))[0];
        Assert.Equal((restored.RevisionId, MemoryOperation.Restore, MemoryAuthor.Person, "Restored an earlier version"), (newest.Id, newest.Operation, newest.Author, newest.Summary));
    }

    [Fact]
    public async Task A_restore_of_a_deletion_or_of_a_taken_path_is_refused()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "a\n", None));
        var deleted = Ok(await _memory.DeleteAsync(_user, Agent, "/notes/a.md", None));
        Assert.Contains("deletion of the file", Refused(await _memory.RestoreAsync(_user, MemoryActor.Person, deleted.RevisionId, None)));

        var old = Ok(await _memory.WriteAsync(_user, Agent, "/notes/b.md", "b\n", None));
        Ok(await _memory.MoveAsync(_user, Agent, "/notes/b.md", "/notes/c.md", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/b.md", "another b\n", None));
        Assert.Contains("Another file now uses /notes/b.md", Refused(await _memory.RestoreAsync(_user, MemoryActor.Person, old.RevisionId, None)));

        var missing = await _memory.RestoreAsync(Guid.NewGuid(), MemoryActor.Person, old.RevisionId, None);
        Assert.True(missing.IsMissing);
    }

    [Fact]
    public async Task The_history_says_what_each_change_did()
    {
        Ok(await _memory.AppendAsync(_user, Agent, "/collections/list.csv", "item\nfirst\nsecond", None));
        Ok(await _memory.AppendAsync(_user, MemoryActor.Person, "/collections/list.csv", "third", None));
        Ok(await _memory.EditAsync(_user, Agent, "/collections/list.csv", "third", "3rd", None));
        Ok(await _memory.SetSensitivityAsync(_user, Agent, "/collections/list.csv", MemorySensitivity.Private, None));
        Ok(await _memory.MoveAsync(_user, Agent, "/collections/list.csv", "/collections/items.csv", None));
        var write = Ok(await _memory.WriteAsync(_user, Agent, "/collections/items.csv", "item\nonly\n", None));
        Ok(await _memory.UndoAsync(_user, MemoryActor.Person, write.RevisionId, None));

        var history = Ok(await _memory.GetHistoryAsync(_user, "/collections/items.csv", None));

        Assert.Equal(
            ["Undid a change", "Rewrote", "Moved from /collections/list.csv", "Marked private", "Changed rows", "Added 1 row", "Created with 2 rows"],
            history.Select(r => r.Summary));
    }

    [Fact]
    public async Task A_version_can_be_read_by_its_id_only_by_its_owner()
    {
        var change = Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "v1\n", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "v2\n", None));

        var version = Ok(await _memory.GetRevisionAsync(_user, change.RevisionId, None));

        Assert.Equal(("/notes/a.md", "v1\n", false), (version.Path, version.Content, version.Deleted));
        Assert.True((await _memory.GetRevisionAsync(Guid.NewGuid(), change.RevisionId, None)).IsMissing);
    }

    [Fact]
    public async Task Missing_files_are_told_apart_from_refusals()
    {
        Assert.True((await _memory.ReadAsync(_user, "/notes/none.md", null, null, None)).IsMissing);
        Assert.True((await _memory.EditAsync(_user, Agent, "/notes/none.md", "a", "b", None)).IsMissing);
        Assert.True((await _memory.DeleteAsync(_user, Agent, "/notes/none.md", None)).IsMissing);
        Assert.True((await _memory.GetHistoryAsync(_user, "/notes/none.md", None)).IsMissing);
        Assert.True((await _memory.UndoAsync(_user, Agent, long.MaxValue, None)).IsMissing);

        var refused = await _memory.WriteAsync(_user, Agent, "/notes/x.txt", "x", None);
        Assert.True(refused.IsRefused);
        Assert.False(refused.IsMissing);
    }

    [Fact]
    public async Task The_index_lists_every_non_private_file_newest_first_and_never_the_core()
    {
        await _memory.EnsureCoreAsync(_user, None);
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/old.md", "one\ntwo\n", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/lists/films.csv", "title,year\nDune,2021\n", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/secret.md", "hidden\n", None));
        Ok(await _memory.SetSensitivityAsync(_user, Agent, "/notes/secret.md", MemorySensitivity.Private, None));

        var lines = (await _memory.BuildIndexAsync(_user, None)).Split('\n');

        Assert.Equal(2, lines.Length);
        Assert.StartsWith("- /lists/films.csv (collection · fields: title, year · 1 rows · changed ", lines[0]);
        Assert.StartsWith("- /notes/old.md (document · 2 lines · changed ", lines[1]);
        Assert.Equal("(nothing yet besides the core)", await _memory.BuildIndexAsync(Guid.NewGuid(), None));
    }

    [Fact]
    public async Task The_map_is_made_from_the_files_with_what_each_holds_and_the_core_keeps_no_map()
    {
        var core = await _memory.EnsureCoreAsync(_user, None);
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/trip.md", "# The trip\n\nPlans for the walk along the river in May.\nDay one.\n", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/empty.md", "# Only a title\n", None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "the person", "plants", "3", null, null, [], None));

        var lines = (await _memory.BuildIndexAsync(_user, None)).Split('\n');

        Assert.DoesNotContain("map", core, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith($"- {Facts.Path} (facts that hold for a time · 1 rows", lines[0]);
        Assert.EndsWith("): Only a title", lines[1]);
        Assert.EndsWith("): Plans for the walk along the river in May.", lines[2]);
    }

    [Fact]
    public async Task Past_its_limit_the_index_lists_the_newest_files_and_says_how_many_are_left_out()
    {
        var memory = Memory(new MemoryOptions { IndexMaxFiles = 2 });
        foreach (var name in new[] { "a", "b", "c", "d" })
        {
            Ok(await memory.WriteAsync(_user, Agent, $"/notes/{name}.md", name, None));
        }

        var lines = (await memory.BuildIndexAsync(_user, None)).Split('\n');

        Assert.Equal(3, lines.Length);
        Assert.StartsWith("- /notes/d.md", lines[0]);
        Assert.StartsWith("- /notes/c.md", lines[1]);
        Assert.Equal("- …and 2 older files not listed here: memory_search finds them.", lines[2]);
    }

    [Fact]
    public async Task Events_are_appended_in_order_and_read_back_with_filters()
    {
        var chat = Guid.NewGuid();
        var said = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "I moved to\r\nthe coast", new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero), chat, Guid.NewGuid()), None);
        var answered = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Answered, MemoryEventSource.Agent, "Noted.", ConversationId: chat), None);
        var derived = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Derived, MemoryEventSource.Agent, "Wrote 1 change", Sources: [said.Id], Revisions: [7]), None);

        Assert.True(said.Id < answered.Id && answered.Id < derived.Id);
        Assert.Equal("I moved to\nthe coast", said.Text);
        Assert.Equal(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero), said.OccurredAt);
        Assert.True(answered.OccurredAt == answered.RecordedAt);

        var all = await _memory.EventsAsync(_user, new EventQuery(), None);
        Assert.Equal([said.Id, answered.Id, derived.Id], all.Select(e => e.Id));
        Assert.Equal([said.Id], all[2].Sources);
        Assert.Equal([7L], all[2].Revisions);
        Assert.Equal([answered.Id, derived.Id], (await _memory.EventsAsync(_user, new EventQuery(AfterId: said.Id), None)).Select(e => e.Id));
        Assert.Equal([said.Id, answered.Id], (await _memory.EventsAsync(_user, new EventQuery(ConversationId: chat), None)).Select(e => e.Id));
        Assert.Equal([derived.Id], (await _memory.EventsAsync(_user, new EventQuery(Kinds: [MemoryEventKind.Derived]), None)).Select(e => e.Id));
        Assert.Equal([said.Id], (await _memory.EventsAsync(_user, new EventQuery(Before: new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero)), None)).Select(e => e.Id));
    }

    [Fact]
    public async Task A_search_of_the_events_ranks_what_was_said_and_gives_the_reply_after_it()
    {
        var chat = Guid.NewGuid();
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "The weather is fine today", ConversationId: chat), None);
        var asked = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "My bicycle is the blue one with a basket", ConversationId: chat), None);
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Answered, MemoryEventSource.Agent, "A blue bicycle, noted.", ConversationId: chat), None);
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Derived, MemoryEventSource.Agent, "bicycle bicycle bicycle"), None);
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "my private bicycle code", Sensitivity: MemorySensitivity.Private), None);

        var hits = Ok(await _memory.SearchEventsAsync(_user, "Bicycle basket", null, null, includePrivate: false, None));

        Assert.Equal(asked.Id, hits[0].Event.Id);
        Assert.Equal("A blue bicycle, noted.", hits[0].Next?.Text);
        Assert.DoesNotContain(hits, h => h.Event.Kind == MemoryEventKind.Derived || h.Event.Sensitivity == MemorySensitivity.Private);
        Assert.Contains(Ok(await _memory.SearchEventsAsync(_user, "code", null, null, includePrivate: true, None)), h => h.Event.Sensitivity == MemorySensitivity.Private);
        Assert.Empty(Ok(await _memory.SearchEventsAsync(_user, "bicycle", DateTimeOffset.UtcNow.AddDays(1), null, false, None)));
        Assert.Contains("between 1 and 200", Refused(await _memory.SearchEventsAsync(_user, " ", null, null, false, None)));
    }

    [Fact]
    public async Task Forgetting_removes_events_and_whole_files_for_good()
    {
        var kept = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "keep this"), None);
        var gone = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "forget this"), None);
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/keep.md", "keep\n", None));
        var dropped = Ok(await _memory.WriteAsync(_user, Agent, "/notes/drop.md", "drop\n", None));
        Ok(await _memory.AppendAsync(_user, Agent, "/notes/drop.md", "more", None));
        await _memory.ForgetAsync(_user, [gone.Id], ["/notes/drop.md"], None);

        Assert.Equal([kept.Id], (await _memory.EventsAsync(_user, new EventQuery(), None)).Select(e => e.Id));
        Assert.Equal(["/notes/keep.md"], Ok(await _memory.ListAsync(_user, "/", true, None)).Select(f => f.Path));
        Assert.Contains("no change", Refused(await _memory.GetRevisionAsync(_user, dropped.RevisionId, None)));
        var next = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "after"), None);
        Assert.True(next.Id > gone.Id);
    }

    [Fact]
    public async Task A_fact_that_changes_closes_the_old_value_and_keeps_both_with_their_sources()
    {
        Ok(await _memory.RecordFactAsync(_user, Agent, "the person", "plants", "3", "2026-01-10", null, [4], None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "the person", "lives in", "the coast", "2026-01-10", null, [4], None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "The Person", "Plants", "4", "2026-02-01", "a new fern", [9, 8], None));

        var now = Ok(await _memory.CurrentFactsAsync(_user, "the person", None));
        Assert.Equal([("lives in", "the coast"), ("Plants", "4")], now.Select(f => (f.Attribute, f.Value)).OrderBy(f => f.Attribute, StringComparer.OrdinalIgnoreCase));
        var history = Ok(await _memory.FactHistoryAsync(_user, "the person", "plants", None));
        Assert.Equal([("3", "2026-01-10", "2026-02-01"), ("4", "2026-02-01", "")], history.Select(f => (f.Value, f.ValidFrom, f.ValidTo)));
        Assert.Equal([4L], history[0].Sources);
        Assert.Equal([8L, 9L], history[1].Sources);
        Assert.Equal("a new fern", history[1].Note);

        var file = Ok(await _memory.ReadAsync(_user, Facts.Path, null, null, None));
        Assert.Equal(string.Join(",", Facts.Header), file.Lines[0]);
        Assert.Contains("already 4", Refused(await _memory.RecordFactAsync(_user, Agent, "the person", "plants", "4", null, null, [], None)));
        Assert.Contains("later day", Refused(await _memory.RecordFactAsync(_user, Agent, "the person", "plants", "5", "2026-01-15", null, [], None)));
        Assert.Contains("yyyy-MM-dd", Refused(await _memory.RecordFactAsync(_user, Agent, "the person", "plants", "5", "next week", null, [], None)));
        Assert.Empty(Ok(await _memory.CurrentFactsAsync(_user, "someone else", None)));
    }

    [Fact]
    public async Task Another_persons_events_do_not_exist()
    {
        var other = Guid.NewGuid();
        var mine = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "mine"), None);

        Assert.Empty(await _memory.EventsAsync(other, new EventQuery(), None));
        Assert.Empty(Ok(await _memory.SearchEventsAsync(other, "mine", null, null, true, None)));
        await _memory.ForgetAsync(other, [mine.Id], [], None);
        Assert.Single(await _memory.EventsAsync(_user, new EventQuery(), None));

        if (OnePersonPerStore)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _memory.RecordAsync(other, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "theirs"), None));
        }
    }

    /// <summary>A service whose changes all wait for each other right before saving, so they race from the same version.</summary>
    private MemoryService RacingService(int racers)
    {
        var arrived = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return Memory(SmallLimits,
            async _ =>
            {
                if (Interlocked.Increment(ref arrived) == racers)
                {
                    gate.SetResult();
                }

                await gate.Task.WaitAsync(TimeSpan.FromSeconds(10));
            });
    }

    private static T Ok<T>(MemoryOutcome<T> outcome) where T : class
    {
        Assert.True(!outcome.IsRefused, outcome.Refusal);
        return outcome.Value!;
    }

    private static string Refused<T>(MemoryOutcome<T> outcome) where T : class
    {
        Assert.True(outcome.IsRefused, "expected a refusal");
        return outcome.Refusal!;
    }
}
