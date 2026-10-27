namespace Filum.Engine.Abstractions.Constants;

/// <summary>Who made a revision, as the history records it.</summary>
public static class MemoryAuthor
{
    /// <summary>The agent, in a turn of a conversation.</summary>
    public const string Agent = "agent";

    /// <summary>The person, from the app or by editing a file by hand.</summary>
    public const string Person = "person";

    /// <summary>The engine itself: a new memory's core, its starter skills, a rollback.</summary>
    public const string Platform = "platform";

    /// <summary>A consolidation pass.</summary>
    public const string Consolidation = "consolidation";
}
