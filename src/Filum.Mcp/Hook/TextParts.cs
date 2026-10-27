namespace Filum.Mcp.Hook;

internal static class TextParts
{
    // Cut at a line end when there is one.
    public static IReadOnlyList<string> Split(string text)
    {
        const int max = McpConstants.Limits.MaxText;
        var parts = new List<string>();
        var rest = text;
        while (rest.Length > max)
        {
            var cut = rest.LastIndexOf('\n', max - 1);
            cut = cut <= 0 ? max : cut + 1;
            parts.Add(rest[..cut].TrimEnd());
            rest = rest[cut..];
        }

        if (rest.Trim().Length > 0)
        {
            parts.Add(rest.Trim());
        }

        return parts;
    }
}
