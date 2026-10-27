using Microsoft.Extensions.Options;

namespace Filum.Mcp.Configuration;

// A folder setting that is not a path stops the start.
internal sealed class McpOptionsValidator : IValidateOptions<McpOptions>
{
    public ValidateOptionsResult Validate(string? name, McpOptions options)
    {
        var failures = new List<string>();
        Check(options.Home, McpConstants.Variables.Home, failures);
        Check(options.Models, McpConstants.Variables.Models, failures);
        if (!string.IsNullOrEmpty(options.Tools) && !options.AllTools && !string.Equals(options.Tools, McpConstants.Variables.CoreProfile, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"{McpConstants.Variables.Tools} is \"{options.Tools}\". Set it to {McpConstants.Variables.CoreProfile} or {McpConstants.Variables.FullProfile}.");
        }

        if (options.HasPack)
        {
            Check(options.Pack, McpConstants.Variables.Pack, failures);
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void Check(string? folder, string variable, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(folder) || folder.AsSpan().IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            failures.Add($"{variable} is not a folder path. Set {variable} to a folder you can write.");
        }
    }
}
