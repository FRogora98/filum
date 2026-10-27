using Filum.Evals.Scenarios;

namespace Filum.Evals.Mcp;

internal sealed class SessionBudget(int max)
{
    public int Max { get; } = max;

    public int Used { get; private set; }

    public static int Needed(IReadOnlyList<Scenario> scenarios, int repetitions) => scenarios.Sum(s => s.TurnCount) * repetitions * McpArms.SessionsPerTurn;

    public bool CanStart(int sessions) => Used + sessions <= Max;

    public void Spend() => Used++;
}
