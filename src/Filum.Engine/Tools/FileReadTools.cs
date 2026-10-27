using System.ComponentModel;
using System.Globalization;
using System.Text;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Tools;
using Filum.Engine.Events;
using Filum.Engine.Instructions;
using Filum.Engine.Memory.Contracts;
using Microsoft.Extensions.AI;
using static Filum.Engine.Tools.ToolText;

namespace Filum.Engine.Tools;

internal sealed class FileReadTools(MemoryToolSession session) : MemoryToolGroup(session)
{
    private IMemoryCore Core => Session.Memory;

    private IMemoryReader Reader => Session.Memory;

    private IMemorySkills Skills => Session.Memory;

    private IMemoryHistory History => Session.Memory;

    private IMemoryEvents EventLog => Session.Memory;

    private IMemoryFacts Facts => Session.Memory;

    public override IEnumerable<AIFunction> Functions() =>
    [
        AIFunctionFactory.Create(Overview, ToolNames.MemoryOverview,
            "The person's core, the map of every file and their enabled skills, in one call."),
        AIFunctionFactory.Create(Read, ToolNames.MemoryRead,
            $"Read a file with line numbers, up to {Session.Limits.MaxReadLines} lines; fromLine and toLine page through it."),
        AIFunctionFactory.Create(Search, ToolNames.MemorySearch,
            $"Find the lines that contain a text across the files or under a folder, up to {Session.Limits.MaxSearchResults}, and what was said that matches, newest first. Private content only with includePrivate."),
    ];

    private Task<string> Overview(CancellationToken cancellationToken = default) =>
        Run(ToolNames.MemoryOverview, "/", "look at", async () =>
        {
            // The same brief a host's start hook gives (spec 046): core, rules, open items, facts, learned, map, what was said.
            await Core.EnsureCoreAsync(UserId, cancellationToken);
            await Core.EnsureLearnedAsync(UserId, cancellationToken);
            var brief = await Session.Memory.BriefAsync(UserId, null, cancellationToken);
            var skills = PlatformInstructions.SkillList(await Skills.ListSkillsAsync(UserId, includePrivate: false, cancellationToken), Session.Limits.SkillListMax);
            var waiting = Session.IsConsolidation ? string.Empty : await WaitingAsync(cancellationToken);
            return new ToolDone(
                $"{PlatformInstructions.PackSection(Core.Pack)}{brief}{waiting}\n\n# Enabled skills\n\n{skills}",
                ToolStep.Read, "/", "Looked at the memory");
        });


    // Without a host's hooks nothing tidies on its own (spec 046): when much is waiting, the newest of it is shown here,
    // bounded, so a model that only reads the overview still records a rule, an open item or who the person is.
    private async Task<string> WaitingAsync(CancellationToken cancellationToken)
    {
        var pending = await Session.Memory.PendingAsync(UserId, OverviewBacklog * 10, cancellationToken);
        if (pending.Count <= OverviewBacklog)
        {
            return string.Empty;
        }

        var shown = new List<string>();
        var chars = 0;
        foreach (var e in pending.Where(e => e.Sensitivity == MemorySensitivity.Normal).OrderByDescending(e => e.Id))
        {
            var line = $"- {e.OccurredAt.UtcDateTime:yyyy-MM-dd} #{e.Id}: {EventSearch.Cut(e.Text, OverviewWaitingLine)}";
            if (chars + line.Length > OverviewWaitingChars)
            {
                break;
            }

            shown.Add(line);
            chars += line.Length;
        }

        return string.Create(CultureInfo.InvariantCulture,
            $"\n\n# Said and not tidied yet ({pending.Count}, newest first)\n\nIf these hold who the person is, a rule or an open item the memory lacks, record it now.\n\n{string.Join('\n', shown)}");
    }

    private const int OverviewBacklog = 10;
    private const int OverviewWaitingChars = 1_500;
    private const int OverviewWaitingLine = 300;

    private Task<string> Read(
        [Description("Path, like /notes/plans.md.")] string path,
        [Description("First line to read, from 1.")] int? fromLine = null,
        [Description("Last line to read.")] int? toLine = null,
        CancellationToken cancellationToken = default) =>
        Run(ToolNames.MemoryRead, path, "read", async () =>
        {
            var outcome = await Reader.ReadAsync(UserId, path, fromLine, toLine, cancellationToken);
            if (outcome.Refuses(out var refusal, out var read))
            {
                return refusal;
            }

            if (Session.IsConsolidation && read.Sensitivity == MemorySensitivity.Private)
            {
                return $"{path} is private: a pass does not read it.";
            }

            var text = new StringBuilder();
            if (read.Sensitivity != MemorySensitivity.Normal)
            {
                text.Append(CultureInfo.InvariantCulture, $"({read.Sensitivity} file)\n");
            }

            // A line that replaced another shows what it said before, so the old value stays in sight.
            var changes = await History.LineChangesAsync(UserId, path, cancellationToken);
            var notes = 0;
            for (var i = 0; i < read.Lines.Count; i++)
            {
                var line = read.Lines[i];
                var note = notes < MemoryTools.MaxChangeNotes && changes.TryGetValue(line, out var change)
                    ? $"   (changed {change.At.UtcDateTime:yyyy-MM-dd}; before: \"{change.Before}\")"
                    : string.Empty;
                notes += note.Length > 0 ? 1 : 0;
                text.Append(CultureInfo.InvariantCulture, $"{read.FromLine + i}: {line}{note}\n");
            }

            var whole = read.FromLine == 1 && read.ToLine == read.TotalLines;
            text.Append(read.TotalLines == 0 ? "(empty file)" : $"(lines {read.FromLine}–{read.ToLine} of {read.TotalLines})");
            return new ToolDone(text.ToString(), ToolStep.Read, path, whole ? $"Read {path}" : $"Read {path} (lines {read.FromLine}–{read.ToLine} of {read.TotalLines})");
        });

    private Task<string> Search(
        [Description("The text to look for.")] string query,
        [Description("Folder to search under; / for all.")] string prefix = "/",
        [Description("Include private files.")] bool includePrivate = false,
        CancellationToken cancellationToken = default) =>
        Run(ToolNames.MemorySearch, prefix, "search", async () =>
        {
            var outcome = await Reader.SearchAsync(UserId, query, prefix, includePrivate && !Session.IsConsolidation, cancellationToken);
            if (outcome.Refuses(out var refusal, out var result))
            {
                return refusal;
            }

            var text = string.Join('\n', result.Hits.Select(h => $"{h.Path}:{h.Line}: {h.Text}"));
            if (result.Truncated)
            {
                text += "\n(more matches not shown; narrow the query or the prefix)";
            }

            var count = result.Truncated ? $"more than {result.Hits.Count} matches" : Plural(result.Hits.Count, "match", "matches");
            var found = result.Hits.Count == 0 ? $"No matches for \"{query}\"." : text;

            // What was said is shown beside the files, newest first, so a later word stays in sight of an older file.
            if (!Session.IsConsolidation && (await EventLog.SearchEventsAsync(UserId, query, null, null, includePrivate, cancellationToken)).Value is { Count: > 0 } said)
            {
                found = (result.Hits.Count == 0 ? $"No file matches \"{query}\"." : text) + "\n\nWhat was said that matches, newest first:\n" + string.Join('\n', said
                    .Select(h => h.Event)
                    .OrderByDescending(e => e.OccurredAt)
                    .Take(MemoryTools.SearchSaidMax)
                    .Select(e => $"- {e.OccurredAt.UtcDateTime:yyyy-MM-dd} · {Who(e)}: {EventSearch.Excerpt(e.Text, query)}"));
            }

            if (await FactsAboutAsync(query, cancellationToken) is { Length: > 0 } facts)
            {
                found = $"{facts}\n\n{found}";
            }

            return new ToolDone(found, ToolStep.Searched, prefix, $"Searched for \"{query}\" · {count}");
        });

    private async Task<string> FactsAboutAsync(string query, CancellationToken cancellationToken) =>
        FactLinks.About((await Facts.CurrentFactsAsync(UserId, null, cancellationToken)).Value ?? [], query);
}
