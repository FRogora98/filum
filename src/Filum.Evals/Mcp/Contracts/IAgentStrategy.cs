namespace Filum.Evals.Mcp.Contracts;

/// <summary>An agent run through its command line: a new session per call, or a chat's session continued.</summary>
internal interface IAgentStrategy
{
    string Name { get; }

    Task<AgentSession> AskAsync(string message, AgentChat? chat, string workingDirectory, string mcpConfig, bool filumMounted, CancellationToken cancellationToken);
}
