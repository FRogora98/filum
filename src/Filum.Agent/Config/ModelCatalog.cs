namespace Filum.Agent;

public sealed class ModelDefinition
{
    /// <summary>The stable name the app, the messages and the usage records use.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The <see cref="ProviderOptions"/> entry that serves this model.</summary>
    public string Provider { get; set; } = ProviderOptions.OpenAI;

    /// <summary>The model's id at its provider; empty when it is the same as <see cref="Id"/>.</summary>
    public string ProviderModel { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal InputPricePerMillionUsd { get; set; }

    public decimal OutputPricePerMillionUsd { get; set; }

    public string ModelAtProvider => string.IsNullOrWhiteSpace(ProviderModel) ? Id : ProviderModel;
}

/// <summary>
/// The models a person can choose from, with their list prices. Configuration, not code: section <c>Models</c>.
/// Only the models whose provider has a key are offered; with no key at all every model is listed, and messages
/// are answered with 503 as before.
/// </summary>
public sealed class ModelCatalog
{
    public const string SectionName = "Models";

    public ModelCatalog(IReadOnlyList<ModelDefinition> models, string defaultModelId, IReadOnlySet<string>? availableProviders = null)
    {
        if (models.Count == 0)
        {
            throw new InvalidOperationException("The model catalog is empty: configure at least one model in the 'Models' section.");
        }

        var available = availableProviders is null
            ? models.ToList()
            : models.Where(m => availableProviders.Contains(m.Provider)).ToList();
        AnyAvailable = available.Count > 0;
        Models = AnyAvailable ? available : models;

        var configuredDefault = Find(defaultModelId);
        Default = configuredDefault ?? Models[0];
        DefaultIsConfigured = configuredDefault is not null;
    }

    public IReadOnlyList<ModelDefinition> Models { get; }

    public ModelDefinition Default { get; }

    public bool DefaultIsConfigured { get; }

    /// <summary>At least one model's provider has a key.</summary>
    public bool AnyAvailable { get; }

    public ModelDefinition? Find(string? modelId) =>
        Models.FirstOrDefault(m => string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase));

    public static decimal CostUsd(ModelDefinition model, int inputTokens, int outputTokens) =>
        (inputTokens * model.InputPricePerMillionUsd + outputTokens * model.OutputPricePerMillionUsd) / 1_000_000m;
}
