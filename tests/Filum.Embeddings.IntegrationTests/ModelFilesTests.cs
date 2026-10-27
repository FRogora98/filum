using Filum.Embeddings.IntegrationTests.Fakes;

namespace Filum.Embeddings.IntegrationTests;

public sealed class ModelFilesTests
{
    [Fact]
    public async Task EnsureAsync_WhenTheDownloadDoesNotMatchItsHash_ShouldLeaveNoFile()
    {
        var folder = Path.Combine(Path.GetTempPath(), "filum-tests", Guid.NewGuid().ToString("N"));
        try
        {
            using var http = new HttpClient(new FixedResponseHandler("not the model"u8.ToArray()));

            Assert.False(await ModelFiles.EnsureAsync(folder, http, CancellationToken.None));

            Assert.Empty(Directory.GetFiles(folder));
            Assert.False(ModelFiles.Present(folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
