using Filum.Engine.DependencyInjection;

namespace Filum.Embeddings;

/// <summary>Gives an engine the local embedding model, so it searches what was said by meaning too.</summary>
public static class E5EngineBuilderExtensions
{
    /// <summary>
    /// Searches by meaning with the model in <paramref name="folder"/> (which must hold its files: see
    /// <see cref="ModelFiles.Present"/>), loaded once and disposed with the container; consolidation passes are gated
    /// with the <see cref="KeepGate"/> that ships with this library, when this build has one.
    /// </summary>
    public static FilumEngineBuilder UseE5Embeddings(this FilumEngineBuilder engine, string folder)
    {
        engine.UseEmbedder(_ => new E5Embedder(folder));
        return KeepGate.Shipped() is { } gate ? engine.UseEventGate(gate) : engine;
    }
}
