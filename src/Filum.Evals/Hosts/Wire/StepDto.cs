using System.Text.Json;

namespace Filum.Evals.Hosts.Wire;

internal sealed record StepDto(string Kind, string Tool, string? Path, string Description, int DurationMs, long? RevisionId, string? Error = null, int? Rows = null, bool Undone = false, JsonElement? Data = null)
{
    public const string Read = "read";
    public const string Searched = "searched";
    public const string Listed = "listed";
    public const string Computed = "computed";
    public const string Wrote = "wrote";
    public const string Asked = "asked";
    public const string Failed = "failed";
    public const string Used = "used";
}
