using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Testing.Builders;
using Filum.Engine.Tools;
using Microsoft.Extensions.AI;
using Filum.Engine.Collections.Compute.Jint;

namespace Filum.Engine.UnitTests.Tools;

public sealed class CollectionToolsTests
{
    [Fact]
    public async Task CollectionTrend_WhenAPeriodStandsOut_ShouldSayItsNumbersAreEstimatesAndNameIt()
    {
        var memory = MemoryServices.InMemory();
        var user = Guid.NewGuid();
        var tools = new MemoryTools(memory, new MemoryOptions(), new JintCodeSandbox(), user, MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid()));
        double[] values = [100, 103, 98, 101, 99, 102, 400, 100, 98, 103, 101, 99];
        await memory.WriteAsync(user, MemoryActor.Person, "/log.csv", "date,amount\n" + string.Concat(values.Select((v, i) => $"{new DateOnly(2025, 1, 15).AddMonths(i):yyyy-MM-dd},{v}\n")), CancellationToken.None);

        var function = tools.Tools.OfType<AIFunction>().Single(t => t.Name == "collection_trend");
        var text = (await function.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["path"] = "/log.csv", ["dateColumn"] = "date", ["column"] = "amount" }), CancellationToken.None))!.ToString()!;

        Assert.Contains("Forecast (estimates", text);
        Assert.Contains("- 2025-07: 400, where the trend expected about", text);
        Assert.Equal("Computed the trend of the sum of amount by month on /log.csv · 12 months", tools.Steps.Single().Description);

        await memory.WriteAsync(user, MemoryActor.Person, "/other.csv", "date,amount\n15/01/2025,3\n", CancellationToken.None);
        var none = (await function.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["path"] = "/other.csv", ["dateColumn"] = "date", ["column"] = "amount" }), CancellationToken.None))!.ToString()!;
        Assert.Contains("written as yyyy-MM-dd", none);
    }
}
