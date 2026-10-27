using System.Diagnostics.CodeAnalysis;
using Filum.Embeddings;
using Microsoft.Extensions.Configuration;

namespace Filum.Mcp.Configuration;

/// <summary>
/// Where the server keeps the person's memory and its search model, and the package it starts from: read from the
/// <c>FILUM_*</c> environment variables, which every MCP host can set.
/// </summary>
internal sealed class McpOptions
{
    private const string HomeFolderName = ".filum";
    private const string ModelsFolderName = "models";

    /// <summary>The memory folder: <c>FILUM_HOME</c>, or <c>~/.filum</c>.</summary>
    [ConfigurationKeyName(McpConstants.Variables.Home)]
    public string Home { get; set; } = string.Empty;

    /// <summary>The package's folder (<c>FILUM_PACK</c>); none when empty.</summary>
    [ConfigurationKeyName(McpConstants.Variables.Pack)]
    public string? Pack { get; set; }

    /// <summary><c>FILUM_EMBEDDINGS</c>: <c>off</c> keeps the search model off; anything else, or nothing, turns it on.</summary>
    [ConfigurationKeyName(McpConstants.Variables.Embeddings)]
    public string? Embeddings { get; set; }

    /// <summary>Where the search models live: <c>FILUM_MODELS</c>, or <c>~/.filum/models</c>.</summary>
    [ConfigurationKeyName(McpConstants.Variables.Models)]
    public string Models { get; set; } = string.Empty;

    /// <summary><c>FILUM_TOOLS</c>: <c>core</c> (the default, 14 tools) or <c>full</c> (every tool).</summary>
    [ConfigurationKeyName(McpConstants.Variables.Tools)]
    public string? Tools { get; set; }

    /// <summary>
    /// <c>FILUM_RERANKER</c>: <c>on</c> downloads the local reranker (about 575 MB) and orders the best matches of a search
    /// with it (spec 042); anything else, or nothing, keeps it off and downloads nothing.
    /// </summary>
    [ConfigurationKeyName(McpConstants.Variables.Reranker)]
    public string? Reranker { get; set; }

    /// <summary>Whether a search's best matches are ordered again by the local reranker.</summary>
    public bool Reranks => string.Equals(Reranker, McpConstants.Variables.On, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether every tool is offered; otherwise the core profile.</summary>
    public bool AllTools => string.Equals(Tools, McpConstants.Variables.FullProfile, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether a tool is offered in the chosen profile.</summary>
    public bool Offers(string tool) => AllTools || McpConstants.Tools.Core.Contains(tool);

    [MemberNotNullWhen(true, nameof(Pack))]
    public bool HasPack => !string.IsNullOrEmpty(Pack);

    public bool SearchByMeaning => !string.Equals(Embeddings, McpConstants.Variables.Off, StringComparison.OrdinalIgnoreCase);

    /// <summary>The folder of the one search model the server uses.</summary>
    public string ModelFolder => Path.Combine(Models, ModelFiles.Name);

    /// <summary>The folder of the reranker, when it is on.</summary>
    public string RerankerFolder => Path.Combine(Models, RerankerFiles.Name);

    /// <summary>
    /// The settings read from the environment alone, with their defaults: what <c>filum-mcp log</c> uses, as a host's hook
    /// that starts no server.
    /// </summary>
    public static McpOptions FromEnvironment()
    {
        var options = new ConfigurationBuilder().AddEnvironmentVariables().Build().Get<McpOptions>() ?? new McpOptions();
        options.ApplyDefaults();
        return options;
    }

    /// <summary>Fills what was not set with its default, the user's <c>~/.filum</c>.</summary>
    public void ApplyDefaults()
    {
        var home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), HomeFolderName);
        if (string.IsNullOrEmpty(Home))
        {
            Home = home;
        }

        if (string.IsNullOrEmpty(Models))
        {
            Models = Path.Combine(home, ModelsFolderName);
        }
    }
}
