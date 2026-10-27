namespace Filum.Evals.Mcp;

/// <summary>What one agent session gave: its answer, the tools it called in order, an error, the model it declared.</summary>
internal sealed record AgentSession(string Answer, IReadOnlyList<AgentToolCall> Calls, string? Error, string? Model = null)
{
    public IReadOnlyList<string> Tools => Calls.Select(c => c.Name).ToList();
}
