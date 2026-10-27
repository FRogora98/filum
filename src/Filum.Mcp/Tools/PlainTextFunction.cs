using Microsoft.Extensions.AI;

namespace Filum.Mcp.Tools;

// Its text result is sent as plain text, not as a JSON string.
internal sealed class PlainTextFunction(AIFunction inner) : DelegatingAIFunction(inner)
{
    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var result = await base.InvokeCoreAsync(arguments, cancellationToken);
        return ToolText.Unwrap(result) ?? result;
    }
}
