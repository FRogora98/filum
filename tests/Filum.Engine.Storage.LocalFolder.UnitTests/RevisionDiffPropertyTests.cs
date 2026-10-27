using FsCheck.Xunit;

namespace Filum.Engine.Storage.LocalFolder.UnitTests;

// Spec 039: the change between any two texts, applied to the first, gives the second. Texts are made of lines, with and
// without a final line break, so the line diff meets insertions, deletions, repeats and empty lines.
public sealed class RevisionDiffPropertyTests
{
    [Property(MaxTest = 1000)]
    public bool Apply_WhenGivenTheHunksBetweenTwoTexts_ShouldGiveTheSecond(string[] first, string[] second, bool firstEnds, bool secondEnds)
    {
        var previous = Text(first, firstEnds);
        var content = Text(second, secondEnds);

        return RevisionDiff.Apply(previous, RevisionDiff.Hunks(previous, content)) == content;
    }

    [Property(MaxTest = 1000)]
    public bool Apply_WhenTheTextIsUnchanged_ShouldNeedNoHunk(string[] lines, bool ends)
    {
        var text = Text(lines, ends);

        return RevisionDiff.Hunks(text, text).Count == 0;
    }

    private static string Text(string[] lines, bool endsWithBreak)
    {
        var text = string.Join("\n", (lines ?? []).Select(l => (l ?? string.Empty).Replace("\r", string.Empty, StringComparison.Ordinal)));
        return endsWithBreak && text.Length > 0 ? text + "\n" : text;
    }
}
