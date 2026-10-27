using System.Text.Json;

namespace Filum.Engine.Tools;

internal sealed record ToolDone(string ForModel, string Kind, string? Path, string Description, long? RevisionId = null, int? Rows = null, JsonElement? Data = null, JsonElement? Structured = null);
