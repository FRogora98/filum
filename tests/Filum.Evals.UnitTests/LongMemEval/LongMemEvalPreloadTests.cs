using Filum.Evals.LongMemEval;
using Filum.Evals.UnitTests.Builders;

namespace Filum.Evals.UnitTests.LongMemEval;

public sealed class LongMemEvalPreloadTests
{
    [Fact]
    public async Task Events_WhenAHistoryHasTwoSessions_ShouldMakeEachAConversationOfSaidAndAnsweredDatedByItsSession()
    {
        var instance = (await LongMemEvalInstances.LoadAsync())[0];

        var events = LongMemEvalPreload.Events(instance);

        // The file's order, as the published rounds wrote them: the boat's session (17:50) first, then the bike's (14:47).
        var sessions = events.GroupBy(e => e.ConversationId).ToList();
        Assert.Equal(2, sessions.Count);
        Assert.Contains("boat", sessions[0].First().Text);
        Assert.Equal(new DateTimeOffset(2023, 4, 10, 17, 50, 0, TimeSpan.Zero), sessions[0].First().OccurredAt);
        Assert.Equal(new DateTimeOffset(2023, 4, 10, 14, 47, 0, TimeSpan.Zero), sessions[1].First().OccurredAt);
        Assert.Equal([MemoryEventKind.Said], sessions[0].Select(e => e.Kind));
        Assert.Equal([MemoryEventKind.Said, MemoryEventKind.Answered], sessions[1].Select(e => e.Kind));
        Assert.All(sessions, s => Assert.True(s.Zip(s.Skip(1)).All(p => p.First.OccurredAt < p.Second.OccurredAt)));
    }

    [Fact]
    public async Task WriteAsync_WhenWithoutAModel_ShouldKeepEveryTurnAsAnEventOfTheFolder()
    {
        var instance = (await LongMemEvalInstances.LoadAsync())[0];
        var home = Path.Combine(Path.GetTempPath(), "filum-tests", Guid.NewGuid().ToString("N"), "home");
        try
        {
            var written = await LongMemEvalPreload.WriteAsync(instance, home, embedder: null, CancellationToken.None);

            using var store = new LocalFolderStore(home);
            var kept = await store.EventsAsync(store.Owner, new EventQuery(), CancellationToken.None);
            Assert.Equal(written, kept.Count);
            Assert.Equal(instance.HaystackSessions.Sum(s => s.Count(t => t.Content.Length > 0)), written);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(home)!, recursive: true);
        }
    }
}
