using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.DependencyInjection;
using Filum.Engine.Memory;
using Filum.Engine.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Filum.Engine;

/// <summary>
/// An engine without a host's container, for a console or a script: the same composition as
/// <see cref="FilumEngineServiceCollectionExtensions.AddFilumEngine"/>, in a container of its own.
/// </summary>
public sealed class FilumEngine : IDisposable
{
    private readonly ServiceProvider _provider;

    private FilumEngine(ServiceProvider provider)
    {
        _provider = provider;
        Memory = provider.GetRequiredService<IMemoryService>();
        Tools = provider.GetRequiredService<IMemoryToolsFactory>();
    }

    /// <summary>The person's memory.</summary>
    public IMemoryService Memory { get; }

    /// <summary>The agent's tools, built for each turn.</summary>
    public IMemoryToolsFactory Tools { get; }

    /// <summary>
    /// An engine over <paramref name="store"/>, which the caller owns, with <paramref name="options"/> (the defaults when
    /// null). <paramref name="configure"/> picks the other variants (embedder, gate, package); without
    /// <paramref name="logging"/> nothing is logged. Throws <see cref="OptionsValidationException"/> when the options are
    /// not usable.
    /// </summary>
    public static FilumEngine Create(IMemoryStore store, MemoryOptions? options = null, Action<FilumEngineBuilder>? configure = null, ILoggerFactory? logging = null)
    {
        options ??= new MemoryOptions();
        if (new MemoryOptionsValidator().Validate(Options.DefaultName, options) is { Failed: true } invalid)
        {
            throw new OptionsValidationException(Options.DefaultName, typeof(MemoryOptions), invalid.Failures);
        }

        var services = new ServiceCollection();
        services.AddSingleton(logging ?? NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        var builder = services.AddFilumEngine().UseStore(store);
        services.Replace(ServiceDescriptor.Singleton(Options.Create(options)));
        configure?.Invoke(builder);
        return new FilumEngine(services.BuildServiceProvider());
    }

    /// <summary>Releases the engine's container; the store stays the caller's.</summary>
    public void Dispose() => _provider.Dispose();
}
