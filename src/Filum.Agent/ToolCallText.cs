namespace Filum.Agent;

/// <summary>
/// A tool call a model wrote into its answer as text instead of making it: the syntax some models use for their own
/// tool calls (for example Gemma's <c>&lt;|tool_call&gt;</c>, Hermes' <c>&lt;tool_call&gt;</c>, Mistral's
/// <c>[TOOL_CALLS]</c>, DeepSeek's <c>&lt;｜tool▁calls▁begin｜&gt;</c>) leaking out when the provider does not parse it.
/// The call never happened, and the text is never shown to the person.
/// </summary>
public static class ToolCallText
{
    /// <summary>Shown when nothing but a leaked tool call was left of the answer.</summary>
    public const string NothingLeft = "I tried to make a change but could not complete it.";

    private static readonly string[] Markers =
    [
        "<|tool_call", "<tool_call", "</tool_call", "<|\"|>", "[TOOL_CALLS]", "<|python_tag|>", "<function=", "<|tool_calls", "<｜tool▁"
    ];

    public static bool Contains(string? answer) => answer is not null && First(answer) >= 0;

    /// <summary>The answer without the leaked call and anything after it; <see cref="NothingLeft"/> when nothing remains.</summary>
    public static string Strip(string answer)
    {
        var at = First(answer);
        var kept = at < 0 ? answer : answer[..at].TrimEnd();
        return kept.Length == 0 ? NothingLeft : kept;
    }

    private static int First(string answer) =>
        Markers.Select(m => answer.IndexOf(m, StringComparison.Ordinal)).Where(i => i >= 0).DefaultIfEmpty(-1).Min();
}
