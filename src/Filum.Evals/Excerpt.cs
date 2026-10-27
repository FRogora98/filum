namespace Filum.Evals;

internal static class Excerpt
{
    private const string Ellipsis = "…";

    private const int CutBeforeLimit = 3;

    public static string Of(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - CutBeforeLimit)] + Ellipsis;

    public static string Quoted(string text, int maxLength) => $"\"{Of(text, maxLength).ReplaceLineEndings(" ")}\"";
}
