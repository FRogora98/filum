namespace Filum.Engine.Abstractions.Events;

/// <summary>Where an event comes from.</summary>
public static class MemoryEventSource
{
    /// <summary>A conversation in the host's chat.</summary>
    public const string Chat = "chat";

    /// <summary>A file the person imported.</summary>
    public const string Import = "import";

    /// <summary>The person's app, outside a conversation.</summary>
    public const string App = "app";

    /// <summary>The agent itself.</summary>
    public const string Agent = "agent";

    /// <summary>A consolidation pass.</summary>
    public const string Consolidation = "consolidation";

    /// <summary>A live source the person connected: a mailbox, a calendar link.</summary>
    public const string Connector = "connector";

    /// <summary>An agent that hosts the engine through MCP, recording what the person said (<c>memory_log</c>).</summary>
    public const string Host = "host";
}
