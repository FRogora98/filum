using System.Text;

namespace Filum.Evals.LongMemEval;

/// <summary>
/// How a LongMemEval question is played (spec 020), the same for every system and model: each history session is a
/// new chat, in date order, sent in parts when it is longer than a message may be; the question comes last, in a new
/// chat, with its date.
/// </summary>
public static class LmeProtocol
{
    /// <summary>The longest message a host takes (the turn's limit).</summary>
    public const int MaxMessage = 8000;

    public static string Header(string date) =>
        $"Here is a conversation you had with the person on {date}. Keep in your memory what is worth remembering about the person and about what was said.";

    public static string Transcript(IReadOnlyList<LmeTurn> turns) =>
        string.Join("\n", turns.Select(t => $"{(t.Role == "assistant" ? "Assistant" : "Person")}: {t.Content}"));

    /// <summary>A session as messages of at most <paramref name="max"/> characters, split between turns where it can be.</summary>
    public static IReadOnlyList<string> SessionMessages(string date, IReadOnlyList<LmeTurn> turns, int max = MaxMessage)
    {
        var header = Header(date);
        // The longest a header can be with its "(part n of m)" marker, kept free in every part.
        var room = max - header.Length - 40;
        var parts = new List<string>();
        var current = new StringBuilder();
        foreach (var line in Transcript(turns).Split('\n').SelectMany(l => Split(l, room)))
        {
            if (current.Length > 0 && current.Length + 1 + line.Length > room)
            {
                parts.Add(current.ToString());
                current.Clear();
            }

            current.Append(current.Length == 0 ? string.Empty : "\n").Append(line);
        }

        if (current.Length > 0 || parts.Count == 0)
        {
            parts.Add(current.ToString());
        }

        return parts.Count == 1
            ? [$"{header}\n\n{parts[0]}"]
            : parts.Select((p, i) => $"{header} (part {i + 1} of {parts.Count})\n\n{p}").ToList();
    }

    /// <summary>The question, with the day it is asked on.</summary>
    public static string Question(LmeInstance instance) => $"Today is {instance.QuestionDate}. {instance.Question}";

    /// <summary>The whole history as one text, for the long-context and retrieval references.</summary>
    public static string History(IEnumerable<(string Date, IReadOnlyList<LmeTurn> Turns)> sessions) =>
        string.Join("\n\n", sessions.Select(s => $"[Conversation on {s.Date}]\n{Transcript(s.Turns)}"));

    private static IEnumerable<string> Split(string line, int room)
    {
        for (var i = 0; i < line.Length; i += room)
        {
            yield return line.Substring(i, Math.Min(room, line.Length - i));
        }

        if (line.Length == 0)
        {
            yield return line;
        }
    }
}
