using Filum.Evals.Mcp;
using Filum.Evals.Mcp.Contracts;

namespace Filum.Evals.UnitTests.Fakes;

internal sealed class PromptAgent : IAgentStrategy
{
    public string Name => "prompt agent";

    public List<(string Message, bool Filum)> Sessions { get; } = [];

    public Task<AgentSession> AskAsync(string message, AgentChat? chat, string workingDirectory, string mcpConfig, bool filumMounted, CancellationToken cancellationToken)
    {
        Sessions.Add((message, filumMounted));
        return Task.FromResult(new AgentSession("ok", [], null, "claude-sonnet-test"));
    }
}
