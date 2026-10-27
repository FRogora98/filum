using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Search;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Collections;
using Filum.Engine.Consolidation;
using Filum.Engine.Events;
using Filum.Engine.Briefing;
using Filum.Engine.Facts;
using Filum.Engine.Forgetting;
using Filum.Engine.Imports;
using Filum.Engine.Memory.Contracts;
using Filum.Engine.Memory;
using Filum.Engine.Memory.Core;
using Filum.Engine.Memory.Files;
using Filum.Engine.Memory.History;
using Filum.Engine.Packs;
using Filum.Engine.Skills;
using Filum.Engine.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Filum.Engine.Collections.Compute;
using Filum.Engine.Collections.Compute.Jint;

namespace Filum.Engine.DependencyInjection;

/// <summary>The one entry point that registers the engine in a host's container.</summary>
public static class FilumEngineServiceCollectionExtensions
{
    /// <summary>
    /// Registers the engine: <see cref="IMemoryService"/> (and each of its roles, the same instance) and
    /// <see cref="IMemoryToolsFactory"/>, both stateless singletons, with the limits <paramref name="configure"/> sets,
    /// validated when the host starts. The store is chosen on the returned builder; the log comes from the host's
    /// <c>ILoggerFactory</c>, and the clock from a registered <see cref="TimeProvider"/>.
    /// </summary>
    public static FilumEngineBuilder AddFilumEngine(this IServiceCollection services, Action<MemoryOptions>? configure = null)
    {
        var options = services.AddOptions<MemoryOptions>().ValidateOnStart();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MemoryOptions>, MemoryOptionsValidator>());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(new EnginePack(null));
        AddStoreRoles(services);
        AddServices(services);
        services.TryAddSingleton<IMemoryService, MemoryService>();
        AddMemoryRoles(services);
        services.TryAddSingleton<IMemoryToolsFactory, MemoryToolsFactory>();
        return new FilumEngineBuilder(services);
    }

    // Every role is the one memory service instance.
    private static void AddMemoryRoles(IServiceCollection services)
    {
        services.TryAddSingleton<IMemoryCore>(sp => sp.GetRequiredService<IMemoryService>());
        services.TryAddSingleton<IMemoryReader>(sp => sp.GetRequiredService<IMemoryService>());
        services.TryAddSingleton<IMemoryWriter>(sp => sp.GetRequiredService<IMemoryService>());
        services.TryAddSingleton<IMemorySkills>(sp => sp.GetRequiredService<IMemoryService>());
        services.TryAddSingleton<IMemoryHistory>(sp => sp.GetRequiredService<IMemoryService>());
        services.TryAddSingleton<IMemoryEvents>(sp => sp.GetRequiredService<IMemoryService>());
        services.TryAddSingleton<IMemoryFacts>(sp => sp.GetRequiredService<IMemoryService>());
        services.TryAddSingleton<IMemoryForgetting>(sp => sp.GetRequiredService<IMemoryService>());
        services.TryAddSingleton<IMemoryImports>(sp => sp.GetRequiredService<IMemoryService>());
        services.TryAddSingleton<IMemoryConsolidation>(sp => sp.GetRequiredService<IMemoryService>());
    }

    // Every role is the one store the builder chose.
    private static void AddStoreRoles(IServiceCollection services)
    {
        services.TryAddSingleton<IMemoryFileStore>(sp => sp.GetRequiredService<IMemoryStore>());
        services.TryAddSingleton<IMemoryRevisionStore>(sp => sp.GetRequiredService<IMemoryStore>());
        services.TryAddSingleton<IMemoryEventStore>(sp => sp.GetRequiredService<IMemoryStore>());
        services.TryAddSingleton<IMemoryVectorStore>(sp => sp.GetRequiredService<IMemoryStore>());
        services.TryAddSingleton<IConversationDirectory>(sp => sp.GetRequiredService<IMemoryStore>());
    }

    private static void AddServices(IServiceCollection services)
    {
        services.TryAddSingleton<MemoryWriter>();
        services.TryAddSingleton<MemoryFileReader>();
        services.TryAddSingleton<MemoryFileEditor>();
        services.TryAddSingleton<MemoryIndexBuilder>();
        services.TryAddSingleton<CollectionRowService>();
        services.TryAddSingleton<MemoryHistoryService>();
        services.TryAddSingleton<MemoryReverter>();
        services.TryAddSingleton<SkillService>();
        services.TryAddSingleton<CoreBootstrapper>();
        services.TryAddSingleton<EventService>();
        services.TryAddSingleton<FactService>();
        services.TryAddSingleton<FactRecall>();
        services.TryAddSingleton<BriefService>();
        services.TryAddSingleton<ForgetService>();
        services.TryAddSingleton<ImportService>();
        services.TryAddSingleton<ConsolidationService>();
        services.TryAddSingleton<ICodeSandbox, JintCodeSandbox>();

        // The optional embedder and gate are chosen here, once: the services get a null object, never a null.
        services.TryAddSingleton<IEventVectors>(sp => sp.GetService<IEmbedder>() is { } embedder
            ? ActivatorUtilities.CreateInstance<EventVectorService>(sp, embedder)
            : ActivatorUtilities.CreateInstance<NullEventVectors>(sp));
        services.TryAddSingleton<IPendingEventFilter>(sp =>
            sp.GetService<IEventGate>() is { } gate && sp.GetRequiredService<IEventVectors>() is EventVectorService vectors
                ? new GatedPendingEventFilter(vectors, gate)
                : new NullPendingEventFilter());
    }
}
