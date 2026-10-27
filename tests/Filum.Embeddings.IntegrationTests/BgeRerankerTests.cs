using System.Text.Json;
using Filum.Embeddings.IntegrationTests.Fakes;

namespace Filum.Embeddings.IntegrationTests;

// Against a reference made with the model's own tokenizer and ONNX Runtime in Python (Fixtures/bge-reranker-reference.json).
// The reranker (about 575 MB) is not in the repository: the test runs where it is, in FILUM_RERANKER_DIR or
// ~/.filum/models/bge-reranker-v2-m3, and says so where it is not.
public sealed class BgeRerankerTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private sealed record Reference(string Query, string Text, long[] Ids, float Score);

    [Fact]
    public void Score_WhenTheModelIsPresent_ShouldMatchTheReferenceTokensAndScores()
    {
        var folder = Environment.GetEnvironmentVariable("FILUM_RERANKER_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".filum", "models", RerankerFiles.Name);
        if (!RerankerFiles.Present(folder))
        {
            Console.WriteLine($"BgeRerankerTests: no reranker in {folder}; set FILUM_RERANKER_DIR to run it.");
            return;
        }

        var references = JsonSerializer.Deserialize<List<Reference>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "bge-reranker-reference.json")), Web)!;
        using var model = new BgeReranker(folder);
        foreach (var reference in references)
        {
            Assert.Equal(reference.Ids, model.PairIds(reference.Query, reference.Text));
        }

        // Scored together, as a search does: padding must not change a pair's score.
        var scores = model.Score(references[0].Query, [.. references.Take(2).Select(r => r.Text)])!;
        Assert.All(scores.Zip(references.Take(2)), x => Assert.Equal(x.Second.Score, x.First, 0.05f));
        Assert.True(scores[0] > scores[1]);
    }

    [Fact]
    public async Task EnsureAsync_WhenTheDownloadDoesNotMatchItsHash_ShouldLeaveNoFile()
    {
        var folder = Path.Combine(Path.GetTempPath(), "filum-tests", Guid.NewGuid().ToString("N"));
        try
        {
            using var http = new HttpClient(new FixedResponseHandler("not the model"u8.ToArray()));

            Assert.False(await RerankerFiles.EnsureAsync(folder, http, CancellationToken.None));

            Assert.Empty(Directory.GetFiles(folder));
            Assert.False(RerankerFiles.Present(folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Files_WhenListed_ShouldEachHaveAFixedRevisionAndAHash()
    {
        Assert.All(RerankerFiles.Files.Values, f =>
        {
            Assert.Matches("/resolve/[0-9a-f]{40}/", f.Url);
            Assert.Matches("^[0-9a-f]{64}$", f.Sha256);
        });
        Assert.Equal(ModelFiles.Files[ModelFiles.Tokenizer].Sha256, RerankerFiles.Files[RerankerFiles.Tokenizer].Sha256);
    }
}
