using System.Text.Json;
using Filum.Engine.Abstractions.Constants;

namespace Filum.Mcp.Tools;

// The shapes of the structured content the engine gives for these tools (spec 040); the others answer in text only.
internal static class OutputSchemas
{
    private const string Text = """{ "type": "string" }""";

    private static readonly JsonElement Facts = Parse($$"""
        { "type": "object", "required": ["facts"], "properties": { "facts": { "type": "array", "items": {
            "type": "object", "required": ["subject", "attribute", "value", "from", "to"],
            "properties": { "subject": {{Text}}, "attribute": {{Text}}, "value": {{Text}}, "from": {{Text}}, "to": {{Text}},
                "ended": {{Text}}, "recordedAt": {{Text}}, "author": {{Text}}, "quote": {{Text}} } } } } }
        """);

    private static readonly JsonElement Matches = Parse($$"""
        { "type": "object", "required": ["matches"], "properties": { "matches": { "type": "array", "items": {
            "type": "object", "required": ["id", "date", "who", "text"],
            "properties": { "id": { "type": "integer" }, "date": {{Text}}, "who": {{Text}}, "text": {{Text}} } } } } }
        """);

    public static JsonElement? Of(string tool) => tool switch
    {
        ToolNames.FactsCurrent or ToolNames.FactsHistory => Facts,
        ToolNames.EventsSearch => Matches,
        _ => null,
    };

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
