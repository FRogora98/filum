using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Consolidation;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;
using Filum.Engine.Tools;
using Filum.Engine.Collections.Compute.Jint;

namespace Filum.Engine.UnitTests.Tools;

public sealed class ToolCatalogTests
{
    private static readonly MemoryActor Agent = MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid());

    private readonly Guid _user = Guid.NewGuid();
    private readonly IMemoryService _memory = MemoryServices.InMemory();

    [Fact]
    public void Order_WhenComparedWithTheToolsOfEveryGroup_ShouldNameEachExactlyOnce()
    {
        // The exception to SVC-04 (docs/EXCEPTIONS.md): the order is written by hand, so this test keeps it complete.
        var session = new MemoryToolSession(_memory, new MemoryOptions(), new JintCodeSandbox(), _user, Agent, [], consolidation: false);
        var registered = ToolCatalog.Groups(session).SelectMany(g => g.Functions()).Select(f => f.Name).ToList();

        Assert.Equal(registered.Count, registered.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(ToolCatalog.Order.GroupBy(n => n, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key));
        Assert.Equal(registered.Order(StringComparer.Ordinal), ToolCatalog.Order.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Catalog_WhenBuilt_ShouldGiveEveryToolANameADescriptionAndTypedParameters()
    {
        var catalog = ToolCatalogs.Of(new MemoryOptions());

        Assert.Equal(30, catalog.Count);
        Assert.Equal(catalog.Count, catalog.Select(t => t.Name).Distinct().Count());
        Assert.All(catalog, t =>
        {
            Assert.Matches("^(memory|collection|skill|events|facts?|proposal|compute)_[a-z_]+$", t.Name);
            Assert.True(t.Description.Length > 20, t.Name);
            Assert.Equal("object", t.JsonSchema.GetProperty("type").GetString());
        });
        // What every model call of a turn carries (spec 031): names, descriptions and schemas, at most 12,700 characters
        // for the whole catalog (spec 041; 200 more for events_search's listing, spec 057; the core profile has its own
        // budget, in filum-mcp's tests).
        var size = catalog.Sum(t => System.Text.Json.JsonSerializer.Serialize(new { t.Name, t.Description, t.JsonSchema }).Length);
        Assert.True(size <= 12_700, $"{size} characters");
        // Spec 040: every tool says what it does to the memory, so a host can show or gate it.
        Assert.All(catalog, t => Assert.NotNull(Filum.Engine.Abstractions.Tools.ToolTraits.Of(t.Name)));
        Assert.Contains(catalog, t => t.Name == "memory_overview");
        Assert.Contains(catalog, t => t.Name == "skill_list");
        Assert.Contains(catalog, t => t.Name == "memory_history");
        Assert.Contains(catalog, t => t.Name == "memory_undo");
    }

    [Fact]
    public async Task MemoryTools_WhenAHostExcludesTools_ShouldLeaveThemOutSoTheyCannotBeCalled()
    {
        var limits = new MemoryOptions { ExcludedTools = ["memory_delete", "collection_remove_rows"] };
        var tools = new MemoryTools(_memory, limits, new JintCodeSandbox(), _user, Agent);

        Assert.Equal(ToolCatalogs.Of(new MemoryOptions()).Select(t => t.Name).Except(["memory_delete", "collection_remove_rows"]), tools.Tools.Select(t => t.Name));
        Assert.Equal(30, new MemoryTools(_memory, new MemoryOptions(), new JintCodeSandbox(), _user, Agent).Tools.Count);
        await Task.CompletedTask;
    }

    [Fact]
    public void MemoryTools_WhenBuiltForAPass_ShouldGiveOnlyItsToolsAndNeverTheProposingOneToATurn()
    {
        Assert.Equal(ConsolidationPrompt.Tools.Order(), Pass().Tools.Select(t => t.Name).Order());
        Assert.DoesNotContain(ToolCatalogs.Of(new MemoryOptions()), t => t.Name == ConsolidationPrompt.ProposeTool);
    }

    private MemoryTools Pass() => new(_memory, new MemoryOptions(), new JintCodeSandbox(), _user, MemoryActor.Consolidation, consolidation: true);
}
