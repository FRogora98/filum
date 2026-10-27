using Filum.Engine.Abstractions.Search;

namespace Filum.Engine.UnitTests.Fakes;

// "my" in a text is the first dimension.
internal sealed class WordEmbedder : IEmbedder
{
    public IReadOnlyList<float[]> Embed(IReadOnlyList<string> texts, bool query) =>
        texts.Select(t => t.Contains("my ", StringComparison.OrdinalIgnoreCase) ? new[] { 1f, 0f } : [0f, 1f]).ToList();
}
