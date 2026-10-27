using Filum.Engine.Abstractions.Options;
using Filum.Engine.Consolidation;
using Filum.Engine.DependencyInjection;
using Filum.Engine.Events;
using Filum.Engine.Memory.Contracts;
using Filum.Engine.Memory;
using Filum.Engine.Storage.InMemory;
using Filum.Engine.Tools;
using Filum.Engine.UnitTests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Filum.Engine.UnitTests.DependencyInjection;

public sealed class FilumEngineServiceCollectionExtensionsTests
{
    [Fact]
    public void AddFilumEngine_WhenAStoreIsChosen_ShouldGiveTheMemoryAndTheToolsWithTheConfiguredLimits()
    {
        using var provider = Provider(engine => engine.UseInMemoryStore(), o => o.MaxToolCallsPerTurn = 3);

        Assert.NotNull(provider.GetRequiredService<IMemoryService>());
        Assert.Equal(30, provider.GetRequiredService<IMemoryToolsFactory>().Catalog().Count);
        Assert.Equal(3, provider.GetRequiredService<IOptions<MemoryOptions>>().Value.MaxToolCallsPerTurn);
    }

    [Fact]
    public void AddFilumEngine_WhenARoleOfTheMemoryIsAsked_ShouldGiveTheOneMemoryService()
    {
        using var provider = Provider(engine => engine.UseInMemoryStore());
        var memory = provider.GetRequiredService<IMemoryService>();

        Type[] roles =
        [
            typeof(IMemoryCore), typeof(IMemoryReader), typeof(IMemoryWriter), typeof(IMemorySkills), typeof(IMemoryHistory),
            typeof(IMemoryEvents), typeof(IMemoryFacts), typeof(IMemoryForgetting), typeof(IMemoryImports), typeof(IMemoryConsolidation)
        ];
        Assert.All(roles, role => Assert.Same(memory, provider.GetRequiredService(role)));
    }

    [Fact]
    public void AddFilumEngine_WhenTheOptionsAreNotUsable_ShouldRefuseThemWhenRead()
    {
        using var provider = Provider(engine => engine.UseInMemoryStore(), o => o.ExcludedTools = ["no_such_tool"]);

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<MemoryOptions>>().Value);
    }

    [Fact]
    public void AddFilumEngine_WhenThereIsNoEmbedder_ShouldGiveTheServicesNullObjects()
    {
        using var provider = Provider(engine => engine.UseInMemoryStore().UseEventGate(new FirstDimensionGate()));

        Assert.IsType<NullEventVectors>(provider.GetRequiredService<IEventVectors>());
        Assert.IsType<NullPendingEventFilter>(provider.GetRequiredService<IPendingEventFilter>());
    }

    [Fact]
    public void AddFilumEngine_WhenAnEmbedderAndAGateAreChosen_ShouldSearchByMeaningAndGateThePasses()
    {
        using var provider = Provider(engine => engine.UseInMemoryStore().UseEmbedder(new WordEmbedder()).UseEventGate(new FirstDimensionGate()));

        Assert.IsType<EventVectorService>(provider.GetRequiredService<IEventVectors>());
        Assert.IsType<GatedPendingEventFilter>(provider.GetRequiredService<IPendingEventFilter>());
    }

    [Fact]
    public void Create_WhenTheOptionsAreNotUsable_ShouldThrowBeforeAnythingIsBuilt() =>
        Assert.Throws<OptionsValidationException>(() => FilumEngine.Create(new InMemoryMemoryStore(), new MemoryOptions { MaxFileBytes = 0 }));

    private static ServiceProvider Provider(Action<FilumEngineBuilder> choose, Action<MemoryOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        choose(services.AddFilumEngine(configure));
        return services.BuildServiceProvider();
    }
}
