using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using ChatClient = OpenAI.Chat.ChatClient;

namespace Filum.Evals.LongMemEval.Systems;

internal sealed record DirectModel(string Id, IChatClient Client, decimal InputPricePerMillionUsd, decimal OutputPricePerMillionUsd)
{
    public decimal Cost(UsageDetails? usage) => new ModelPrice(InputPricePerMillionUsd, OutputPricePerMillionUsd).CostUsd(usage);

    // Through its provider's OpenAI-compatible API, with the key in <PROVIDER>_API_KEY.
    public static DirectModel Load(ModelsFile file, string id, DotEnvSecrets secrets)
    {
        var model = file.Find(id) ?? throw new InvalidOperationException($"The model '{id}' is not in {file.FilePath}.");
        var provider = model.TryGetProperty(EvalsConstants.ModelsFileFields.Provider, out var p) && p.GetString() is { } named ? named : ModelsFile.DefaultProvider;
        var providerModel = model.TryGetProperty(EvalsConstants.ModelsFileFields.ProviderModel, out var pm) && pm.GetString() is { Length: > 0 } name ? name : id;
        var baseUrl = file.BaseUrl(provider);
        var keyName = provider.ToUpperInvariant() + EvalsConstants.ModelsFileFields.ApiKeySuffix;
        var key = secrets.Find(keyName) ?? throw new InvalidOperationException($"{keyName} is needed for {id}.");
        var options = string.IsNullOrWhiteSpace(baseUrl) ? new OpenAIClientOptions() : new OpenAIClientOptions { Endpoint = new Uri(baseUrl) };
        var client = new ChatClient(providerModel, new ApiKeyCredential(key), options).AsIChatClient();
        var price = ModelsFile.PriceOf(model);
        return new DirectModel(id, client, price.InputPerMillionUsd, price.OutputPerMillionUsd);
    }
}
