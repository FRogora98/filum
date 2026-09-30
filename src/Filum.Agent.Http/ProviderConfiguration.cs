using Microsoft.Extensions.Configuration;

namespace Filum.Agent.Http;

/// <summary>How a host reads the model providers from its configuration; the libraries only take the result.</summary>
public static class ProviderConfiguration
{
    /// <summary>
    /// The providers in configuration, by name (case-insensitive). The old <c>OpenAI:ApiKey</c> still counts as the
    /// key of the <c>openai</c> provider.
    /// </summary>
    public static IReadOnlyDictionary<string, ProviderOptions> From(IConfiguration configuration)
    {
        var providers = new Dictionary<string, ProviderOptions>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in configuration.GetSection(ProviderOptions.SectionName).GetChildren())
        {
            providers[section.Key] = section.Get<ProviderOptions>() ?? new ProviderOptions();
        }

        var legacyKey = configuration[$"{OpenAIOptions.SectionName}:ApiKey"];
        if (!string.IsNullOrWhiteSpace(legacyKey))
        {
            var openAI = providers.TryGetValue(ProviderOptions.OpenAI, out var existing) ? existing : providers[ProviderOptions.OpenAI] = new ProviderOptions();
            if (!openAI.IsConfigured)
            {
                openAI.ApiKey = legacyKey;
            }
        }

        return providers;
    }
}
