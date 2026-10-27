using Filum.Evals.Checks;

namespace Filum.Evals.Runs;

internal sealed class ScenarioRunRecorder
{
    private readonly List<TurnRecord> _turns = [];
    private readonly List<CheckedExpectation> _checks = [];
    private readonly List<int> _claimed = [];
    private readonly List<MemorySnapshot> _memories = [];
    private decimal _judgeCost;

    // Before a turn is recorded, the ones before it.
    public IReadOnlyList<TurnRecord> Turns => _turns;

    public void Record(TurnRecord turn, MemorySnapshot memory, TurnAssessment assessment)
    {
        _turns.Add(turn);
        _memories.Add(memory);
        _checks.AddRange(assessment.Checks);
        _judgeCost += assessment.JudgeCostUsd;
        if (assessment.ClaimedNotDone)
        {
            _claimed.Add(turn.Index);
        }
    }

    // With withMemories, the memory after each turn too, so a fixed check can score it again without the models.
    public ScenarioRun ToRun(string scenarioId, string model, int repetition, bool withMemories) =>
        new(scenarioId, model, repetition, _turns.ToList(), _checks.ToList(), _claimed.ToList(), _judgeCost, withMemories ? _memories.ToList() : null);
}
