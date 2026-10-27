using Filum.Mcp.Configuration;

namespace Filum.Mcp.UnitTests.Configuration;

public sealed class McpOptionsValidatorTests
{
    private readonly McpOptionsValidator _validator = new();

    [Fact]
    public void Validate_WhenTheFoldersArePaths_ShouldSucceed()
    {
        var result = _validator.Validate(null, new McpOptions { Home = "/memory", Models = "/models", Pack = "/pack" });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTheHomeIsBlank_ShouldFailNamingTheVariable()
    {
        var result = _validator.Validate(null, new McpOptions { Home = "  ", Models = "/models" });

        Assert.True(result.Failed);
        Assert.Contains("FILUM_HOME", result.FailureMessage);
    }

    [Fact]
    public void Validate_WhenThePackHasACharacterNoPathHas_ShouldFailNamingTheVariable()
    {
        var result = _validator.Validate(null, new McpOptions { Home = "/memory", Models = "/models", Pack = "/pack\0" });

        Assert.True(result.Failed);
        Assert.Contains("FILUM_PACK", result.FailureMessage);
    }

    [Fact]
    public void Validate_WhenThereIsNoPack_ShouldNotCheckIt()
    {
        var result = _validator.Validate(null, new McpOptions { Home = "/memory", Models = "/models", Pack = null });

        Assert.True(result.Succeeded);
    }
}
