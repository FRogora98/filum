using Filum.Evals.LongMemEval;

namespace Filum.Evals.UnitTests.LongMemEval;

public sealed class LongMemEvalProtocolTests
{
    [Fact]
    public void SessionMessages_WhenTheSessionIsLong_ShouldSendItInPartsNoneLongerThanAMessageLosingNothing()
    {
        var turns = Enumerable.Range(0, 60).Select(n => new LongMemEvalTurn(n % 2 == 0 ? "user" : "assistant", new string('a', 200) + n)).ToList();

        var parts = LongMemEvalProtocol.SessionMessages("2023/04/10 (Mon) 14:47", turns);

        Assert.True(parts.Count > 1);
        Assert.All(parts, p => Assert.True(p.Length <= LongMemEvalProtocol.MaxMessage, $"{p.Length}"));
        Assert.StartsWith("Here is a conversation you had with the person on 2023/04/10 (Mon) 14:47.", parts[0]);
        Assert.Contains($"(part 1 of {parts.Count})", parts[0]);
        Assert.All(Enumerable.Range(0, 60), n => Assert.Contains(parts, p => p.Contains(new string('a', 200) + n + "\n", StringComparison.Ordinal) || p.EndsWith(new string('a', 200) + n, StringComparison.Ordinal)));
        Assert.Single(LongMemEvalProtocol.SessionMessages("d", [new LongMemEvalTurn("user", "short")]));
    }
}
