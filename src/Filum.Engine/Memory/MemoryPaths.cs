using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Filum.Engine.Abstractions.Memory;

namespace Filum.Engine.Memory;

/// <summary>
/// What a memory path may look like: absolute, <c>/</c>-separated, letters (any script), digits, <c>-</c>, <c>_</c>
/// and <c>.</c>, ending in <c>.md</c> (a document) or <c>.csv</c> (a collection). Paths are case-sensitive.
/// </summary>
public static partial class MemoryPaths
{
    /// <summary>The path of the person's core.</summary>
    public const string CorePath = "/filum.md";
    internal const int MaxLength = 200;
    private const string PathRequired = "The path is required.";

    /// <summary>Why <paramref name="path"/> is not a valid memory path, or null when it is.</summary>
    public static string? Validate(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return PathRequired;
        }

        if (path.Length > MaxLength)
        {
            return $"The path is too long (maximum {MaxLength} characters).";
        }

        if (!path.StartsWith('/'))
        {
            return "The path must start with '/'.";
        }

        var segments = path[1..].Split('/');
        if (segments.Any(s => s.Length == 0))
        {
            return "The path has an empty segment.";
        }

        if (segments.Any(s => s is "." or ".."))
        {
            return "The path cannot contain '.' or '..' segments.";
        }

        if (!AllowedPath().IsMatch(path))
        {
            return "The path may only contain letters, digits, '-', '_', '.' and '/'.";
        }

        if (!path.EndsWith(EngineConstants.FileExtensions.Document, StringComparison.Ordinal) && !path.EndsWith(EngineConstants.FileExtensions.Collection, StringComparison.Ordinal))
        {
            return "Only .md (documents) and .csv (collections) files can be stored.";
        }

        return null;
    }

    internal static bool IsInvalid([NotNullWhen(false)] string? path, [NotNullWhen(true)] out string? error)
    {
        error = Validate(path);
        if (error is not null || path is null)
        {
            error ??= PathRequired;
            return true;
        }

        return false;
    }

    /// <summary>A prefix to list or search under: <c>/</c>, or an absolute path without empty segments.</summary>
    public static string? ValidatePrefix(string? prefix)
    {
        if (string.IsNullOrEmpty(prefix) || prefix == "/")
        {
            return null;
        }

        if (!prefix.StartsWith('/') || prefix.Length > MaxLength)
        {
            return "The prefix must start with '/' and be at most 200 characters.";
        }

        return prefix.TrimEnd('/')[1..].Split('/').Any(s => s is "" or "." or "..")
            ? "The prefix has an empty, '.' or '..' segment."
            : null;
    }

    /// <summary>Whether the file at <paramref name="path"/> is a document or a collection.</summary>
    public static MemoryFileKind KindOf(string path) =>
        path.EndsWith(EngineConstants.FileExtensions.Collection, StringComparison.Ordinal) ? MemoryFileKind.Collection : MemoryFileKind.Document;

    /// <summary>Whether <paramref name="path"/> is the core.</summary>
    public static bool IsCore(string path) => path == CorePath;

    [GeneratedRegex(@"^(/[\p{L}\p{N}_.\-]+)+$")]
    private static partial Regex AllowedPath();
}
