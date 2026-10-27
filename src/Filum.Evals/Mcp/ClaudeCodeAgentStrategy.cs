using Filum.Evals.Mcp.Contracts;

namespace Filum.Evals.Mcp;

// Its own tools are off (--tools ""), so it cannot keep notes or memory of its own; only the MCP servers of the given
// config exist (--strict-mcp-config). Every session runs on an isolated config (ClaudeIsolation): the model sees nothing
// of the person running the eval.
internal sealed class ClaudeCodeAgentStrategy(string? model) : IAgentStrategy
{
    private readonly ClaudeIsolation _isolation = new(model);

    public string Name => "Claude Code";

    public string? Model => model;

    public async Task<AgentSession> AskAsync(string message, AgentChat? chat, string workingDirectory, string mcpConfig, bool filumMounted, CancellationToken cancellationToken)
    {
        var start = ClaudeProcess.Create(Arguments(mcpConfig, filumMounted, chat), workingDirectory);
        await _isolation.ApplyAsync(start, cancellationToken);
        var (exitCode, output, errors) = await ClaudeProcess.RunAsync(start, message, cancellationToken);
        var session = AgentOutput.Parse(output);
        return exitCode == 0 || session.Error is not null ? session : session with { Error = $"the agent exited with {exitCode}: {errors.Trim()}" };
    }

    internal IEnumerable<string> Arguments(string mcpConfig, bool filumMounted, AgentChat? chat)
    {
        string[] always = [EvalsConstants.AgentArguments.Print, EvalsConstants.AgentArguments.OutputFormat, EvalsConstants.AgentArguments.StreamJson, EvalsConstants.AgentArguments.Verbose, EvalsConstants.AgentArguments.StrictMcpConfig, EvalsConstants.AgentArguments.McpConfig, mcpConfig, EvalsConstants.AgentArguments.Tools, EvalsConstants.AgentArguments.NoTools];
        foreach (var argument in always)
        {
            yield return argument;
        }

        // A chat's later turns continue its session, so the model sees what was said before in it (spec 056).
        if (chat is not null)
        {
            yield return chat.Started ? EvalsConstants.AgentArguments.Resume : EvalsConstants.AgentArguments.SessionId;
            yield return chat.SessionId.ToString();
        }

        if (filumMounted)
        {
            yield return EvalsConstants.AgentArguments.AllowedTools;
            yield return AgentToolSteps.FilumToolPrefix + EvalsConstants.AgentArguments.AnyTool;
        }

        if (model is not null)
        {
            yield return EvalsConstants.AgentArguments.Model;
            yield return model;
        }

        // Settings beside the MCP config (the hook) go with it.
        var settings = Path.Combine(Path.GetDirectoryName(mcpConfig) ?? ".", McpConfigWriter.SettingsFile);
        if (filumMounted && File.Exists(settings))
        {
            yield return EvalsConstants.AgentArguments.Settings;
            yield return settings;
        }
    }
}
