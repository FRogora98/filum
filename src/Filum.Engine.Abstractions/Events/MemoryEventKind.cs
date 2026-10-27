namespace Filum.Engine.Abstractions.Events;

/// <summary>What an event of the log is.</summary>
public static class MemoryEventKind
{
    /// <summary>A message of the person.</summary>
    public const string Said = "said";

    /// <summary>The agent's answer.</summary>
    public const string Answered = "answered";

    /// <summary>An item from a file or, later, a connector.</summary>
    public const string Imported = "imported";

    /// <summary>An explicit instruction ("from now on…"), recorded when a turn changed the core's rules.</summary>
    public const string Told = "told";

    /// <summary>The person changed, deleted or forgot something.</summary>
    public const string Corrected = "corrected";

    /// <summary>A change to the memory's files, with the events it came from and the revisions it made.</summary>
    public const string Derived = "derived";

    /// <summary>A structural change consolidation suggests, waiting for the person's answer.</summary>
    public const string Proposed = "proposed";

    /// <summary>What was said or brought in: the events a search and a consolidation read.</summary>
    public static readonly IReadOnlyList<string> Spoken = [Said, Answered, Imported];
}
