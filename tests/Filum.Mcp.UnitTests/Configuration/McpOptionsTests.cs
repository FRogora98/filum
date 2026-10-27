using Filum.Mcp.Configuration;
using Microsoft.Extensions.Configuration;

namespace Filum.Mcp.UnitTests.Configuration;

public sealed class McpOptionsTests
{
    [Fact]
    public void Bind_WhenTheVariablesAreSet_ShouldReadEachOne()
    {
        var options = Bind(new()
        {
            ["FILUM_HOME"] = "/memory",
            ["FILUM_PACK"] = "/pack",
            ["FILUM_EMBEDDINGS"] = "on",
            ["FILUM_MODELS"] = "/models"
        });

        Assert.Equal(("/memory", "/pack", "/models"), (options.Home, options.Pack, options.Models));
        Assert.True(options.HasPack);
        Assert.True(options.SearchByMeaning);
        Assert.Equal(Path.Combine("/models", "multilingual-e5-small"), options.ModelFolder);
    }

    [Fact]
    public void ApplyDefaults_WhenNothingIsSet_ShouldUseTheFilumFolderOfTheUser()
    {
        var options = Bind([]);

        options.ApplyDefaults();

        var home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".filum");
        Assert.Equal((home, Path.Combine(home, "models")), (options.Home, options.Models));
        Assert.False(options.HasPack);
        Assert.True(options.SearchByMeaning);
    }

    [Fact]
    public void ApplyDefaults_WhenTheFoldersAreSet_ShouldKeepThem()
    {
        var options = Bind(new() { ["FILUM_HOME"] = "/memory", ["FILUM_MODELS"] = "/models" });

        options.ApplyDefaults();

        Assert.Equal(("/memory", "/models"), (options.Home, options.Models));
    }

    [Theory]
    [InlineData("off", false)]
    [InlineData("OFF", false)]
    [InlineData("Off", false)]
    [InlineData("on", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public void SearchByMeaning_WhenEmbeddingsIsSet_ShouldBeOffOnlyForOff(string? embeddings, bool expected)
    {
        Assert.Equal(expected, new McpOptions { Embeddings = embeddings }.SearchByMeaning);
    }

    [Theory]
    [InlineData("on", true)]
    [InlineData("ON", true)]
    [InlineData("off", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Reranks_WhenRerankerIsSet_ShouldBeOnOnlyForOn(string? reranker, bool expected)
    {
        var options = Bind(new() { ["FILUM_RERANKER"] = reranker, ["FILUM_MODELS"] = "/models" });

        Assert.Equal(expected, options.Reranks);
        Assert.Equal(Path.Combine("/models", "bge-reranker-v2-m3"), options.RerankerFolder);
    }

    [Fact]
    public void HasPack_WhenThePackIsEmpty_ShouldBeFalse()
    {
        Assert.False(Bind(new() { ["FILUM_PACK"] = string.Empty }).HasPack);
    }

    private static McpOptions Bind(Dictionary<string, string?> variables)
    {
        var options = new McpOptions();
        new ConfigurationBuilder().AddInMemoryCollection(variables).Build().Bind(options);
        return options;
    }
}
