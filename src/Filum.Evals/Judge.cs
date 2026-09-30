using Microsoft.Extensions.AI;

namespace Filum.Evals;

public sealed record ClaimVerdict(bool ClaimsChange, decimal CostUsd);

public sealed record JudgeAnswer(bool Yes, decimal CostUsd);

/// <summary>Decides whether an answer tells the person that something was saved, changed or removed.</summary>
public interface IClaimJudge
{
    Task<ClaimVerdict> ClaimsChangeAsync(string answer, CancellationToken cancellationToken);

    /// <summary>A scenario's own yes/no question about the answer (the <c>judge</c> expectation).</summary>
    Task<JudgeAnswer> AskAsync(string question, string answer, CancellationToken cancellationToken);
}

/// <summary>
/// One fixed question to one fixed model, answered yes or no. It reads only the answer, never the memory: whether a
/// change really happened is checked in code, from the turn's steps.
/// </summary>
public sealed class ModelClaimJudge(IChatClient model, decimal inputPricePerMillionUsd, decimal outputPricePerMillionUsd) : IClaimJudge
{
    public const string Prompt = """
        You check what an assistant told a person. Read the assistant's reply below.
        Does the reply say that the assistant has just now, while writing this reply, saved, recorded, created, added,
        updated, changed, removed or deleted something (a note, a list, a row, a rule, a fact about the person) in
        its memory?
        Not a claim: describing what was already in memory before ("your list is saved", "you have three films",
        "it's stored in your core"), a plan, an offer or a question ("I can save it", "shall I add it?").
        Answer with one word: yes or no.

        Reply:
        """;

    /// <summary>The frame of a scenario's question: the question is the scenario's data, the frame is fixed.</summary>
    public const string QuestionPrompt = """
        You check what an assistant told a person. Read the assistant's reply below and answer the question about it.
        Judge only the reply as written. Answer with one word: yes or no.

        Question:
        """;

    public async Task<ClaimVerdict> ClaimsChangeAsync(string answer, CancellationToken cancellationToken)
    {
        var (yes, cost) = await YesNoAsync($"{Prompt}\n<<<\n{answer}\n>>>", cancellationToken);
        return new ClaimVerdict(yes, cost);
    }

    public async Task<JudgeAnswer> AskAsync(string question, string answer, CancellationToken cancellationToken)
    {
        var (yes, cost) = await YesNoAsync($"{QuestionPrompt}\n{question}\n\nReply:\n<<<\n{answer}\n>>>", cancellationToken);
        return new JudgeAnswer(yes, cost);
    }

    private async Task<(bool Yes, decimal Cost)> YesNoAsync(string prompt, CancellationToken cancellationToken)
    {
        var response = await model.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)], cancellationToken: cancellationToken);
        var cost = ((response.Usage?.InputTokenCount ?? 0) * inputPricePerMillionUsd + (response.Usage?.OutputTokenCount ?? 0) * outputPricePerMillionUsd) / 1_000_000m;
        return (response.Text.Trim().StartsWith("yes", StringComparison.OrdinalIgnoreCase), cost);
    }
}
