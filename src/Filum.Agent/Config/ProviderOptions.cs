namespace Filum.Agent;

/// <summary>
/// An OpenAI-compatible API that serves catalog models. Configuration, not code: section <c>Providers</c>, one
/// entry per name. The key comes from the host's environment or secrets (<c>Providers__&lt;name&gt;__ApiKey</c>) and
/// is never logged.
/// </summary>
public sealed class ProviderOptions
{
    public const string SectionName = "Providers";
    public const string OpenAI = "openai";

    /// <summary>Empty for OpenAI itself.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Ask the provider not to train on or keep the requests (OpenRouter's <c>data_collection: deny</c>).</summary>
    public bool DenyDataCollection { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
