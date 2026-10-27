namespace Filum.Engine.Imports;

internal static class ImportText
{
    public const char ByteOrderMark = '\uFEFF';
    private const string LeftToRightMark = "\u200E";
    private const string RightToLeftMark = "\u200F";
    private const char NarrowNoBreakSpace = '\u202F';
    private const char NoBreakSpace = '\u00A0';

    public static string Cut(string text) => text.Length <= Importer.MaxItemChars ? text : text[..Importer.MaxItemChars] + "…";

    // Line breaks normalized; the invisible marks some exports carry removed or made plain spaces.
    public static string Clean(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').Replace(LeftToRightMark, string.Empty).Replace(RightToLeftMark, string.Empty)
            .Replace(NarrowNoBreakSpace, ' ').Replace(NoBreakSpace, ' ');
}
