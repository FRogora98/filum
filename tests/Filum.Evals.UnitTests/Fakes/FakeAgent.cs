using Filum.Evals.Mcp;
using Filum.Evals.Mcp.Contracts;

namespace Filum.Evals.UnitTests.Fakes;

// Answers from the memory only when filum is mounted.
internal sealed class FakeAgent : IAgentStrategy
{
    public string Name => "fake agent";

    public List<(string Message, AgentChat? Chat, string WorkingDirectory, bool FilumMounted)> Sessions { get; } = [];

    public Task<AgentSession> AskAsync(string message, AgentChat? chat, string workingDirectory, string mcpConfig, bool filumMounted, CancellationToken cancellationToken)
    {
        Sessions.Add((message, chat, workingDirectory, filumMounted));
        return Task.FromResult(new AgentSession(filumMounted ? "It is under the mat." : "I don't know.", filumMounted ? [new AgentToolCall("mcp__filum__memory_write")] : [], null));
    }
}
