using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Facts;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Consolidation;
using Filum.Engine.Events;
using Filum.Engine.Facts;
using Filum.Engine.Memory;
using Filum.Engine.Memory.Core;
using Filum.Engine.Memory.Files;
using Filum.Engine.Tools;
using Microsoft.Extensions.Options;

namespace Filum.Engine.Briefing;

// What an agent knows about the person before the first message, and what the memory holds about each message (spec
// 045). Deterministic, read only, never private.
internal sealed partial class BriefService(
    IMemoryFileStore files, MemoryIndexBuilder index, FactService facts, FactRecall recall, EventService events, ConsolidationService consolidation, IOptions<MemoryOptions> options)
{
    // The share of what is left that each section may take, in order: rules, core, open items, facts, learned, map.
    private static readonly double[] Weights = [0.12, 0.30, 0.12, 0.22, 0.10, 0.14];

    // The memory is the source about the person: it wins over what else the context says about them (spec 045).
    private const string Source = "What the person's memory (Filum) holds about them. It is what they told you: it wins over anything else your context says about them.\n\n";

    // The share of a recall the rules may take, before what matched the message, and their reminder at its end.
    private const double RecallRulesShare = 0.4;
    private const double RecallReminderShare = 0.2;

    // Pending messages are counted up to this.
    private const int PendingCount = 1_000;

    private const int RecallHits = 3;
    private const int RecallExcerpt = 200;
    private const int MinWordLength = 3;

    private static readonly string[] Spoken = [MemoryEventKind.Said, MemoryEventKind.Answered, MemoryEventKind.Imported];

    public async Task<string> BriefAsync(Guid userId, int? maxChars, CancellationToken cancellationToken)
    {
        var budget = maxChars ?? options.Value.BriefChars;
        var said = await events.EventsAsync(userId, new EventQuery(Kinds: Spoken, IncludePrivate: false), cancellationToken);
        var saidLine = said.Count == 0 ? string.Empty : string.Create(CultureInfo.InvariantCulture,
            $"# What was said\n\n{said.Count} messages kept, from {said.Min(e => e.OccurredAt):yyyy-MM-dd} to {said.Max(e => e.OccurredAt):yyyy-MM-dd}. Search them with events_search before saying you do not know.")
            + await PendingLineAsync(userId, cancellationToken);
        var current = await CurrentAsync(userId, cancellationToken);
        var sections = new[]
        {
            Rules(current),
            Titled("The person's core (/filum.md): who they are", await OpenTextAsync(userId, MemoryPaths.CorePath, cancellationToken)),
            Titled("Open items: promised or still to do", Lines(current, FactTable.Open, f => $"- {f.Value}{(f.Note.Length > 0 ? $" (due {f.Note})" : string.Empty)}, since {f.ValidFrom}")),
            Titled("Facts that hold now (facts_current and facts_history give the rest)",
                string.Join('\n', current.Where(f => !FactTable.IsManyValued(f.Attribute)).Select(f => $"- {f.Subject} · {f.Attribute}: {f.Value} (since {f.ValidFrom})"))
                + await PlannedAsync(userId, cancellationToken)),
            Titled("What was learned about them (/learned.md), newest first", string.Join('\n', (await OpenTextAsync(userId, LearnedFile.Path, cancellationToken)).Split('\n').Reverse())),
            Titled("What the memory holds", await index.BuildAsync(userId, cancellationToken)),
        };

        var text = new StringBuilder(Source);
        var left = budget - text.Length - saidLine.Length;
        for (var i = 0; i < sections.Length; i++)
        {
            var share = (int)(left * Weights[i] / Weights[i..].Sum());
            var part = Fit(sections[i], share);
            text.Append(part);
            left -= part.Length;
        }

        return text.Append(saidLine).ToString().TrimEnd();
    }

    public async Task<string> RecallAsync(Guid userId, string? message, int? maxChars, CancellationToken cancellationToken)
    {
        var query = Query(message);
        if (query.Length == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>();
        var rules = string.Empty;
        IReadOnlyList<Fact> current = [];
        if (!await IsPrivateAsync(userId, FactTable.Path, cancellationToken))
        {
            var planned = (await facts.PlannedAsync(userId, null, cancellationToken)).Value ?? [];
            lines.AddRange(((await recall.MatchAsync(userId, query, cancellationToken)).Value ?? [])
                .Where(f => !FactTable.Same(f.Attribute, FactTable.Rule)).Select(f => planned.Any(p => (p.Subject, p.Attribute, p.Value, p.ValidFrom) == (f.Subject, f.Attribute, f.Value, f.ValidFrom)) ? FactTable.PlannedLine(f) : FactTable.Line(f)));
            current = await CurrentAsync(userId, cancellationToken);
            rules = Rules(current);
        }

        var hits = (await events.SearchAsync(userId, query, null, null, includePrivate: false, cancellationToken)).Value ?? [];
        lines.AddRange(hits.Take(RecallHits).Select(h =>
            $"- {h.Event.OccurredAt:yyyy-MM-dd} #{h.Event.Id} {ToolText.Who(h.Event)}: {EventSearch.Cut(EventSearch.Excerpt(h.Event.Text, query, RecallExcerpt / 2), RecallExcerpt)}"));
        var budget = maxChars ?? options.Value.RecallChars;
        var kept = Fit(rules, (int)(budget * RecallRulesShare));
        var reminder = kept.Length == 0 ? string.Empty : EventSearch.Cut(Reminder(current), (int)(budget * RecallReminderShare));
        var matched = lines.Count == 0 ? string.Empty
            : Fit($"What the person's memory (Filum) holds about this message; search it for more:\n{string.Join('\n', lines)}", budget - kept.Length - reminder.Length);
        return (kept + matched).TrimEnd() + reminder;
    }

    public async Task<string> RulesAsync(Guid userId, int? maxChars, CancellationToken cancellationToken)
    {
        var rules = Rules(await CurrentAsync(userId, cancellationToken));
        return maxChars is { } max ? Fit(rules, max).TrimEnd() : rules.TrimEnd();
    }

    // The rules again in one line at the very end, right before the model answers (spec 055).
    private static string Reminder(IReadOnlyList<Fact> current) =>
        $"\n\nBefore you answer, follow: {string.Join("; ", current.Where(f => FactTable.Same(f.Attribute, FactTable.Rule)).Select(f => f.Value.Trim().TrimEnd('.')))}.";

    // The rules the person set, first in a brief and in every recall: they hold for every answer (spec 045).
    private static string Rules(IReadOnlyList<Fact> current) =>
        Titled("Rules the person set: follow every one in this answer", Lines(current, FactTable.Rule, f => $"- {f.Value}{(f.Note.Length > 0 ? $" (because {f.Note})" : string.Empty)}"));

    // A long message is searched by its words, first said first, within a query's length.
    private static string Query(string? message)
    {
        var query = new StringBuilder();
        foreach (var word in WordPattern().Matches(message ?? string.Empty).Select(m => m.Value).Where(w => w.Length >= MinWordLength).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (query.Length + word.Length + 1 > FileRefusals.MaxQueryLength)
            {
                break;
            }

            query.Append(query.Length == 0 ? string.Empty : " ").Append(word);
        }

        return query.ToString();
    }

    // The facts that hold now, the most recently recorded first; none when the facts file is private.
    private async Task<IReadOnlyList<Fact>> CurrentAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await IsPrivateAsync(userId, FactTable.Path, cancellationToken))
        {
            return [];
        }

        return [.. ((await facts.CurrentAsync(userId, null, null, cancellationToken)).Value ?? [])
            .Select((f, i) => (Fact: f, Row: i))
            .OrderByDescending(x => x.Fact.RecordedAt.Length > 0 ? x.Fact.RecordedAt : x.Fact.ValidFrom, StringComparer.Ordinal)
            .ThenByDescending(x => x.Row)
            .Select(x => x.Fact)];
    }

    // The values that start later, under the facts that hold now: they do not hold yet (spec 055).
    private async Task<string> PlannedAsync(Guid userId, CancellationToken cancellationToken) =>
        await IsPrivateAsync(userId, FactTable.Path, cancellationToken) || (await facts.PlannedAsync(userId, null, cancellationToken)).Value is not { Count: > 0 } planned
            ? string.Empty
            : $"\n\n{FactTable.PlannedTitle}\n{string.Join('\n', planned.Select(FactTable.PlannedLine))}";

    private static string Lines(IReadOnlyList<Fact> current, string attribute, Func<Fact, string> line) =>
        string.Join('\n', current.Where(f => FactTable.Same(f.Attribute, attribute)).Select(line));

    // What was said and not yet tidied: consolidation catches what the turns missed (spec 046).
    private async Task<string> PendingLineAsync(Guid userId, CancellationToken cancellationToken) =>
        (await consolidation.PendingAsync(userId, PendingCount, cancellationToken)).Count is var pending and > 0
            ? string.Create(CultureInfo.InvariantCulture, $" {pending} of them are not tidied yet: after answering the person's first request, call memory_consolidate.")
            : string.Empty;

    private async Task<string> OpenTextAsync(Guid userId, string path, CancellationToken cancellationToken) =>
        await files.FindLiveAsync(userId, path, cancellationToken) is { } file && file.Sensitivity == MemorySensitivity.Normal ? file.Content.Trim() : string.Empty;

    private async Task<bool> IsPrivateAsync(Guid userId, string path, CancellationToken cancellationToken) =>
        await files.FindLiveAsync(userId, path, cancellationToken) is { } file && file.Sensitivity != MemorySensitivity.Normal;

    private static string Titled(string title, string body) => body.Length == 0 ? string.Empty : $"# {title}\n\n{body}\n\n";

    // Whole lines up to the share, and how many lines did not fit.
    private static string Fit(string text, int share)
    {
        if (text.Length <= share)
        {
            return text;
        }

        var kept = new StringBuilder();
        var lines = text.Split('\n');
        var shown = 0;
        foreach (var line in lines)
        {
            if (kept.Length + line.Length + 1 > share - MoreLineChars)
            {
                break;
            }

            kept.Append(line).Append('\n');
            shown++;
        }

        return shown == 0 ? string.Empty : kept.Append(CultureInfo.InvariantCulture, $"(+{lines.Length - shown} more lines)\n\n").ToString();
    }

    // Room kept for the "+N more lines" mark.
    private const int MoreLineChars = 30;

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordPattern();
}
