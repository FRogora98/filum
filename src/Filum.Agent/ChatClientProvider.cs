using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Nodes;

namespace Filum.Agent;

public interface IChatClientProvider
{
    IChatClient Get(string modelId);
}

/// <summary>
/// One client per catalog model, created on first use, pointed at the model's provider: its endpoint, its key and
/// the model's id there. It caches configuration only: no conversation state.
/// </summary>
public sealed class ProviderChatClientProvider(
    ModelCatalog catalog,
    IReadOnlyDictionary<string, ProviderOptions> providers,
    Func<HttpMessageHandler>? transportForTests = null) : IChatClientProvider
{
    private readonly ConcurrentDictionary<string, IChatClient> _clients = new(StringComparer.OrdinalIgnoreCase);

    public IChatClient Get(string modelId) => _clients.GetOrAdd(modelId, Create);

    private IChatClient Create(string modelId)
    {
        var model = catalog.Find(modelId) ?? throw new InvalidOperationException($"The model '{modelId}' is not in the catalog.");
        if (!providers.TryGetValue(model.Provider, out var provider) || !provider.IsConfigured)
        {
            throw new InvalidOperationException($"The provider '{model.Provider}' of model '{modelId}' has no key.");
        }

        HttpMessageHandler handler = transportForTests?.Invoke() ?? new HttpClientHandler();
        if (provider.DenyDataCollection)
        {
            handler = new DenyDataCollectionHandler { InnerHandler = handler };
        }

        var options = new OpenAIClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(handler)) };
        if (!string.IsNullOrWhiteSpace(provider.BaseUrl))
        {
            options.Endpoint = new Uri(provider.BaseUrl);
        }

        return new ChatClient(model.ModelAtProvider, new ApiKeyCredential(provider.ApiKey), options).AsIChatClient();
    }
}

/// <summary>
/// Adds <c>"provider": {"data_collection": "deny"}</c> to every chat request, so a router that forwards to third
/// parties only uses those that do not train on or keep the data. The OpenAI SDK has no field for it.
/// </summary>
public sealed class DenyDataCollectionHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Post && request.Content is not null && request.RequestUri?.AbsolutePath.EndsWith("/chat/completions", StringComparison.Ordinal) == true)
        {
            var body = JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
            if (body is JsonObject json)
            {
                var provider = json["provider"] as JsonObject ?? new JsonObject();
                provider["data_collection"] = "deny";
                json["provider"] = provider;
                request.Content = new StringContent(json.ToJsonString(), Encoding.UTF8, "application/json");
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
