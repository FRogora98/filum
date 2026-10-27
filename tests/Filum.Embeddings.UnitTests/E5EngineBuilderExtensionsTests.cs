using Filum.Engine.Abstractions.Search;
using Filum.Engine.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Filum.Embeddings.UnitTests;

public sealed class E5EngineBuilderExtensionsTests
{
    [Fact]
    public void UseE5Embeddings_WhenCalled_ShouldRegisterTheModelLoadedOnceAndTheShippedGate()
    {
        var services = new ServiceCollection();

        services.AddFilumEngine().UseE5Embeddings("models");

        var embedder = Assert.Single(services, s => s.ServiceType == typeof(IEmbedder));
        Assert.Equal(ServiceLifetime.Singleton, embedder.Lifetime);
        Assert.NotNull(embedder.ImplementationFactory);
        Assert.IsType<KeepGate>(Assert.Single(services, s => s.ServiceType == typeof(IEventGate)).ImplementationInstance);
    }
}
