using Filum.Engine.Abstractions.Search;

namespace Filum.Mcp.UnitTests.Fakes;

// Every text is the same unit vector, with no model file.
internal sealed class WordEmbedder : IEmbedder
{
    public IReadOnlyList<float[]> Embed(IReadOnlyList<string> texts, bool query) => texts.Select(_ => new[] { 1f, 0f }).ToList();
}
