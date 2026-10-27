using Filum.Engine.Abstractions.Tools;
using Filum.Engine.Tools;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;

namespace Filum.Mcp.Tools;

// MemoryTools is made for one turn (it counts calls and keeps the steps); here every call is a turn. A refused call is
// a tool result with isError, known from the step the call recorded, not from its text (spec 040).
internal sealed class PerCallFunction(AIFunction template, Func<MemoryTools> fresh) : DelegatingAIFunction(template)
{
    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var tools = fresh();
        var function = tools.Tools.OfType<AIFunction>().Single(t => t.Name == Name);
        var result = await function.InvokeAsync(arguments, cancellationToken);
        if (ToolText.Unwrap(result) is not { } text)
        {
            return result;
        }

        var refused = tools.Steps.Count > 0 && tools.Steps[^1].Kind == ToolStep.Failed;
        return new CallToolResult { Content = [new TextContentBlock { Text = text }], IsError = refused, StructuredContent = refused ? null : tools.LastStructuredContent };
    }
}
