using Filum.Engine.Abstractions.Search;

namespace Filum.Engine.UnitTests.Fakes;

// With WordEmbedder, what has "my" in it.
internal sealed class FirstDimensionGate : IEventGate
{
    public bool Keep(float[] vector) => vector[0] > 0.5f;
}
