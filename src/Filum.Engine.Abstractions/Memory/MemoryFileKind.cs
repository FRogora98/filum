namespace Filum.Engine.Abstractions.Memory;

/// <summary>What a memory file is, from its extension.</summary>
public enum MemoryFileKind
{
    /// <summary>Free text, a <c>.md</c> file.</summary>
    Document,

    /// <summary>Entries with the same fields, a <c>.csv</c> file written by the code.</summary>
    Collection
}
