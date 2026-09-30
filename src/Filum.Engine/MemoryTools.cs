using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text.Json;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Filum.Engine;

/// <summary>
/// The agent's tools on the person's memory, built for one turn: they act for the turn's user only (there is no user
/// parameter), count their calls against the turn's limit, and record one step per call with a description written
/// here from what the tool really did. The revisions the turn created are kept so a failed turn can be rolled back.
/// </summary>
public sealed class MemoryTools
{
    private readonly MemoryService _memory;
    private readonly MemoryOptions _limits;
    private readonly Guid _userId;
    private readonly MemoryActor _actor;
    private readonly List<ToolStep> _steps = [];
    private readonly List<long> _revisions = [];
    private readonly List<string> _savedSkills = [];
    private readonly Lock _gate = new();
    private SkillProposal? _proposal;
    private int _calls;

    public MemoryTools(MemoryService memory, MemoryOptions limits, Guid userId, MemoryActor actor)
    {
        _memory = memory;
        _limits = limits;
        _userId = userId;
        _actor = actor;
        IEnumerable<AIFunction> all =
        [
            AIFunctionFactory.Create(Overview, "memory_overview",
                "What the person's memory holds, in one call: their core (/filum.md: who they are, their rules, how they want to be answered, the memory map), one line for every file (documents, and collections with their fields), and their enabled skills. Call it first when you start helping the person; you do not need to look through files one by one."),
            AIFunctionFactory.Create(Read, "memory_read",
                $"Read one file of the person's memory with line numbers, at most {limits.MaxReadLines} lines per call; use fromLine and toLine to read further. Read a file only when you need its content."),
            AIFunctionFactory.Create(Search, "memory_search",
                $"Find the lines that contain a text (ignoring case) across the person's memory or under a folder prefix, at most {limits.MaxSearchResults}: for a detail you cannot place. Private files are left out unless includePrivate is true, which you set only when the person asks for private content now."),
            AIFunctionFactory.Create(Write, "memory_write",
                "Create a document (a path ending in .md) or replace its whole content. Save what the person tells you that is worth remembering, when they say it; when they call it private or sensitive, mark the file with memory_set_sensitivity right after. For collections use the collection tools. Prefer memory_append to add to a document and memory_edit for small changes."),
            AIFunctionFactory.Create(Edit, "memory_edit",
                "Replace one exact occurrence of oldText with newText in a document. Fails if oldText is missing or appears more than once: then include more of the surrounding text."),
            AIFunctionFactory.Create(Append, "memory_append",
                "Add text at the end of a document, creating it if it does not exist. For collections use collection_add_rows."),
            AIFunctionFactory.Create(AddRows, "collection_add_rows",
                "Add entries to a collection (a path ending in .csv): one kind of information that repeats, one row per entry, each row an object of field → value. A missing collection is created with the rows' fields. The engine writes the file; never write a collection as text."),
            AIFunctionFactory.Create(UpdateRows, "collection_update_rows",
                "Change rows of a collection: every row whose fields equal all of 'where' gets the values of 'set'. Example: where {\"title\": \"X\"}, set {\"status\": \"seen\"}."),
            AIFunctionFactory.Create(RemoveRows, "collection_remove_rows",
                "Remove the rows of a collection whose fields equal all of 'where'. Example: where {\"item\": \"bread\"}."),
            AIFunctionFactory.Create(AddField, "collection_add_field",
                "Add a new field to a collection; existing rows get an empty value. Only when a new kind of detail is really needed."),
            AIFunctionFactory.Create(Delete, "memory_delete",
                "Delete a file. It stays in the history and can be brought back with memory_undo. The core /filum.md cannot be deleted."),
            AIFunctionFactory.Create(Move, "memory_move",
                "Rename or move a file, keeping its history. Update the memory map in the core afterwards."),
            AIFunctionFactory.Create(SetSensitivity, "memory_set_sensitivity",
                "Set a file's sensitivity: normal, sensitive (never brought up unprompted) or private (left out of lists and searches unless asked for)."),
            AIFunctionFactory.Create(History, "memory_history",
                "The history of one file, newest first: what each change did, who made it and when, with the id of each change for memory_undo."),
            AIFunctionFactory.Create(Undo, "memory_undo",
                "Undo one change by its id (from memory_history or from the result of the tool that made it): the file returns to how it was before. Refused when the file changed again since: undo the later change first."),
            AIFunctionFactory.Create(Aggregate, "collection_aggregate",
                "Compute count, sum, average, min or max of a column of a collection, exactly, in code. Optionally group by a column, or by the day, week, month or year of a date column (groupBy plus period); filter rows by a column value (filterColumn plus filterValue) or by a date range (dateColumn plus from and to, as yyyy-MM-dd). Use it for every total, average or count over a collection."),
            AIFunctionFactory.Create(ListSkills, "skill_list",
                "The person's skills, the procedures they keep: name, what it does, when it applies, on or off. Private skills only with includePrivate, when the person asks for them."),
            AIFunctionFactory.Create(UseSkill, "skill_use",
                "Get the full steps of one of the person's enabled skills, by name, when what they ask matches its 'when'; then follow them."),
            AIFunctionFactory.Create(SaveSkill, "skill_save",
                "Save a skill: a procedure the person wants followed again, with a short name, one line of what it does, one line of when it applies, and plain numbered steps. The engine writes the file. A new name creates it; to change an existing skill set replace to true."),
            AIFunctionFactory.Create(ProposeSkill, "skill_propose",
                "Propose a skill without saving it, when the person keeps asking for the same kind of thing and no skill covers it: the person's app, if any, shows it to them. Ask them in your answer whether to save it; save it with skill_save only when they agree."),
            AIFunctionFactory.Create(SetSkillEnabled, "skill_set_enabled",
                "Turn one of the person's skills off (it is kept but no longer used) or back on.")
        ];
        var excluded = new HashSet<string>(limits.ExcludedTools, StringComparer.Ordinal);
        Tools = all.Where(t => !excluded.Contains(t.Name)).Cast<AITool>().ToList();
    }

    /// <summary>
    /// The catalog of the engine's tools, as any host sees them with these limits: names, descriptions and parameter
    /// schemas. The one source of truth of the tool surface (spec 011).
    /// </summary>
    public static IReadOnlyList<AIFunction> Catalog(MemoryOptions limits) =>
        new MemoryTools(new MemoryService(new InMemoryMemoryStore(), Microsoft.Extensions.Options.Options.Create(limits), Microsoft.Extensions.Logging.Abstractions.NullLogger<MemoryService>.Instance), limits, Guid.Empty, MemoryActor.Platform)
            .Tools.Cast<AIFunction>().ToList();

    /// <summary>The last skill proposed in the turn, if any.</summary>
    public SkillProposal? Proposal
    {
        get
        {
            lock (_gate)
            {
                return _proposal;
            }
        }
    }

    /// <summary>The names of the skills the turn saved.</summary>
    public IReadOnlyList<string> SavedSkills
    {
        get
        {
            lock (_gate)
            {
                return _savedSkills.ToList();
            }
        }
    }

    /// <summary>Records that the person invoked a skill by name: the code gave it to the agent, no tool was called.</summary>
    public void RecordInvoked(SkillFile skill) =>
        Record(new ToolStep(ToolStep.Read, "skill", skill.Path, $"Used skill /{skill.Skill.Name}", 0, null));

    public IList<AITool> Tools { get; }

    /// <summary>The steps of the turn so far, in the order the calls finished.</summary>
    public IReadOnlyList<ToolStep> Steps
    {
        get
        {
            lock (_gate)
            {
                return _steps.ToList();
            }
        }
    }

    /// <summary>The revisions the turn created, oldest first.</summary>
    public IReadOnlyList<long> Revisions
    {
        get
        {
            lock (_gate)
            {
                return _revisions.ToList();
            }
        }
    }

    private sealed record Done(string ForModel, string Kind, string? Path, string Description, long? RevisionId = null, int? Rows = null);

    private Task<string> Overview(CancellationToken cancellationToken = default) =>
        Run("memory_overview", "/", "look at", async () =>
        {
            var core = await _memory.EnsureCoreAsync(_userId, cancellationToken);
            var index = await _memory.BuildIndexAsync(_userId, cancellationToken);
            var skills = PlatformInstructions.SkillList(await _memory.ListSkillsAsync(_userId, includePrivate: false, cancellationToken), _limits.SkillListMax);
            return new Done(
                $"{PlatformInstructions.PackSection(_memory.Pack)}# The person's core (/filum.md)\n\n{core}\n# What the memory holds\n\n{index}\n\n# Enabled skills\n\n{skills}",
                ToolStep.Read, "/", "Looked at the memory");
        });

    private Task<string> History(
        [Description("Path of the file.")] string path,
        CancellationToken cancellationToken = default) =>
        Run("memory_history", path, "read the history of", async () =>
        {
            var outcome = await _memory.GetHistoryAsync(_userId, path, cancellationToken);
            if (outcome.IsRefused)
            {
                return outcome.Refusal!;
            }

            var lines = outcome.Value!.Select(r =>
                $"- change {r.Id}: {r.Summary} · by {(r.Author == MemoryAuthor.Agent ? "the agent" : r.Author == MemoryAuthor.Person ? "the person" : "the platform")}"
                + (r.ConversationTitle is null ? string.Empty : $" in \"{r.ConversationTitle}\"")
                + $" · {r.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC");
            return new Done(string.Join('\n', lines), ToolStep.Read, path, $"Read the history of {path}");
        });

    private Task<string> Undo(
        [Description("The id of the change to undo.")] long revisionId,
        CancellationToken cancellationToken = default) =>
        Run("memory_undo", null, $"undo change {revisionId}", async () =>
        {
            var outcome = await _memory.UndoAsync(_userId, _actor, revisionId, cancellationToken);
            if (outcome.IsRefused)
            {
                return outcome.Refusal!;
            }

            var applied = outcome.Value!;
            lock (_gate)
            {
                _revisions.Add(applied.RevisionId);
            }

            return new Done($"Undid change {revisionId}: {applied.Path} is back to how it was before it (change {applied.RevisionId}).",
                ToolStep.Wrote, applied.Path, $"Undid a change to {applied.Path}", applied.RevisionId, applied.RowCount);
        });

    private Task<string> ListSkills(
        [Description("Also list private skills; only when the person asks for them.")] bool includePrivate = false,
        CancellationToken cancellationToken = default) =>
        Run("skill_list", Skills.Folder, "list the skills", async () =>
        {
            var skills = await _memory.ListSkillsAsync(_userId, includePrivate, cancellationToken);
            var lines = skills
                .OrderBy(s => s.Skill.Name, StringComparer.Ordinal)
                .Select(s => $"- /{s.Skill.Name}: {s.Skill.Description} · when {s.Skill.When} · {(s.Skill.Enabled ? "on" : "off")}{(s.Sensitivity == MemorySensitivity.Private ? " · private" : string.Empty)}");
            return new Done(skills.Count == 0 ? "The person has no skills yet." : string.Join('\n', lines), ToolStep.Read, Skills.Folder, "Listed skills");
        });

    private Task<string> Read(
        [Description("Path of the file, for example /notes/plans.md.")] string path,
        [Description("First line to read, from 1.")] int? fromLine = null,
        [Description("Last line to read.")] int? toLine = null,
        CancellationToken cancellationToken = default) =>
        Run("memory_read", path, "read", async () =>
        {
            var outcome = await _memory.ReadAsync(_userId, path, fromLine, toLine, cancellationToken);
            if (outcome.IsRefused)
            {
                return outcome.Refusal!;
            }

            var read = outcome.Value!;
            var text = new StringBuilder();
            if (read.Sensitivity != MemorySensitivity.Normal)
            {
                text.Append(CultureInfo.InvariantCulture, $"({read.Sensitivity} file)\n");
            }

            for (var i = 0; i < read.Lines.Count; i++)
            {
                text.Append(CultureInfo.InvariantCulture, $"{read.FromLine + i}: {read.Lines[i]}\n");
            }

            var whole = read.FromLine == 1 && read.ToLine == read.TotalLines;
            text.Append(read.TotalLines == 0 ? "(empty file)" : $"(lines {read.FromLine}–{read.ToLine} of {read.TotalLines})");
            return new Done(text.ToString(), ToolStep.Read, path, whole ? $"Read {path}" : $"Read {path} (lines {read.FromLine}–{read.ToLine} of {read.TotalLines})");
        });

    private Task<string> Search(
        [Description("The text to look for.")] string query,
        [Description("Folder prefix to search under; / for everything.")] string prefix = "/",
        [Description("Also search private files; only when the person asks for them in this message.")] bool includePrivate = false,
        CancellationToken cancellationToken = default) =>
        Run("memory_search", prefix, "search", async () =>
        {
            var outcome = await _memory.SearchAsync(_userId, query, prefix, includePrivate, cancellationToken);
            if (outcome.IsRefused)
            {
                return outcome.Refusal!;
            }

            var result = outcome.Value!;
            var text = string.Join('\n', result.Hits.Select(h => $"{h.Path}:{h.Line}: {h.Text}"));
            if (result.Truncated)
            {
                text += "\n(more matches not shown; narrow the query or the prefix)";
            }

            var count = result.Truncated ? $"more than {result.Hits.Count} matches" : Plural(result.Hits.Count, "match", "matches");
            return new Done(result.Hits.Count == 0 ? $"No matches for \"{query}\"." : text, ToolStep.Searched, prefix, $"Searched for \"{query}\" · {count}");
        });

    private const string UseCollectionTools = "is a collection: use collection_add_rows, collection_update_rows or collection_remove_rows; the platform writes the file.";

    private const string UseSkillTools = "is a skill: use skill_save (with replace to change it) or skill_set_enabled; the platform writes the file.";

    private static bool IsCollection(string? path) => path?.EndsWith(".csv", StringComparison.Ordinal) == true;

    /// <summary>Collections and skills are written only through their own tools; the text tools refuse them.</summary>
    private static Task<MemoryOutcome<MemoryChange>>? RefuseTyped(string path) =>
        Skills.IsSkillPath(path) ? Task.FromResult(MemoryOutcome<MemoryChange>.Refused($"{path} {UseSkillTools}"))
        : IsCollection(path) ? Task.FromResult(MemoryOutcome<MemoryChange>.Refused($"{path} {UseCollectionTools}"))
        : null;

    private Task<string> UseSkill(
        [Description("The skill's name, without the '/'.")] string name,
        CancellationToken cancellationToken = default) =>
        Run("skill_use", SkillPath(name), "use the skill", async () =>
        {
            var skill = await _memory.GetSkillAsync(_userId, Bare(name), cancellationToken);
            if (skill.IsRefused)
            {
                return skill.Refusal!;
            }

            var found = skill.Value!;
            if (!found.Skill.Enabled)
            {
                return $"The skill /{found.Skill.Name} is off; the person can turn it back on.";
            }

            if (found.Sensitivity == MemorySensitivity.Private)
            {
                return $"The skill /{found.Skill.Name} is private; it is used only when the person invokes it.";
            }

            return new Done($"Skill /{found.Skill.Name}: {found.Skill.Description}\nFollow these steps now:\n{found.Skill.Steps}", ToolStep.Read, found.Path, $"Used skill /{found.Skill.Name}");
        });

    private Task<string> SaveSkill(
        [Description("Short name: lowercase words joined by '-', like weekly-review.")] string name,
        [Description("One line: what the skill does.")] string description,
        [Description("One line: when it applies, in the person's terms.")] string when,
        [Description("The steps, plain and numbered.")] string steps,
        [Description("True to change an existing skill with this name.")] bool replace = false,
        CancellationToken cancellationToken = default) =>
        Change("skill_save", SkillPath(name), "save the skill", async () =>
            {
                var saved = await _memory.SaveSkillAsync(_userId, _actor, new Skill(Bare(name), description, when, true, steps), replace, cancellationToken);
                if (!saved.IsRefused)
                {
                    lock (_gate)
                    {
                        _savedSkills.Add(Bare(name));
                    }
                }

                return saved;
            },
            c => $"Saved skill /{Bare(name)}",
            c => $"Saved the skill /{Bare(name)}; it is on and listed from the next message. The person can use it by typing /{Bare(name)} or by asking.");

    private Task<string> ProposeSkill(
        [Description("Short name: lowercase words joined by '-', like weekly-review.")] string name,
        [Description("One line: what the skill does.")] string description,
        [Description("One line: when it applies, in the person's terms.")] string when,
        [Description("The steps, plain and numbered.")] string steps,
        CancellationToken cancellationToken = default) =>
        Run("skill_propose", SkillPath(name), "propose the skill", async () =>
        {
            var skill = new Skill(Bare(name), description, when, true, steps);
            if (Skills.Validate(skill) is { } error)
            {
                return error;
            }

            if (!(await _memory.GetSkillAsync(_userId, skill.Name, cancellationToken)).IsRefused)
            {
                return $"There is already a skill named '{skill.Name}'; pick another name, or change that one with skill_save and replace.";
            }

            lock (_gate)
            {
                _proposal = new SkillProposal(skill.Name, skill.Description.Trim(), skill.When.Trim(), skill.Steps.Trim());
            }

            return new Done($"Proposed /{skill.Name}; nothing is saved. Ask the person whether to save it; if they agree, save it with skill_save.", ToolStep.Asked, Skills.PathOf(skill.Name), $"Proposed skill /{skill.Name}");
        });

    private Task<string> SetSkillEnabled(
        [Description("The skill's name, without the '/'.")] string name,
        [Description("False to turn it off, true to turn it back on.")] bool enabled,
        CancellationToken cancellationToken = default) =>
        Change("skill_set_enabled", SkillPath(name), enabled ? "turn on the skill" : "turn off the skill",
            () => _memory.SetSkillEnabledAsync(_userId, _actor, Bare(name), enabled, cancellationToken),
            _ => $"Turned {(enabled ? "on" : "off")} /{Bare(name)}",
            _ => $"The skill /{Bare(name)} is now {(enabled ? "on" : "off")}.");

    /// <summary>A name as the model may write it: '/log-note' and 'log-note' are the same skill.</summary>
    private static string Bare(string? name) => (name ?? string.Empty).Trim().TrimStart('/');

    private static string SkillPath(string? name) => Skills.PathOf(Bare(name));

    private Task<string> AddRows(
        [Description("Path of the collection, ending in .csv.")] string path,
        [Description("The rows to add, each an object of field → value.")] List<Dictionary<string, JsonElement>> rows,
        CancellationToken cancellationToken = default) =>
        Change("collection_add_rows", path, "add rows to", () => _memory.AddRowsAsync(_userId, _actor, path, rows.Select(Values).ToList(), cancellationToken),
            c => c.Created ? $"Created {path} with {Plural(c.RowCount ?? 0, "row")}" : $"Added {Plural(Math.Max(c.Added, 0), "row")} to {path}",
            c => $"{(c.Created ? "Created" : "Added to")} {path}: {Plural(c.RowCount ?? 0, "row")} now.");

    private Task<string> UpdateRows(
        [Description("Path of the collection, ending in .csv.")] string path,
        [Description("Which rows: field → value, all must match (case is ignored).")] Dictionary<string, JsonElement> where,
        [Description("The new values: field → value.")] Dictionary<string, JsonElement> set,
        CancellationToken cancellationToken = default) =>
        Change("collection_update_rows", path, "change rows of", () => _memory.UpdateRowsAsync(_userId, _actor, path, Values(where), Values(set), cancellationToken),
            _ => $"Changed rows of {path}",
            c => $"Changed rows of {path}: {Plural(c.RowCount ?? 0, "row")}.");

    private Task<string> RemoveRows(
        [Description("Path of the collection, ending in .csv.")] string path,
        [Description("Which rows to remove: field → value, all must match (case is ignored).")] Dictionary<string, JsonElement> where,
        CancellationToken cancellationToken = default) =>
        Change("collection_remove_rows", path, "remove rows from", () => _memory.RemoveRowsAsync(_userId, _actor, path, Values(where), cancellationToken),
            c => $"Removed {Plural(Math.Max(-c.Added, 0), "row")} from {path}",
            c => $"Removed {Plural(Math.Max(-c.Added, 0), "row")} from {path}: {Plural(c.RowCount ?? 0, "row")} left.");

    private Task<string> AddField(
        [Description("Path of the collection, ending in .csv.")] string path,
        [Description("The name of the new field.")] string field,
        CancellationToken cancellationToken = default) =>
        Change("collection_add_field", path, "add a field to", () => _memory.AddFieldAsync(_userId, _actor, path, field, cancellationToken),
            _ => $"Added the field '{field}' to {path}",
            _ => $"Added the field '{field}' to {path}.");

    /// <summary>A model's JSON values as text: strings as they are, numbers and booleans as written, null as empty.</summary>
    private static IReadOnlyDictionary<string, string> Values(Dictionary<string, JsonElement> values) =>
        values.ToDictionary(v => v.Key, v => v.Value.ValueKind switch
        {
            JsonValueKind.String => v.Value.GetString() ?? string.Empty,
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => v.Value.GetRawText()
        });

    private Task<string> Write(
        [Description("Path of the document, ending in .md.")] string path,
        [Description("The whole content of the file.")] string content,
        CancellationToken cancellationToken = default) =>
        Change("memory_write", path, "write", () => RefuseTyped(path) ?? _memory.WriteAsync(_userId, _actor, path, content, cancellationToken),
            c => c.Created ? $"Created {path}" : $"Rewrote {path}",
            c => $"{(c.Created ? "Created" : "Replaced")} {path} ({Plural(c.LineCount, "line")}).");

    private Task<string> Edit(
        [Description("Path of the file.")] string path,
        [Description("The exact text to replace; it must appear once.")] string oldText,
        [Description("The text to put in its place.")] string newText,
        CancellationToken cancellationToken = default) =>
        Change("memory_edit", path, "edit", () => RefuseTyped(path) ?? _memory.EditAsync(_userId, _actor, path, oldText, newText, cancellationToken),
            _ => $"Edited {path}",
            _ => $"Edited {path}.");

    private Task<string> Append(
        [Description("Path of the file.")] string path,
        [Description("The lines to add at the end.")] string text,
        CancellationToken cancellationToken = default) =>
        Change("memory_append", path, "add to", () => RefuseTyped(path) ?? _memory.AppendAsync(_userId, _actor, path, text, cancellationToken),
            c => AddedTo(c, path),
            c => AddedTo(c, path) + ".");

    private Task<string> Delete(
        [Description("Path of the file.")] string path,
        CancellationToken cancellationToken = default) =>
        Change("memory_delete", path, "delete", () => _memory.DeleteAsync(_userId, _actor, path, cancellationToken),
            _ => $"Deleted {path}",
            _ => $"Deleted {path}; it stays in the history.");

    private Task<string> Move(
        [Description("Current path of the file.")] string from,
        [Description("New path of the file.")] string to,
        CancellationToken cancellationToken = default) =>
        Change("memory_move", from, "move", () => _memory.MoveAsync(_userId, _actor, from, to, cancellationToken),
            _ => $"Moved {from} to {to}",
            _ => $"Moved {from} to {to}.",
            stepPath: to);

    private Task<string> SetSensitivity(
        [Description("Path of the file.")] string path,
        [Description("normal, sensitive or private.")] string level,
        CancellationToken cancellationToken = default) =>
        Change("memory_set_sensitivity", path, "change the sensitivity of", () => _memory.SetSensitivityAsync(_userId, _actor, path, level, cancellationToken),
            _ => $"Marked {path} {level}",
            _ => $"{path} is now {level}.");

    private Task<string> Aggregate(
        [Description("Path of the collection (.csv).")] string path,
        [Description("count, sum, average, min or max.")] string operation,
        [Description("The field to compute on; optional for count.")] string? column = null,
        [Description("A field to group by; with period, a date field.")] string? groupBy = null,
        [Description("day, week, month or year: groups by that period of the groupBy date field.")] string? period = null,
        [Description("A field to filter on.")] string? filterColumn = null,
        [Description("The value filterColumn must have (ignoring case).")] string? filterValue = null,
        [Description("A date field to filter by range.")] string? dateColumn = null,
        [Description("First day included, yyyy-MM-dd.")] string? from = null,
        [Description("Last day included, yyyy-MM-dd.")] string? to = null,
        CancellationToken cancellationToken = default) =>
        Run("collection_aggregate", path, "compute on", async () =>
        {
            var table = await _memory.ReadCollectionAsync(_userId, path, cancellationToken);
            if (table.IsRefused)
            {
                return table.Refusal!;
            }

            if (!table.Value!.IsValid)
            {
                return $"{path} is not a valid collection: {table.Value.Error}";
            }

            var outcome = CollectionAggregator.Compute(table.Value, new AggregateRequest(operation, column, groupBy, period, filterColumn, filterValue, dateColumn, from, to));
            if (outcome.IsRefused)
            {
                return outcome.Refusal!;
            }

            var result = outcome.Value!;
            var lines = result.Groups.Count == 0
                ? "No rows match."
                : string.Join('\n', result.Groups.Select(g => $"{(g.Key is null ? result.Operation : g.Key)}: {g.Value.ToString(CultureInfo.InvariantCulture)} ({Plural(g.Rows, "row")})"));
            var skipped = result.RowsSkipped > 0 ? $", {result.RowsSkipped} skipped" : string.Empty;
            var skippedWhy = result.RowsSkipped > 0 ? $", {result.RowsSkipped} skipped because a value could not be read" : string.Empty;
            var of = result.Column is null ? "rows" : result.Column;
            var by = groupBy is null ? string.Empty : $" by {period ?? groupBy}";
            return new Done(
                $"{lines}\n(computed, not estimated: {Plural(result.RowsUsed, "row")} used{skippedWhy})",
                ToolStep.Computed, path, $"Computed the {result.Operation} of {of}{by} on {path} · {Plural(result.RowsUsed, "row")}{skipped}");
        });

    private Task<string> Change(string tool, string path, string verb, Func<Task<MemoryOutcome<MemoryChange>>> change, Func<MemoryChange, string> describe, Func<MemoryChange, string> forModel, string? stepPath = null) =>
        Run(tool, path, verb, async () =>
        {
            var outcome = await change();
            if (outcome.IsRefused)
            {
                return outcome.Refusal!;
            }

            var applied = outcome.Value!;
            lock (_gate)
            {
                _revisions.Add(applied.RevisionId);
            }

            return new Done(forModel(applied), ToolStep.Wrote, stepPath ?? path, describe(applied), applied.RevisionId, applied.RowCount);
        });

    /// <summary>
    /// Runs one call: checks the turn's limit, times it and records its step. The action returns either what was
    /// done, or the reason it was refused (a string), which goes back to the model and becomes a failed step.
    /// </summary>
    private async Task<string> Run(string tool, string? path, string verb, Func<Task<object>> action)
    {
        int call;
        lock (_gate)
        {
            call = ++_calls;
        }

        if (call > _limits.MaxToolCallsPerTurn)
        {
            var stop = $"the limit of {_limits.MaxToolCallsPerTurn} memory actions for this message is reached; answer with what you have.";
            if (call == _limits.MaxToolCallsPerTurn + 1)
            {
                Record(new ToolStep(ToolStep.Failed, tool, path, $"Stopped: the limit of {_limits.MaxToolCallsPerTurn} memory actions was reached", 0, null, stop));
            }

            return $"Refused: {stop}";
        }

        var stopwatch = Stopwatch.StartNew();
        var result = await action();
        var elapsed = (int)stopwatch.ElapsedMilliseconds;
        if (result is Done done)
        {
            Record(new ToolStep(done.Kind, tool, done.Path, done.Description, elapsed, done.RevisionId, Rows: done.Rows));
            return done.ForModel;
        }

        var reason = (string)result;
        Record(new ToolStep(ToolStep.Failed, tool, path, $"Could not {verb} {path}".TrimEnd(), elapsed, null, reason));
        return $"Refused: {reason}";
    }

    /// <summary>Adds a step to the turn, in call order; a host records its own tools' calls here (spec 017).</summary>
    public void Record(ToolStep step)
    {
        lock (_gate)
        {
            _steps.Add(step);
        }
    }

    private static string AddedTo(MemoryChange change, string path)
    {
        var unit = change.RowCount is null ? "line" : "row";
        return change.Created
            ? $"Created {path} with {Plural(Math.Max(change.Added, 0), unit)}"
            : $"Added {Plural(Math.Max(change.Added, 0), unit)} to {path}";
    }

    private static string Plural(int count, string one, string? many = null) =>
        count == 1 ? $"1 {one}" : $"{count.ToString(CultureInfo.InvariantCulture)} {many ?? one + "s"}";
}
