using System.Text;

namespace Filum.Evals.LongMemEval;

// The same for every system and model: each history session is a new chat, in date order, in parts when longer than a
// message may be; the question comes last, in a new chat, with its date.
internal static class LongMemEvalProtocol
{
    // A host's limit for one turn.
    public const int MaxMessage = 8000;

    // Kept free in every part for the header's "(part n of m)" marker.
    private const int PartMarkerRoom = 40;

    public static string Header(string date) =>
        $"Here is a conversation you had with the person on {date}. Keep in your memory what is worth remembering about the person and about what was said.";

    public static string Transcript(IReadOnlyList<LongMemEvalTurn> turns) =>
        string.Join("\n", turns.Select(t => $"{(t.ByAssistant ? "Assistant" : "Person")}: {t.Content}"));

    // Split between turns where it can be.
    public static IReadOnlyList<string> SessionMessages(string date, IReadOnlyList<LongMemEvalTurn> turns, int max = MaxMessage)
    {
        var header = Header(date);
        var room = max - header.Length - PartMarkerRoom;
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

    public static string Question(LongMemEvalInstance instance) => $"Today is {instance.QuestionDate}. {instance.Question}";

    // For the systems that read the history in their prompt.
    public static string QuestionAfter(IEnumerable<(string Date, IReadOnlyList<LongMemEvalTurn> Turns)> sessions, LongMemEvalInstance instance) =>
        $"Here are past conversations with the person:\n\n{History(sessions)}\n\n{Question(instance)}";

    // For the long-context and retrieval references.
    public static string History(IEnumerable<(string Date, IReadOnlyList<LongMemEvalTurn> Turns)> sessions) =>
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
