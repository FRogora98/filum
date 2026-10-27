namespace Filum.Mcp.Session;

// What it logs and changes is dated to this conversation.
internal sealed record ServerSession(Guid Id)
{
    public static ServerSession New() => new(Guid.NewGuid());
}
