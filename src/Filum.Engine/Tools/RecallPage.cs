using System.Globalization;
using System.Text;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Events;
using Filum.Engine.Memory.Contracts;
using static Filum.Engine.Tools.ToolText;

namespace Filum.Engine.Tools;

// What a search of what was said shows of the conversations (spec 042): the best ones, down to the largest drop in score,
// each as whole as its share of a character budget allows; what one leaves goes to the next.
internal sealed class RecallPage(IMemoryEvents log, Guid userId, bool withPrivate, string query, int budget)
{
    // A neighbour of a matching turn is shown cut rather than left out when at least this much of it fits.
    private const int MinNeighbourChars = 200;

    // An excerpt never gets a window smaller than this.
    private const int MinWindow = 150;

    // A conversation to show: an event of it, the events of it that matched, and why it is shown when it is a fact's.
    public sealed record Session(MemoryEvent Anchor, List<long> Matched, string? Source);

    // How many of the best conversations to show: down to the largest drop in score among the first max, at least one;
    // the score after the last is the next conversation's, or zero.
    public static int Count(IReadOnlyList<double> scores, int max)
    {
        var best = 0;
        var drop = double.MinValue;
        for (var i = 0; i < Math.Min(scores.Count, max); i++)
        {
            var next = i + 1 < scores.Count ? scores[i + 1] : 0;
            // A tie goes to the later drop, so equal scores show as many as allowed.
            if (scores[i] - next >= drop)
            {
                drop = scores[i] - next;
                best = i + 1;
            }
        }

        return best;
    }

    public async Task<string> ShowAsync(IReadOnlyList<Session> sessions, CancellationToken cancellationToken)
    {
        var titles = await log.ConversationTitlesAsync(userId, sessions.Select(s => s.Anchor.ConversationId), cancellationToken);
        var text = new StringBuilder();
        var left = budget;
        for (var i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            var all = await Conversations.OfAsync(log, userId, session.Anchor, withPrivate, cancellationToken);
            var title = session.Anchor.ConversationId is not { } id ? "Messages" : titles.TryGetValue(id, out var t) ? $"Conversation \"{t}\"" : "Conversation";
            text.Append(CultureInfo.InvariantCulture, $"=== {title} · {all[0].OccurredAt.UtcDateTime:yyyy-MM-dd}{(session.Source is null ? string.Empty : $" · {session.Source}")}\n");
            left -= Show(all, session.Matched.Count > 0 ? session.Matched : [session.Anchor.Id], left / (sessions.Count - i), text);
            text.Append('\n');
        }

        return text.ToString().TrimEnd();
    }

    // The whole conversation when it fits its share; else its matching turns with their neighbours, and where to read the
    // rest. How many characters of what was said it showed.
    private int Show(List<MemoryEvent> all, List<long> matched, int share, StringBuilder text)
    {
        if (all.Sum(e => e.Text.Length) <= share)
        {
            all.ForEach(e => Line(e, e.Text, text));
            return all.Sum(e => e.Text.Length);
        }

        var at = matched.Select(id => all.FindIndex(e => e.Id == id)).Where(i => i >= 0).Distinct().Order().ToList();
        var shown = new SortedDictionary<int, string>();
        var used = 0;
        foreach (var i in at)
        {
            var allowance = Math.Max(MinWindow * 2, (share - used) / Math.Max(1, at.Count - shown.Count));
            var said = all[i].Text.Length <= allowance ? all[i].Text : EventSearch.Excerpt(all[i].Text, query, Math.Max(MinWindow, allowance / 2));
            shown[i] = said.Length <= allowance ? said : said[..allowance];
            used += shown[i].Length;
        }

        foreach (var i in at.SelectMany(i => new[] { i - 1, i + 1 }).Where(i => i >= 0 && i < all.Count && !shown.ContainsKey(i)).Distinct())
        {
            var room = share - used;
            if (room < MinNeighbourChars)
            {
                break;
            }

            shown[i] = EventSearch.Cut(all[i].Text, room - 1);
            used += shown[i].Length;
        }

        var previous = -1;
        foreach (var (i, said) in shown)
        {
            if (previous >= 0 && i > previous + 1)
            {
                text.Append("[…]\n\n");
            }

            Line(all[i], said, text);
            previous = i;
        }

        text.Append(CultureInfo.InvariantCulture, $"({Plural(all.Count, "message")} in all: read the conversation with {ToolNames.EventsRead} {all[at.DefaultIfEmpty(0).First()].Id})\n");
        return used;
    }

    private static void Line(MemoryEvent e, string said, StringBuilder text) =>
        text.Append(CultureInfo.InvariantCulture, $"#{e.Id} · {e.OccurredAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC · {Who(e)}:\n{said}\n\n");
}
