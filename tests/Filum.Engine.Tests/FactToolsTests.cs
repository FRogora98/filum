using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Filum.Engine.Tests;

/// <summary>The facts' tools (spec 030): recorded with the turn's message as their source, changed only by fact_record.</summary>
public sealed class FactToolsTests
{
    private static readonly CancellationToken None = CancellationToken.None;
    private readonly MemoryService _memory = new(new InMemoryMemoryStore(), Options.Create(new MemoryOptions()), NullLogger<MemoryService>.Instance);
    private readonly Guid _user = Guid.NewGuid();

    [Fact]
    public async Task A_fact_recorded_in_a_turn_cites_its_message_and_the_collection_tools_leave_the_facts_alone()
    {
        var tools = new MemoryTools(_memory, new MemoryOptions(), _user, MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid()), [42]);

        var recorded = await Call(tools, "fact_record", new() { ["subject"] = "the person", ["attribute"] = "plants", ["value"] = "3" });
        var refused = await Call(tools, "collection_add_rows", new() { ["path"] = Facts.Path, ["rows"] = new[] { new Dictionary<string, object?> { ["subject"] = "x" } } });
        var current = await Call(tools, "facts_current", new());

        Assert.StartsWith("Recorded: the person · plants is 3", recorded);
        Assert.Contains("use fact_record", refused);
        Assert.Contains($"- the person · plants: 3 (since {Facts.Day(DateTimeOffset.UtcNow)}; from event 42)", current);
        Assert.Equal(["Recorded the person · plants: 3", "Could not add rows to /facts.csv", "Read the current facts"], tools.Steps.Select(s => s.Description));
    }

    private static async Task<string> Call(MemoryTools tools, string name, Dictionary<string, object?> arguments)
    {
        var function = tools.Tools.OfType<AIFunction>().Single(t => t.Name == name);
        var result = await function.InvokeAsync(new AIFunctionArguments(arguments), None);
        return result?.ToString()?.Trim('"') ?? string.Empty;
    }
}
