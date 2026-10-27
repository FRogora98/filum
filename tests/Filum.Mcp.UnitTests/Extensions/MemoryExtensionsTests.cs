using Filum.Mcp.Extensions;
using Filum.Mcp.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Filum.Mcp.UnitTests.Extensions;

public sealed class MemoryExtensionsTests
{
    [Fact]
    public void AddFilumMcpMemory_WhenTheRerankerIsNotAskedFor_ShouldLoadNoReranker()
    {
        var loaders = HostedServices(new() { ["FILUM_EMBEDDINGS"] = "off" });

        Assert.DoesNotContain(typeof(RerankerLoader), loaders);
        Assert.DoesNotContain(typeof(ModelLoader), loaders);
    }

    [Fact]
    public void AddFilumMcpMemory_WhenTheRerankerIsOn_ShouldLoadItInTheBackground()
    {
        var loaders = HostedServices(new() { ["FILUM_RERANKER"] = "on" });

        Assert.Contains(typeof(RerankerLoader), loaders);
        Assert.Contains(typeof(ModelLoader), loaders);
    }

    private static List<Type?> HostedServices(Dictionary<string, string?> variables) =>
        new ServiceCollection()
            .AddFilumMcpMemory(new ConfigurationBuilder().AddInMemoryCollection(variables).Build())
            .Where(d => d.ServiceType == typeof(IHostedService))
            .Select(d => d.ImplementationType)
            .ToList();
}
