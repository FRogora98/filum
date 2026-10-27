using Filum.Engine.Collections.Compute.Jint;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;
using Filum.Engine.Tools;
using static Filum.Engine.UnitTests.Builders.ToolCalls;

namespace Filum.Engine.UnitTests.Tools;

public sealed class ComputeToolsTests
{
    private static readonly CancellationToken None = CancellationToken.None;
    private readonly IMemoryService _memory = MemoryServices.InMemory();
    private readonly Guid _user = Guid.NewGuid();

    [Fact]
    public async Task ComputeRun_WhenTheCodeJoinsTwoCollections_ShouldGiveAnExactNumberAndAStepCarryingTheCode()
    {
        await _memory.WriteAsync(_user, MemoryActor.Person, "/people.csv", "name,team\nAda,blue\nBo,red\nCy,blue\n", None);
        await _memory.WriteAsync(_user, MemoryActor.Person, "/hours.csv", "name,hours\nAda,3.5\nBo,2\nCy,4\nAda,1\n", None);
        var tools = Tools();
        const string code = """
            const blue = new Set(data["/people.csv"].filter(p => p.team === "blue").map(p => p.name));
            print("blue: " + [...blue].join(","));
            return data["/hours.csv"].filter(h => blue.has(h.name)).reduce((s, h) => s + h.hours, 0);
            """;

        var text = await Call(tools, "compute_run", new() { ["code"] = code, ["collections"] = new[] { "/people.csv", "/hours.csv" } });

        Assert.StartsWith("Result (computed, not estimated): 8.5", text);
        Assert.Contains("blue: Ada,Cy", text);
        var step = tools.Steps.Single();
        Assert.Equal("Ran code on /people.csv, /hours.csv · 7 rows", step.Description);
        Assert.Contains("blue.has(h.name)", step.Data!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ComputeRun_WhenACollectionIsPrivateOrTheResultLarge_ShouldGiveItOnlyWhenAskedAndRefuseTheResult()
    {
        await _memory.WriteAsync(_user, MemoryActor.Person, "/secret.csv", "n\n1\n2\n", None);
        await _memory.SetSensitivityAsync(_user, MemoryActor.Person, "/secret.csv", MemorySensitivity.Private, None);
        var tools = Tools();

        Assert.Contains("is private", await Call(tools, "compute_run", new() { ["code"] = "return 1;", ["collections"] = new[] { "/secret.csv" } }));
        Assert.StartsWith("Result (computed, not estimated): 3", await Call(tools, "compute_run", new() { ["code"] = "return data['/secret.csv'].reduce((s, r) => s + r.n, 0);", ["collections"] = new[] { "/secret.csv" }, ["includePrivate"] = true }));
        Assert.Contains("return less", await Call(tools, "compute_run", new() { ["code"] = "return 'x'.repeat(10000);" }));
    }

    private MemoryTools Tools() => new(_memory, new MemoryOptions(), new JintCodeSandbox(), _user, MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid()));
}
