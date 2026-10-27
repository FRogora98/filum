using Filum.Engine.Memory.Files;

namespace Filum.Engine.Memory.Core;

/// <summary>
/// What the agent inferred about the person, kept apart from what the person told (the core):
/// their words for people, places and things, their habits and rhythms, how they like to be answered. One line each,
/// with the day it was noticed and where it came from. Read at every message, visible and editable like any file; an
/// inference never overrides a rule of the core.
/// </summary>
public static class LearnedFile
{
    /// <summary>Where what was learned lives in every memory.</summary>
    public const string Path = "/learned.md";

    // Past this, the instructions carry the newest lines.
    internal const int MaxChars = 3_000;

    internal const string Template = """
        # What Filum has learned about you

        Inferred, not told: each line says when it was noticed. Correct or delete any line; what you tell Filum always wins.

        """;

    internal static string ForInstructions(string content)
    {
        var lines = MemoryText.LinesOf(content).Where(l => l.TrimStart().StartsWith("- ", StringComparison.Ordinal)).ToList();
        if (lines.Count == 0)
        {
            return "(nothing yet)";
        }

        var kept = new List<string>();
        var length = 0;
        for (var i = lines.Count - 1; i >= 0 && length + lines[i].Length + 1 <= MaxChars; i--)
        {
            kept.Insert(0, lines[i]);
            length += lines[i].Length + 1;
        }

        return string.Join('\n', kept) + (kept.Count < lines.Count ? $"\n(and {lines.Count - kept.Count} older lines in {Path})" : string.Empty);
    }
}
