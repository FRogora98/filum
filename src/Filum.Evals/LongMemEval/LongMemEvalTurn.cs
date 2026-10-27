using System.Text.Json.Serialization;

namespace Filum.Evals.LongMemEval;

internal sealed record LongMemEvalTurn([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string Content, [property: JsonPropertyName("has_answer")] bool? HasAnswer = null)
{
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";

    public bool ByAssistant => Role == AssistantRole;
}
