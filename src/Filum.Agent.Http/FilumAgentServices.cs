using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Filum.Agent.Http;

/// <summary>What <see cref="FilumAgentServices.AddFilumAgent{TContext}"/> read from configuration, for the host's start-up log.</summary>
public sealed record FilumAgentSetup(ModelCatalog Models, string ConfiguredModel, Pack? Pack);

/// <summary>
/// The turn's services from configuration, on the host's own context. The host registers <typeparamref name="TContext"/>
/// (with <see cref="FilumModel"/> applied, as a scoped factory) before calling this, and keeps its migrations.
/// </summary>
public static class FilumAgentServices
{
    public static FilumAgentSetup AddFilumAgent<TContext>(this IServiceCollection services, IConfiguration configuration)
        where TContext : DbContext
    {
        services.Configure<OpenAIOptions>(configuration.GetSection(OpenAIOptions.SectionName));
        var openAIOptions = configuration.GetSection(OpenAIOptions.SectionName).Get<OpenAIOptions>() ?? new OpenAIOptions();
        var providers = ProviderConfiguration.From(configuration);
        var models = new ModelCatalog(
            configuration.GetSection(ModelCatalog.SectionName).Get<List<ModelDefinition>>() ?? [],
            openAIOptions.Model,
            providers.Where(p => p.Value.IsConfigured).Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase));
        services.AddSingleton(models);
        if (models.AnyAvailable)
        {
            services.AddSingleton<IChatClientProvider>(new ProviderChatClientProvider(models, providers));
        }

        services.Configure<UsageOptions>(configuration.GetSection(UsageOptions.SectionName));
        services.AddScoped<UsageService>();
        services.AddScoped<ConversationService>();

        services.Configure<MemoryOptions>(configuration.GetSection(MemoryOptions.SectionName));
        // A package (spec 016) makes the engine vertical with data; one that is set but cannot be used stops the host here.
        var pack = configuration["Memory:PackPath"] is { Length: > 0 } packPath
            ? Pack.Load(packPath, configuration.GetSection(MemoryOptions.SectionName).Get<MemoryOptions>() ?? new MemoryOptions())
            : null;
        if (pack is not null)
        {
            services.AddSingleton(pack);
        }

        // The turn works on the host's context: the libraries never own one.
        services.AddScoped<DbContext>(provider => provider.GetRequiredService<TContext>());
        services.AddScoped<IFilumDb, FilumDb<TContext>>();
        services.AddScoped<IMemoryStore, PostgresMemoryStore>();
        services.AddScoped<MemoryService>();

        services.Configure<AgentOptions>(configuration.GetSection(AgentOptions.SectionName));
        services.Configure<ReliabilityOptions>(configuration.GetSection(ReliabilityOptions.SectionName));
        services.AddScoped<ClaimCheck>();
        return new FilumAgentSetup(models, openAIOptions.Model, pack);
    }

    /// <summary>The libraries' database: short-lived contexts from the host's own factory.</summary>
    private sealed class FilumDb<TContext>(IDbContextFactory<TContext> factory) : IFilumDb
        where TContext : DbContext
    {
        public DbContext CreateContext() => factory.CreateDbContext();
    }
}
