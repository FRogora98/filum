namespace Filum.Engine.Abstractions.Constants;

/// <summary>What a revision did to its file, as the history records it.</summary>
public static class MemoryOperation
{
    /// <summary>The core of a new memory was created.</summary>
    public const string CreateCore = "create_core";

    /// <summary>A file was created or its whole content replaced.</summary>
    public const string Write = "write";

    /// <summary>Part of a file was replaced.</summary>
    public const string Edit = "edit";

    /// <summary>Text was added at the end of a file.</summary>
    public const string Append = "append";

    /// <summary>A file was deleted; it stays in the history.</summary>
    public const string Delete = "delete";

    /// <summary>A file was renamed or moved, keeping its history.</summary>
    public const string Move = "move";

    /// <summary>A file's sensitivity changed.</summary>
    public const string Sensitivity = "sensitivity";

    /// <summary>The change of an earlier revision was taken back.</summary>
    public const string Undo = "undo";

    /// <summary>A file returned to its state before a failed turn.</summary>
    public const string Rollback = "rollback";

    /// <summary>An earlier version of a file was brought back.</summary>
    public const string Restore = "restore";

    /// <summary>A change the person made to a local memory folder by hand, adopted by the store.</summary>
    public const string Outside = "outside";

    /// <summary>Nothing was written: what was asked already held (spec 041). No revision has it.</summary>
    public const string Unchanged = "unchanged";
}
