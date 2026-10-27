using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Testing.Builders;

namespace Filum.Engine.Storage.InMemory.UnitTests;

public sealed class InMemoryMemoryStoreConcurrencyTests
{
    private const int Turns = 20;
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly Guid _person = Guid.NewGuid();
    private readonly Guid _other = Guid.NewGuid();

    [Fact]
    public async Task AppendAsync_WhenTurnsAppendToTheSameFileAtOnce_ShouldKeepEveryWinnerOnceWithItsOwnRevision()
    {
        var memory = MemoryServices.InMemory();
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
        var memory = MemoryServices.InMemory();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, Turns).Select(i =>
            Task.Run(() => memory.RecordFactAsync(_person, Agent(), $"item {i}", "count", $"{i}", "2026-10-01", null, [], None))));

        var winners = Enumerable.Range(0, Turns).Where(i => outcomes[i].Refusal is null).ToList();
        Assert.NotEmpty(winners);
        var facts = (await memory.CurrentFactsAsync(_person, null, None)).Value!;
        Assert.Equal(winners.Select(i => $"item {i}").Order(StringComparer.Ordinal), facts.Select(f => f.Subject).Order(StringComparer.Ordinal));
        Assert.All(facts, f => Assert.Equal(f.Subject["item ".Length..], f.Value));
    }

    [Fact]
    public async Task AppendAsync_WhenTwoPeopleWriteAtOnce_ShouldShowEachOnlyTheirOwnFile()
    {
        var memory = MemoryServices.InMemory();

        await Task.WhenAll(Enumerable.Range(0, Turns).Select(i =>
            Task.Run(() => memory.WriteAsync(i % 2 == 0 ? _person : _other, Agent(), $"/notes/n{i}.md", $"# Note {i}\n", None))));

        var mine = (await memory.ListAsync(_person, "/notes", true, None)).Value!.Select(f => f.Path);
        var theirs = (await memory.ListAsync(_other, "/notes", true, None)).Value!.Select(f => f.Path);
        Assert.Equal(Enumerable.Range(0, Turns).Where(i => i % 2 == 0).Select(i => $"/notes/n{i}.md").Order(StringComparer.Ordinal), mine.Order(StringComparer.Ordinal));
        Assert.Equal(Enumerable.Range(0, Turns).Where(i => i % 2 == 1).Select(i => $"/notes/n{i}.md").Order(StringComparer.Ordinal), theirs.Order(StringComparer.Ordinal));
    }

    private static MemoryActor Agent() => MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid());
}
