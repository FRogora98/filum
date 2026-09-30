namespace Filum.Engine;

public static class MemorySensitivity
{
    public const string Normal = "normal";
    public const string Sensitive = "sensitive";
    public const string Private = "private";

    public static readonly IReadOnlyList<string> All = [Normal, Sensitive, Private];
}

public static class MemoryOperation
{
    public const string CreateCore = "create_core";
    public const string Write = "write";
    public const string Edit = "edit";
    public const string Append = "append";
    public const string Delete = "delete";
    public const string Move = "move";
    public const string Sensitivity = "sensitivity";
    public const string Undo = "undo";
    public const string Rollback = "rollback";
    public const string Restore = "restore";

    /// <summary>A change the person made to a local memory folder by hand, adopted by the store (spec 012).</summary>
    public const string Outside = "outside";
}

public static class MemoryAuthor
{
    public const string Agent = "agent";
    public const string Person = "person";
    public const string Platform = "platform";
}
