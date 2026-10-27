using System.Text.Json;

namespace Filum.Evals;

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Indented = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}
