using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Filum.Evals.Cli;

// A switch reads "true"; other words are ignored.
internal sealed class CommandLineArguments
{
    private const string Prefix = "--";
    private const string SwitchValue = "true";

    private readonly Dictionary<string, string> _options;

    private CommandLineArguments(Dictionary<string, string> options) => _options = options;

    public static CommandLineArguments Parse(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith(Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var name = args[i][Prefix.Length..];
            var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith(Prefix, StringComparison.Ordinal);
            options[name] = hasValue ? args[++i] : SwitchValue;
        }

        return new CommandLineArguments(options);
    }

    public static IReadOnlyList<string> Split(string list) => list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public bool Has(string name) => _options.ContainsKey(name);

    public bool TryGet(string name, [NotNullWhen(true)] out string? value) => _options.TryGetValue(name, out value);

    public string? Get(string name) => _options.GetValueOrDefault(name);

    public string Get(string name, string fallback) => _options.GetValueOrDefault(name, fallback);

    public int Int(string name, int fallback) => TryGet(name, out var value) ? int.Parse(value, CultureInfo.InvariantCulture) : fallback;

    public decimal Decimal(string name, decimal fallback) => TryGet(name, out var value) ? decimal.Parse(value, CultureInfo.InvariantCulture) : fallback;

    public string OutDirectory() => Get(OptionNames.Out, ResultsFolder.Default);
}
