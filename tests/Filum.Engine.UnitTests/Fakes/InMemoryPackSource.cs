using Filum.Engine.Packs;
using Filum.Engine.Packs.Contracts;

namespace Filum.Engine.UnitTests.Fakes;

internal sealed class InMemoryPackSource(PackContent? content) : IPackSource
{
    public PackContent? Read(string location) => content;
}
