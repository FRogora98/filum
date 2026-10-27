using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Filum.Evals.LongMemEval;

// LongMemEval, MIT, © 2024 Di Wu: https://github.com/xiaowu0162/LongMemEval. The answer is a text or a number in the
// files, so it is kept as JSON and read as text.
internal sealed record LongMemEvalInstance(
    [property: JsonPropertyName("question_id")] string QuestionId,
    [property: JsonPropertyName("question_type")] string QuestionType,
    [property: JsonPropertyName("question")] string Question,
    [property: JsonPropertyName("answer")] JsonElement Answer,
    [property: JsonPropertyName("question_date")] string QuestionDate,
    [property: JsonPropertyName("haystack_dates")] IReadOnlyList<string> HaystackDates,
    [property: JsonPropertyName("haystack_sessions")] IReadOnlyList<IReadOnlyList<LongMemEvalTurn>> HaystackSessions,
    [property: JsonPropertyName("haystack_session_ids")] IReadOnlyList<string>? HaystackSessionIds = null,
    [property: JsonPropertyName("answer_session_ids")] IReadOnlyList<string>? AnswerSessionIds = null)
{
    private static readonly string[] DateFormats = ["yyyy/MM/dd HH:mm", "yyyy/MM/dd"];

    // Around the weekday in a date of the files: "2023/04/10 (Mon) 17:50".
    private const string WeekdayStart = " (";
    private const string WeekdayEnd = ") ";

    public string AnswerText => Answer.ValueKind == JsonValueKind.String && Answer.GetString() is { } text ? text : Answer.GetRawText();

    public bool IsAbstention => QuestionId.EndsWith(LongMemEvalQuestionTypes.AbstentionSuffix, StringComparison.Ordinal);

    public string Ability => IsAbstention ? LongMemEvalAbilities.Abstention : QuestionType switch
    {
        LongMemEvalQuestionTypes.SingleSessionUser or LongMemEvalQuestionTypes.SingleSessionAssistant or LongMemEvalQuestionTypes.SingleSessionPreference => LongMemEvalAbilities.Extraction,
        LongMemEvalQuestionTypes.MultiSession => LongMemEvalAbilities.MultiSession,
        LongMemEvalQuestionTypes.KnowledgeUpdate => LongMemEvalAbilities.KnowledgeUpdate,
        LongMemEvalQuestionTypes.TemporalReasoning => LongMemEvalAbilities.Temporal,
        _ => QuestionType
    };

    // In date order: the files do not keep them in that order.
    public IEnumerable<(string Date, IReadOnlyList<LongMemEvalTurn> Turns)> Sessions =>
        HaystackSessions
            .Select((turns, i) => (Date: i < HaystackDates.Count ? HaystackDates[i] : string.Empty, Turns: turns, Index: i))
            .OrderBy(s => When(s.Date))
            .ThenBy(s => s.Index)
            .Select(s => (s.Date, s.Turns));

    // One that does not read sorts first, in file order.
    public static DateTime When(string date)
    {
        var cut = date.IndexOf(WeekdayStart, StringComparison.Ordinal);
        var time = cut > 0 && date.IndexOf(WeekdayEnd, cut, StringComparison.Ordinal) is var close and > 0 ? date[..cut] + " " + date[(close + WeekdayEnd.Length)..] : date;
        return DateTime.TryParseExact(time, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var when)
            ? when
            : DateTime.MinValue;
    }

    public int HistoryCharacters => HaystackSessions.Sum(s => s.Sum(t => t.Content.Length));
}
