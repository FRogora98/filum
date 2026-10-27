namespace Filum.Engine.Memory.Files;

internal sealed record ChangeTarget(string Path, string Content, string Sensitivity, bool Deleted, string? Refusal = null, bool Missing = false)
{
    public static ChangeTarget Refuse(string reason) => new(string.Empty, string.Empty, string.Empty, false, reason);

    public static ChangeTarget NotFound(string reason) => new(string.Empty, string.Empty, string.Empty, false, reason, Missing: true);
}
