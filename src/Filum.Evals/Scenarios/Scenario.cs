namespace Filum.Evals.Scenarios;

internal sealed record Scenario(string Id, string Language, string? Description, IReadOnlyList<ScenarioTurn> Turns)
{
    public int TurnCount => Turns.Count;
}
