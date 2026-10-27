using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;
using static Filum.Engine.Testing.Builders.Outcomes;

namespace Filum.Engine.Storage.LocalFolder.IntegrationTests;

// Spec 039: every revision line carries its content's hash, and a history that cannot be rebuilt says so.
public sealed class LocalFolderStoreHistoryIntegrityTests : IDisposable
{
    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly MemoryActor Agent = MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid());

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "filum-tests", Guid.NewGuid().ToString("N"));
    private readonly Guid _person = Guid.NewGuid();

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Filum.Engine.Testing.Builders.TemporaryFolders.Delete(_folder);
        }
    }

    [Fact]
    public async Task WriteAsync_WhenARevisionIsWritten_ShouldCarryTheHashOfItsContent()
    {
        await TwoWritesAsync();

        var lines = Lines();
        Assert.Equal(Sha("# Plans\nFirst\n"), lines[0]["sha"]!.GetValue<string>());
        Assert.Equal(Sha("# Plans\nSecond\n"), lines[1]["sha"]!.GetValue<string>());
        Assert.NotNull(lines[1]["base"]);
    }

    [Fact]
    public async Task GetRevisionAsync_WhenALineOfTheChainWasAltered_ShouldFailNamingThePathAndTheRevision()
    {
        var second = await TwoWritesAsync();
        var lines = Lines();
        lines[0]["content"] = "# Plans\nAltered\n";
        Save(lines);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => Open().GetRevisionAsync(_person, second, None));

        Assert.Contains("/notes/plans.md", error.Message, StringComparison.Ordinal);
        Assert.Contains("damaged", error.Message, StringComparison.Ordinal);
        Assert.Equal("# Plans\nSecond\n", File.ReadAllText(Disk("notes/plans.md")));
    }

    [Fact]
    public async Task GetRevisionAsync_WhenTheBaseOfAChangeIsMissing_ShouldFailInsteadOfRebuildingFromNothing()
    {
        var second = await TwoWritesAsync();
        Save(Lines().Skip(1).ToList());

        var error = await Assert.ThrowsAnyAsync<Exception>(() => Open().GetRevisionAsync(_person, second, None));

        Assert.Contains("/notes/plans.md", error.Message, StringComparison.Ordinal);
        Assert.Contains($"revision {second}", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetRevisionAsync_WhenTheHistoryIsWhole_ShouldReadEveryRevision()
    {
        var second = await TwoWritesAsync();

        Assert.Equal("# Plans\nSecond\n", Ok(await Open().GetRevisionAsync(_person, second, None)).Content);
    }

    // Two agent writes of one file; the second's revision id.
    private async Task<long> TwoWritesAsync()
    {
        var memory = Open();
        Ok(await memory.WriteAsync(_person, Agent, "/notes/plans.md", "# Plans\nFirst\n", None));
        return Ok(await memory.WriteAsync(_person, Agent, "/notes/plans.md", "# Plans\nSecond\n", None)).RevisionId;
    }

    private List<JsonObject> Lines() =>
        File.ReadAllLines(Disk(".filum/revisions.jsonl")).Where(l => l.Length > 0).Select(l => JsonNode.Parse(l)!.AsObject()).ToList();

    private void Save(List<JsonObject> lines) =>
        File.WriteAllText(Disk(".filum/revisions.jsonl"), string.Concat(lines.Select(l => l.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n")));

    private static string Sha(string content) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private IMemoryService Open() => MemoryServices.On(new LocalFolderStore(_folder, _person));

    private string Disk(string relative) => Path.Combine(_folder, relative.Replace('/', Path.DirectorySeparatorChar));
}
