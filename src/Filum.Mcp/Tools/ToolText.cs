using System.Text.Json;

namespace Filum.Mcp.Tools;

internal static class ToolText
{
    private const string Ellipsis = "…";

    // AIFunctionFactory hands back a string as a JSON string; null when the result is not text.
    public static string? Unwrap(object? result) => result switch
    {
        JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
        string text => text,
        _ => null
    };

    public static string Cut(string text, int length) => text.Length <= length ? text : text[..length] + Ellipsis;
}
