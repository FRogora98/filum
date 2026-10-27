using System.Text;

namespace Filum.Engine.Storage.LocalFolder;

// A later line for the same event wins.
internal sealed class VectorFile(FolderLayout layout)
{
    private Dictionary<long, float[]>? _vectors;

    public Dictionary<long, float[]> All() =>
        _vectors ??= JsonLines.Read<VectorLine>(layout.VectorsFile).GroupBy(l => l.Id).ToDictionary(g => g.Key, g => VectorText.FromText(g.Last().V));

    // Appended as one write.
    public void Save(IEnumerable<(long EventId, float[] Vector)> vectors)
    {
        var lines = new StringBuilder();
        foreach (var (id, vector) in vectors)
        {
            All()[id] = vector;
            lines.Append(Line(id, vector)).Append('\n');
        }

        if (lines.Length > 0)
        {
            JsonLines.Append(layout.VectorsFile, lines.ToString().TrimEnd('\n'));
        }
    }

    // Every vector goes; the next search makes them again.
    public void Clear()
    {
        _vectors = [];
        JsonLines.Rewrite<VectorLine>(layout, layout.VectorsFile, []);
    }

    public void Remove(IReadOnlyCollection<long> eventIds)
    {
        foreach (var id in eventIds)
        {
            All().Remove(id);
        }

        JsonLines.Rewrite<VectorLine>(layout, layout.VectorsFile, All().Select(v => Line(v.Key, v.Value)));
    }

    private static string Line(long id, float[] vector) => JsonLines.Serialize(new VectorLine(id, VectorText.ToText(vector)));

    private sealed record VectorLine(long Id, string V);
}
