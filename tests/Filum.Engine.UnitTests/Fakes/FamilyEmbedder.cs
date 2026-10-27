using Filum.Engine.Abstractions.Search;

namespace Filum.Engine.UnitTests.Fakes;

internal sealed class FamilyEmbedder : IEmbedder
{
    private static readonly string[][] Families = [["fern", "plants", "plant", "balcony"], ["train", "late", "travel"]];

    public IReadOnlyList<float[]> Embed(IReadOnlyList<string> texts, bool query) =>
        texts.Select(t =>
        {
            var v = Families.Select(f => f.Any(w => t.Contains(w, StringComparison.OrdinalIgnoreCase)) ? 1f : 0f).Append(0.1f).ToArray();
            var norm = MathF.Sqrt(v.Sum(x => x * x));
            return v.Select(x => x / norm).ToArray();
        }).ToList();
}
