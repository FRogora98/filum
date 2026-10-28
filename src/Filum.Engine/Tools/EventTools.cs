using System.ComponentModel;
using System.Globalization;
using System.Text;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Tools;
using Filum.Engine.Events;
using Filum.Engine.Memory.Contracts;
using Microsoft.Extensions.AI;
using static Filum.Engine.Tools.ToolText;

namespace Filum.Engine.Tools;

internal sealed class EventTools(MemoryToolSession session) : MemoryToolGroup(session)
{
    private IMemoryEvents EventLog => Session.Memory;

    private IMemoryFacts Facts => Session.Memory;

    public override IEnumerable<AIFunction> Functions() =>
    [
        AIFunctionFactory.Create(SearchEvents, ToolNames.EventsSearch,
            "Search what was said, answered or imported: each match with its date and the reply after it."),
        AIFunctionFactory.Create(ReadEvents, ToolNames.EventsRead,
            "Read the whole conversation of an event number from events_search, in order."),
    ];

    // A conversation read whole up to this; a message is never cut, so one longer than this comes alone.
    private const int MaxReadChars = 20_000;

    // A search shows excerpts: the answer may sit elsewhere in the same conversation, so it can be read whole.
    private Task<string> ReadEvents(
        long @event,
        CancellationToken cancellationToken = default) =>
        Run(ToolNames.EventsRead, null, "read what was said", async () =>
        {
            // Private messages are reached only through events_search with includePrivate, never here.
            const bool withPrivate = false;
            var found = await Conversations.EventAsync(EventLog, UserId, @event, withPrivate, cancellationToken);
            if (found is null)
            {
                return $"There is no event #{@event}.";
            }

            var all = await Conversations.OfAsync(EventLog, UserId, found, withPrivate, cancellationToken);
            var start = Start(all, all.FindIndex(e => e.Id == @event));
            var text = new StringBuilder();
            if (found.ConversationId is { } id && (await EventLog.ConversationTitlesAsync(UserId, [id], cancellationToken)).TryGetValue(id, out var title))
            {
                text.Append(CultureInfo.InvariantCulture, $"Conversation \"{title}\"\n\n");
            }

            var shown = Page(all, start, text);
            return new ToolDone(text.ToString().TrimEnd(), ToolStep.Read, null,
                $"Read {Plural(shown, "message", "messages")} of a conversation");
        });

    // A conversation within the budget is read from its start; a longer one from up to half the budget before the event.
    private static int Start(List<MemoryEvent> all, int at)
    {
        if (all.Sum(e => e.Text.Length) <= MaxReadChars)
        {
            return 0;
        }

        var start = at;
        var chars = all[at].Text.Length;
        while (start > 0 && chars + all[start - 1].Text.Length <= MaxReadChars / 2)
        {
            start--;
            chars += all[start].Text.Length;
        }

        return start;
    }

    // Whole messages from start, up to the budget, then which events to read for the rest; how many were shown.
    private static int Page(List<MemoryEvent> all, int start, StringBuilder text)
    {
        if (start > 0)
        {
            text.Append(CultureInfo.InvariantCulture, $"({Plural(start, "earlier message", "earlier messages")}: read them with event {all[start - 1].Id})\n\n");
        }

        var shown = 0;
        var chars = 0;
        foreach (var e in all.Skip(start))
        {
            if (shown > 0 && chars + e.Text.Length > MaxReadChars)
            {
                break;
            }

            text.Append(CultureInfo.InvariantCulture, $"#{e.Id} · {e.OccurredAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC · {Who(e)}:\n{e.Text}\n\n");
            chars += e.Text.Length;
            shown++;
        }

        var left = all.Count - start - shown;
        if (left > 0)
        {
            text.Append(CultureInfo.InvariantCulture, $"({Plural(left, "more message", "more messages")}: read them with event {all[start + shown].Id})");
        }

        return shown;
    }

    private Task<string> SearchEvents(
        [Description("Words to look for.")] string query,
        [Description("From the day it was said, yyyy-MM-dd.")] string? from = null,
        [Description("To the day it was said, yyyy-MM-dd.")] string? to = null,
        [Description("Include private messages.")] bool includePrivate = false,
        [Description("List every mention, oldest first, to count across conversations; alternatives as \"sofa | desk\".")] bool every = false,
        CancellationToken cancellationToken = default) =>
        Run(ToolNames.EventsSearch, null, "search what was said", async () =>
        {
            var withPrivate = includePrivate && !Session.IsConsolidation;
            if (every)
            {
                return await ListAsync(query, from, to, withPrivate, cancellationToken);
            }

            var outcome = await EventLog.SearchEventsAsync(UserId, query, Day(from), Day(to)?.AddDays(1), withPrivate, cancellationToken);
            if (outcome.Refuses(out var refusal, out var hits))
            {
                return refusal;
            }

            // A model that searches only what was said still meets the facts that may answer it, and where they were said.
            var current = (await Facts.CurrentFactsAsync(UserId, null, cancellationToken)).Value ?? [];
            var named = FactLinks.Named(current, query);
            var matching = ((await Facts.MatchFactsAsync(UserId, query, cancellationToken)).Value ?? []).Where(f => !named.Any(n => FactLinks.Same(n, f))).ToList();
            var sessions = await SessionsAsync(hits, [.. matching, .. named], withPrivate, cancellationToken);
            var found = sessions.Count > 0
                ? await new RecallPage(EventLog, UserId, withPrivate, query, Session.Limits.RecallBudgetChars).ShowAsync(sessions, cancellationToken)
                : await NothingAsync(query, from, to, includePrivate, cancellationToken);
            var facts = string.Join("\n\n", new[] { FactLinks.About(current, query), FactLinks.Matching(matching) }.Where(s => s.Length > 0));
            if (facts.Length > 0)
            {
                found = $"{facts}\n\n{found}";
            }

            var shown = sessions.Select(s => Conversations.KeyOf(s.Anchor)).ToList();
            found += Others(hits, shown, query);
            if (hits.Count >= Session.Limits.EventsSearchMax)
            {
                found += "\n\nMore messages may match: to count or add them up, list them with every.";
            }

            var ordered = hits.Where(h => shown.Contains(Conversations.KeyOf(h.Event))).OrderBy(h => shown.IndexOf(Conversations.KeyOf(h.Event))).ToList();
            return new ToolDone(found, ToolStep.Searched, null,
                $"Searched what was said for \"{query}\" · {Plural(hits.Count, "match", "matches")}", Structured: ToolData.Matches(ordered));
        });

    // Every mention, one line each within the listing's caps, oldest first (spec 057).
    private async Task<object> ListAsync(string query, string? from, string? to, bool withPrivate, CancellationToken cancellationToken)
    {
        var outcome = await EventLog.ListEventsAsync(UserId, query, Day(from), Day(to)?.AddDays(1), withPrivate, Session.Limits.EventsListMax + 1, cancellationToken);
        if (outcome.Refuses(out var refusal, out var listed))
        {
            return refusal;
        }

        var text = new StringBuilder().Append(CultureInfo.InvariantCulture, $"Every message said that mentions \"{query}\", oldest first:\n");
        var shown = 0;
        foreach (var e in listed.Take(Session.Limits.EventsListMax))
        {
            var line = $"- {e.OccurredAt.UtcDateTime.ToString(EngineConstants.Dates.Day, CultureInfo.InvariantCulture)} #{e.Id} {Who(e)}: {EventSearch.Around(e.Text, query, ListExcerpt)}\n";
            if (text.Length + line.Length > Session.Limits.EventsListChars)
            {
                break;
            }

            text.Append(line);
            shown++;
        }

        text.Append(shown == 0 ? "Nothing that was said matches."
            : shown < listed.Count ? $"{Plural(shown, "message", "messages")} listed; there may be more: narrow the words or the days."
            : $"{Plural(shown, "message", "messages")} listed: all that match.");
        text.Append(shown == 0 ? string.Empty : " Count each distinct item once, the ones you are unsure of too, and name them.");
        return new ToolDone(text.ToString(), ToolStep.Searched, null, $"Listed what was said about \"{query}\" · {Plural(shown, "message", "messages")}",
            Structured: ToolData.Matches(listed.Take(shown).Select(e => new MemoryEventHit(e, null, 0))));
    }

    // The matches in conversations the page did not show, a line each: an item said in many conversations is still seen
    // whole, so it can be counted (spec 057).
    private static string Others(IReadOnlyList<MemoryEventHit> hits, List<string> shown, string query)
    {
        var others = hits.Where(h => !shown.Contains(Conversations.KeyOf(h.Event))).OrderBy(h => h.Event.OccurredAt).ToList();
        return others.Count == 0 ? string.Empty
            : "\n\nOther conversations that match (read one with events_read):\n" + string.Join('\n', others.Select(h =>
                $"- {h.Event.OccurredAt.UtcDateTime.ToString(EngineConstants.Dates.Day, CultureInfo.InvariantCulture)} #{h.Event.Id} {Who(h.Event)}: {EventSearch.Around(h.Event.Text, query, ListExcerpt)}"));
    }

    // About this many characters of each listed message, around the match.
    private const int ListExcerpt = 160;

    // The conversations to show: those where the best facts were said first, then the best by their turns' scores.
    private async Task<List<RecallPage.Session>> SessionsAsync(IReadOnlyList<MemoryEventHit> hits, List<Abstractions.Facts.Fact> facts, bool withPrivate, CancellationToken cancellationToken)
    {
        var sessions = new List<RecallPage.Session>();
        foreach (var fact in facts.Where(f => f.Sources.Count > 0).Take(MaxFactSessions))
        {
            if (await Conversations.EventAsync(EventLog, UserId, fact.Sources[0], withPrivate, cancellationToken) is { } source
                && sessions.All(s => Conversations.KeyOf(s.Anchor) != Conversations.KeyOf(source)))
            {
                sessions.Add(new RecallPage.Session(source, [], $"where {fact.Subject} · {fact.Attribute} was said"));
            }
        }

        var groups = hits.GroupBy(h => Conversations.KeyOf(h.Event)).ToList();
        var count = RecallPage.Count([.. groups.Select(g => g.Max(h => h.Score))], Session.Limits.RecallSessionsMax);
        foreach (var group in groups)
        {
            var anchored = sessions.FindIndex(s => Conversations.KeyOf(s.Anchor) == group.Key);
            if (anchored >= 0)
            {
                sessions[anchored] = sessions[anchored] with { Matched = [.. group.Select(h => h.Event.Id)] };
            }
            else if (count-- > 0)
            {
                sessions.Add(new RecallPage.Session(group.First().Event, [.. group.Select(h => h.Event.Id)], null));
            }
        }

        return sessions;
    }

    // The conversations of this many of the best facts come first.
    private const int MaxFactSessions = 2;

    private const int MaxOtherDays = 10;

    // A model often puts in the window the days the question is about, while events carry the day they were said: when
    // the window holds nothing, it hears on which other days matches were said, instead of concluding there are none.
    private async Task<string> NothingAsync(string query, string? from, string? to, bool includePrivate, CancellationToken cancellationToken)
    {
        const string Nothing = "Nothing that was said matches.";
        if (Day(from) is null && Day(to) is null)
        {
            return Nothing;
        }

        var outside = (await EventLog.SearchEventsAsync(UserId, query, null, null, includePrivate && !Session.IsConsolidation, cancellationToken)).Value ?? [];
        if (outside.Count == 0)
        {
            return Nothing;
        }

        var days = outside.Select(h => h.Event.OccurredAt.UtcDateTime.ToString(EngineConstants.Dates.Day, CultureInfo.InvariantCulture)).Distinct().Order(StringComparer.Ordinal).Take(MaxOtherDays);
        return $"Nothing that was said between {from ?? "the start"} and {to ?? "today"} matches. {Plural(outside.Count, "match was", "matches were")} said on other days: {string.Join(", ", days)}. " +
            "The dates are the days of what was said, not of what it tells: search again without from and to.";
    }

    private static DateTimeOffset? Day(string? day) =>
        DateTimeOffset.TryParseExact(day, EngineConstants.Dates.Day, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value : null;
}
