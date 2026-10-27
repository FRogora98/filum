using Filum.Engine.Abstractions.Search;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Packs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Filum.Engine.DependencyInjection;

/// <summary>
/// The variants of an engine, chosen once in composition: where the memory is stored, whether it searches by meaning,
/// and the package it starts from. Returned by <see cref="FilumEngineServiceCollectionExtensions.AddFilumEngine"/>.
/// </summary>
public sealed class FilumEngineBuilder
{
    internal FilumEngineBuilder(IServiceCollection services) => Services = services;

    /// <summary>The service collection the engine is registered in, for the storage and embedding packages to extend.</summary>
    public IServiceCollection Services { get; }

    /// <summary>Stores the memory in <typeparamref name="TStore"/>, a singleton built by the container.</summary>
    public FilumEngineBuilder UseStore<TStore>()
        where TStore : class, IMemoryStore
    {
        Services.Replace(ServiceDescriptor.Singleton<IMemoryStore, TStore>());
        return this;
    }

    /// <summary>Stores the memory in the store <paramref name="factory"/> builds once, from the container.</summary>
    public FilumEngineBuilder UseStore(Func<IServiceProvider, IMemoryStore> factory)
    {
        Services.Replace(ServiceDescriptor.Singleton(factory));
        return this;
    }

    /// <summary>Stores the memory in <paramref name="store"/>, which the caller owns and disposes.</summary>
    public FilumEngineBuilder UseStore(IMemoryStore store)
    {
        Services.Replace(ServiceDescriptor.Singleton(store));
        return this;
    }

    /// <summary>Searches what was said by meaning too, with <typeparamref name="TEmbedder"/>; without one, by words only.</summary>
    public FilumEngineBuilder UseEmbedder<TEmbedder>()
        where TEmbedder : class, IEmbedder
    {
        Services.Replace(ServiceDescriptor.Singleton<IEmbedder, TEmbedder>());
        return this;
    }

    /// <summary>Searches what was said by meaning too, with the embedder <paramref name="factory"/> builds once.</summary>
    public FilumEngineBuilder UseEmbedder(Func<IServiceProvider, IEmbedder> factory)
    {
        Services.Replace(ServiceDescriptor.Singleton(factory));
        return this;
    }

    /// <summary>Searches what was said by meaning too, with <paramref name="embedder"/>.</summary>
    public FilumEngineBuilder UseEmbedder(IEmbedder embedder)
    {
        Services.Replace(ServiceDescriptor.Singleton(embedder));
        return this;
    }

    /// <summary>Orders again the best matches of a search of what was said with <paramref name="reranker"/> (spec 042).</summary>
    public FilumEngineBuilder UseReranker(IReranker reranker)
    {
        Services.Replace(ServiceDescriptor.Singleton(reranker));
        return this;
    }

    /// <summary>
    /// Hands a consolidation pass only the events <paramref name="gate"/> keeps. It needs an embedder too; without one,
    /// a pass reads every pending event.
    /// </summary>
    public FilumEngineBuilder UseEventGate(IEventGate gate)
    {
        Services.Replace(ServiceDescriptor.Singleton(gate));
        return this;
    }

    /// <summary>Starts every new memory from <paramref name="pack"/>, and adds its prompt to the instructions.</summary>
    public FilumEngineBuilder UsePack(Pack pack)
    {
        Services.Replace(ServiceDescriptor.Singleton(new EnginePack(pack)));
        return this;
    }
}
