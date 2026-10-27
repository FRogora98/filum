using System.Text.Json;

namespace Filum.Evals.Mcp;

internal static class AgentOutput
{
    public static AgentSession Parse(string stream)
    {
        var tools = new List<AgentToolCall>();
        string? answer = null;
        string? model = null;
        string? error = null;
        foreach (var line in stream.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Read(line) is not { } item)
            {
                continue;
            }

            var type = item.TryGetProperty(EvalsConstants.AgentStream.Type, out var t) ? t.GetString() : null;
            if (type == EvalsConstants.AgentStream.SystemType && item.TryGetProperty(EvalsConstants.AgentStream.Model, out var declared) && declared.ValueKind == JsonValueKind.String)
            {
                model = declared.GetString();
            }

            if (type == EvalsConstants.AgentStream.AssistantType)
            {
                tools.AddRange(ToolCalls(item));
            }
            else if (type == EvalsConstants.AgentStream.ResultType)
            {
                answer = item.TryGetProperty(EvalsConstants.AgentStream.Result, out var result) ? result.GetString() : null;
                if (item.TryGetProperty(EvalsConstants.AgentStream.IsError, out var isError) && isError.ValueKind == JsonValueKind.True)
                {
                    error = $"the agent reported an error: {answer}";
                }
            }
        }

        return new AgentSession(answer ?? string.Empty, tools, answer is null && error is null ? "the agent gave no result" : error, model);
    }

    // Null for a line that is not JSON: the agent may print other things.
    private static JsonElement? Read(string line)
    {
        try
        {
            return JsonDocument.Parse(line).RootElement;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<AgentToolCall> ToolCalls(JsonElement item) =>
        item.TryGetProperty(EvalsConstants.AgentStream.Message, out var message) && message.TryGetProperty(EvalsConstants.AgentStream.Content, out var content) && content.ValueKind == JsonValueKind.Array
            ? content.EnumerateArray()
                .Where(c => c.TryGetProperty(EvalsConstants.AgentStream.Type, out var kind) && kind.GetString() == EvalsConstants.AgentStream.ToolUseType
                    && c.TryGetProperty(EvalsConstants.AgentStream.Name, out var name) && name.ValueKind == JsonValueKind.String)
                .Select(c => new AgentToolCall(c.GetProperty(EvalsConstants.AgentStream.Name).GetString()!,
                    c.TryGetProperty(EvalsConstants.AgentStream.Input, out var input) ? input.Clone() : null))
                .ToList()
            : [];
}
