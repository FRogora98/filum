using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Testing.Builders;

namespace Filum.Engine.Storage.LocalFolder.IntegrationTests;

public sealed class LocalFolderStoreConcurrencyTests : IDisposable
{
    private const int Turns = 20;
    private static readonly CancellationToken None = CancellationToken.None;

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
    public async Task AppendAsync_WhenTurnsAppendToTheSameFileAtOnce_ShouldKeepEveryWinnerOnceWithItsOwnRevision()
    {
        var memory = MemoryServices.On(new LocalFolderStore(_folder, _person));
        Assert.Null((await memory.WriteAsync(_person, Agent(), "/notes/log.md", "# Log\n", None)).Refusal);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, Turns).Select(i =>
            Task.Run(() => memory.AppendAsync(_person, Agent(), "/notes/log.md", $"line {i}", None))));

        var winners = Enumerable.Range(0, Turns).Where(i => outcomes[i].Refusal is null).ToList();
        Assert.NotEmpty(winners);
        Assert.All(outcomes.Where(o => o.Refusal is not null), o => Assert.Contains("changed at the same time", o.Refusal, StringComparison.Ordinal));
        var lines = (await memory.ReadAsync(_person, "/notes/log.md", null, null, None)).Value!.Lines;
        Assert.Equal(winners.Select(i => $"line {i}").Order(StringComparer.Ordinal), lines.Skip(1).Order(StringComparer.Ordinal));
        var history = (await memory.GetHistoryAsync(_person, "/notes/log.md", None)).Value!;
        Assert.Equal(winners.Count + 1, history.Count);
        Assert.Equal(history.Count, history.Select(r => r.Id).Distinct().Count());
    }

    [Fact]
    public async Task RecordFactAsync_WhenTurnsRecordFactsAtOnce_ShouldKeepTheFactsCollectionWholeWithEveryWinnerOnce()
    {
        var memory = MemoryServices.On(new LocalFolderStore(_folder, _person));

        var outcomes = await Task.WhenAll(Enumerable.Range(0, Turns).Select(i =>
            Task.Run(() => memory.RecordFactAsync(_person, Agent(), $"item {i}", "count", $"{i}", "2026-10-01", null, [], None))));

        var winners = Enumerable.Range(0, Turns).Where(i => outcomes[i].Refusal is null).ToList();
        Assert.NotEmpty(winners);
        var facts = (await memory.CurrentFactsAsync(_person, null, None)).Value!;
        Assert.Equal(winners.Select(i => $"item {i}").Order(StringComparer.Ordinal), facts.Select(f => f.Subject).Order(StringComparer.Ordinal));
        Assert.All(facts, f => Assert.Equal(f.Subject["item ".Length..], f.Value));
    }

    private static MemoryActor Agent() => MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid());
}
