using Filum.Engine.Instructions;

namespace Filum.Mcp.UnitTests.Instructions;

// Spec 040: the platform and filum-mcp tell a model the same fact policy, from one source.
public sealed class InstructionAgreementTests
{
    [Fact]
    public void Instructions_WhenComposed_ShouldCarryTheSameFactPolicyAndReadingAdviceOnBothSurfaces()
    {
        Assert.Contains(InstructionParts.FactPolicy, PlatformInstructions.Text, StringComparison.Ordinal);
        Assert.Contains(InstructionParts.FactPolicy, McpInstructions.Text, StringComparison.Ordinal);
        Assert.Contains(InstructionParts.ReadWhole, PlatformInstructions.Text, StringComparison.Ordinal);
        Assert.Contains(InstructionParts.ReadWhole, McpInstructions.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Instructions_WhenComposed_ShouldNoLongerSendFactsAboutPeopleToTheCoreOnOneSurfaceOnly()
    {
        Assert.DoesNotContain("facts about them and the people and things in their life go in the core", PlatformInstructions.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("every fact the person states about someone or something", McpInstructions.Text, StringComparison.Ordinal);
    }
}
