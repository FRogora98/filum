using Filum.Evals.Mcp;
using System.Diagnostics;
using Filum.Evals.LongMemEval.Contracts;
using Filum.Evals.Mcp.Contracts;

namespace Filum.Evals.LongMemEval.Systems;

// With consolidate, each history session is followed by one that tidies the memory with memory_consolidate, as a
// person's next session would start. With keepMemory, the agent's folder is kept after the question, to read what it
// saved.
internal sealed class ClaudeSystemStrategy(IAgentStrategy agent, ClaudeSystemMode mode, string filumMcp, string model, bool consolidate = false, bool hook = false, bool keepMemory = false) : ILongMemEvalSystemStrategy
{
    // What filum-mcp's instructions ask a host once a session, after its first answer.
    public const string ConsolidatePrompt = "Tidy your memory now: call memory_consolidate, do what it asks, and close it as it says. Nothing else.";

    private const string TemporaryFolder = "filum-evals-lme";

    private bool WithFilum => mode == ClaudeSystemMode.WithFilum;

    public string Name => mode switch
    {
        ClaudeSystemMode.WithFilum when consolidate && hook => "claude code + filum-mcp, hook, consolidated",
        ClaudeSystemMode.WithFilum when hook => "claude code + filum-mcp, hook",
        ClaudeSystemMode.WithFilum when consolidate => "claude code + filum-mcp, consolidated",
        ClaudeSystemMode.WithFilum => "claude code + filum-mcp",
        ClaudeSystemMode.NoMemory => "claude code, no memory",
        _ => "claude code, whole history in the prompt"
    };

    public string Model => model;

    public int SessionsFor(LongMemEvalInstance instance) => WithFilum ? instance.HaystackSessions.Count * (consolidate ? 2 : 1) + 1 : 1;

    public async Task<LongMemEvalAnswer> AnswerAsync(LongMemEvalInstance instance, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var folder = RunFolder.Create(TemporaryFolder);
        var root = folder.FullPath;
        var config = await new McpConfigWriter(filumMcp, hook).WriteConfigAsync(root, WithFilum, Path.Combine(root, EvalsConstants.Folders.Memory), cancellationToken);
        try
        {
            var (turns, failure) = WithFilum ? await IngestAsync(instance, root, config, cancellationToken) : (0, null);
            if (failure is not null)
            {
                return LongMemEvalAnswer.Failed(failure, clock.Elapsed.TotalSeconds, turns);
            }

            var prompt = mode == ClaudeSystemMode.FullContext ? LongMemEvalProtocol.QuestionAfter(instance.Sessions, instance) : LongMemEvalProtocol.Question(instance);
            var asked = await agent.AskAsync(prompt, null, Directory.CreateDirectory(Path.Combine(root, EvalsConstants.Folders.Question)).FullName, config, WithFilum, cancellationToken);
            return new LongMemEvalAnswer(asked.Answer, 0, 0, 0m, clock.Elapsed.TotalSeconds, turns + 1, asked.Error, asked.Model);
        }
        finally
        {
            if (keepMemory)
            {
                Console.Error.WriteLine($"Kept the memory of {instance.QuestionId} in {root}");
            }
            else
            {
                folder.Delete();
            }
        }
    }

    // Returns the sessions taken and the first failure.
    private async Task<(int Turns, string? Failure)> IngestAsync(LongMemEvalInstance instance, string root, string config, CancellationToken cancellationToken)
    {
        var turns = 0;
        foreach (var (date, sessionTurns) in instance.Sessions)
        {
            var work = Directory.CreateDirectory(Path.Combine(root, $"session-{++turns}")).FullName;
            var ingested = await agent.AskAsync($"{LongMemEvalProtocol.Header(date)}\n\n{LongMemEvalProtocol.Transcript(sessionTurns)}", null, work, config, filumMounted: true, cancellationToken);
            if (ingested.Error is not null)
            {
                return (turns, $"session of {date}: {ingested.Error}");
            }

            if (consolidate)
            {
                var tidy = Directory.CreateDirectory(Path.Combine(root, $"consolidate-{turns}")).FullName;
                var tidied = await agent.AskAsync(ConsolidatePrompt, null, tidy, config, filumMounted: true, cancellationToken);
                if (tidied.Error is not null)
                {
                    return (turns, $"consolidation after {date}: {tidied.Error}");
                }
            }
        }

        return (turns, null);
    }
}
