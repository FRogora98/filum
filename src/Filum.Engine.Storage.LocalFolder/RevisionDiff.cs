using System.Text;

namespace Filum.Engine.Storage.LocalFolder;

// A line diff for the revision log: exact for any two texts, small for the usual change (a few lines edited anywhere
// and lines added). It walks both texts and, where they part, looks a few lines ahead for where they meet again; past
// that it gives up with one hunk to the end, which is still exact.
internal static class RevisionDiff
{
    // How far apart, in lines of both texts together, a match is looked for.
    private const int Window = 16;

    public static List<RevisionHunk> Hunks(string previous, string content)
    {
        var before = LinesOf(previous);
        var after = LinesOf(content);
        var hunks = new List<RevisionHunk>();
        var (i, j) = (0, 0);
        while (i < before.Count || j < after.Count)
        {
            if (i < before.Count && j < after.Count && before[i] == after[j])
            {
                (i, j) = (i + 1, j + 1);
                continue;
            }

            var (skipBefore, skipAfter) = Resync(before, after, i, j);
            hunks.Add(new RevisionHunk(i, skipBefore, string.Concat(after.Skip(j).Take(skipAfter))));
            (i, j) = (i + skipBefore, j + skipAfter);
        }

        return hunks;
    }

    public static string Apply(string previous, IReadOnlyList<RevisionHunk> hunks)
    {
        var before = LinesOf(previous);
        var text = new StringBuilder();
        var at = 0;
        foreach (var hunk in hunks)
        {
            for (; at < hunk.At && at < before.Count; at++)
            {
                text.Append(before[at]);
            }

            text.Append(hunk.Insert);
            at = hunk.At + hunk.Remove;
        }

        for (; at < before.Count; at++)
        {
            text.Append(before[at]);
        }

        return text.ToString();
    }

    // The lines of a text with their line breaks, so joining them gives the text back exactly.
    internal static List<string> LinesOf(string text)
    {
        var lines = new List<string>();
        var start = 0;
        for (var k = 0; k < text.Length; k++)
        {
            if (text[k] == '\n')
            {
                lines.Add(text[start..(k + 1)]);
                start = k + 1;
            }
        }

        if (start < text.Length)
        {
            lines.Add(text[start..]);
        }

        return lines;
    }

    // The nearest point, fewest lines skipped first, where both texts have the same line again; when there is none
    // within the window, the rest of both.
    private static (int Before, int After) Resync(List<string> before, List<string> after, int i, int j)
    {
        for (var skipped = 1; skipped <= Window; skipped++)
        {
            for (var skipBefore = 0; skipBefore <= skipped; skipBefore++)
            {
                var skipAfter = skipped - skipBefore;
                if (i + skipBefore < before.Count && j + skipAfter < after.Count && before[i + skipBefore] == after[j + skipAfter])
                {
                    return (skipBefore, skipAfter);
                }
            }
        }

        return (before.Count - i, after.Count - j);
    }
}
