namespace Filum.Agent;

public sealed class OpenAIOptions
{
    public const string SectionName = "OpenAI";

    /// <summary>Still read as the key of the <c>openai</c> provider; new keys go in <c>Providers:&lt;name&gt;:ApiKey</c>.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gemma-4-31b";

    public int TimeoutSeconds { get; set; } = 120;
}
