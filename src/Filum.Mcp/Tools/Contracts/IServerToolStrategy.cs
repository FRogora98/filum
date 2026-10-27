using Microsoft.Extensions.AI;

namespace Filum.Mcp.Tools.Contracts;

/// <summary>
/// A tool of the server's own, beside the engine's catalog: Filum has no model and sees no conversation here, so the
/// host's model logs what the person said and tidies the memory when Filum asks it to.
/// </summary>
internal interface IServerToolStrategy
{
    AIFunction Function();
}
