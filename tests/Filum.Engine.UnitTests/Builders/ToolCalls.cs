using Filum.Engine.Tools;
using Microsoft.Extensions.AI;

namespace Filum.Engine.UnitTests.Builders;

internal static class ToolCalls
{
    // The quotes and the escapes of the JSON form taken away.
    public static async Task<string> Call(MemoryTools tools, string name, Dictionary<string, object?> arguments)
    {
        var function = tools.Tools.OfType<AIFunction>().Single(t => t.Name == name);
        return (await function.InvokeAsync(new AIFunctionArguments(arguments), CancellationToken.None))?.ToString()?.Trim('"').Replace("\\n", "\n").Replace("\\u2026", "…").Replace("\\u0022", "\"").Replace("\\\"", "\"") ?? string.Empty;
    }
}
