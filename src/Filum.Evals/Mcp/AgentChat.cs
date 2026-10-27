namespace Filum.Evals.Mcp;

/// <summary>
/// A scenario's chat as one agent session in its own folder: started by the first turn, continued by the next ones
/// (spec 056). Claude Code finds a session again by its folder.
/// </summary>
internal sealed record AgentChat(Guid SessionId, string Folder, bool Started = false);
