namespace Filum.Evals.Scenarios;

// Chat is a label: a new label is a new conversation, the same label continues it.
internal sealed record ScenarioTurn(string Chat, string Message, IReadOnlyList<Expectation> Expect);
