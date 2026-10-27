using System.Text.Json;

namespace Filum.Evals.Mcp;

/// <summary>A tool the agent called, with its input when the stream gave one.</summary>
internal sealed record AgentToolCall(string Name, JsonElement? Input = null);
