using System.Text.Json;

namespace Filum.Embeddings.IntegrationTests;

// Against a reference made with the model's own tokenizer and ONNX Runtime in Python (Fixtures/e5-reference.json). The
// model (118 MB) is not in the repository: the test runs where it is, in FILUM_MODELS_DIR or
// ~/.filum/models/multilingual-e5-small, and says so where it is not.
public sealed class E5EmbedderTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private sealed record Reference(string Text, bool Query, long[] Ids, float[] Vector);

    [Fact]
    public void Embed_WhenTheModelIsPresent_ShouldMatchTheReferenceTokensAndVectors()
    {
        var folder = Environment.GetEnvironmentVariable("FILUM_MODELS_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".filum", "models", ModelFiles.Name);
        if (!ModelFiles.Present(folder))
        {
            Console.WriteLine($"E5EmbedderTests: no model in {folder}; set FILUM_MODELS_DIR to run it.");
            return;
        }

        var references = JsonSerializer.Deserialize<List<Reference>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "e5-reference.json")), Web)!;
        using var model = new E5Embedder(folder);
        foreach (var reference in references)
        {
            Assert.Equal(reference.Ids, model.Ids((reference.Query ? "query: " : "passage: ") + reference.Text));
            var vector = model.Embed([reference.Text], reference.Query)[0];
            Assert.True(vector.Zip(reference.Vector, (a, b) => a * b).Sum() > 0.999f, reference.Text);
        }
    }
}
