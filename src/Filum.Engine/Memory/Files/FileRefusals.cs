using System.Diagnostics.CodeAnalysis;

namespace Filum.Engine.Memory.Files;

// Refusals several areas give, in the same words.
internal static class FileRefusals
{
    public const string CoreCannotBeDeleted = "The core (/filum.md) cannot be deleted; edit it instead.";

    public const string NothingToChange = "Nothing to change.";

    public const string QueryLength = "The query must be between 1 and 200 characters.";

    public const int MaxQueryLength = 200;

    public static string NoFileAt(string? path) => $"There is no file at {path}.";

    public static string NoChange(long revisionId) => $"There is no change {revisionId}.";

    public static string NotAValidCollection(string? path, string? error) => $"{path} is not a valid collection: {error}";

    public static bool IsValidQuery([NotNullWhen(true)] string? query) => !string.IsNullOrWhiteSpace(query) && query.Length <= MaxQueryLength;
}
