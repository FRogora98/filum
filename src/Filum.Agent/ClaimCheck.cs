using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace Filum.Agent;

/// <param name="Requested">The person asked to save, change or remove something.</param>
/// <param name="Claimed">The answer says a change was made.</param>
public sealed record ClaimVerdict(bool Requested, bool Claimed, int InputTokens, int OutputTokens, decimal CostUsd);

/// <summary>
/// One fixed question to a cheap model after a turn that changed nothing: did the person ask for a change, and does
/// the answer claim one? It reads only the person's message and the answer, and it never blocks a turn: when it
/// cannot answer, it says no.
/// </summary>
public sealed class ClaimCheck(IOptions<ReliabilityOptions> options, ModelCatalog catalog, ILogger<ClaimCheck> logger, IChatClientProvider? clients = null)
{
    /// <summary>The first words of the check's prompt: a scripted model in the tests recognizes it by them.</summary>
    public const string Marker = "Filum claim check.";

    public const string Prompt = Marker + """
         An assistant keeps a memory of notes and lists for a person. Read the person's message and the assistant's
        reply below, and answer two questions:
        1. requested: did the person ask to save, record, add, change, correct, remove or delete something (a fact,
           a rule, a list, a row, a note)? A question about what is already saved is not a request.
        2. claimed: does the reply say that the assistant has just now saved, added, changed or removed something?
           Describing what was already saved before is not a claim; nor is an offer or a question.
        Answer only with JSON: {"requested": true or false, "claimed": true or false}
        """;

    public bool IsEnabled => clients is not null && catalog.AnyAvailable && catalog.Find(options.Value.CheckModel) is not null;

    public async Task<ClaimVerdict> CheckAsync(string message, string answer, CancellationToken cancellationToken)
    {
        var model = catalog.Find(options.Value.CheckModel);
        if (clients is null || model is null)
        {
            return new ClaimVerdict(false, false, 0, 0, 0);
        }

        try
        {
            var response = await clients.Get(model.Id).GetResponseAsync(
                [new ChatMessage(ChatRole.User, $"{Prompt}\nThe person's message:\n<<<\n{message}\n>>>\nThe assistant's reply:\n<<<\n{answer}\n>>>")],
                cancellationToken: cancellationToken);
            var input = (int)(response.Usage?.InputTokenCount ?? 0);
            var output = (int)(response.Usage?.OutputTokenCount ?? 0);
            var text = response.Text ?? string.Empty;
            return new ClaimVerdict(Flag(text, "requested"), Flag(text, "claimed"), input, output, ModelCatalog.CostUsd(model, input, output));
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("[ ClaimCheck ] The check ({Model}) failed: {ExceptionType}; the turn is kept as it is", model.Id, exception.GetType().Name);
            return new ClaimVerdict(false, false, 0, 0, 0);
        }
    }

    private static bool Flag(string text, string name) =>
        Regex.Match(text, $@"""{name}""\s*:\s*(true|false)", RegexOptions.IgnoreCase) is { Success: true } match
        && match.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase);
}
