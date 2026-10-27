using System.Diagnostics;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Tools;
using Filum.Engine.Consolidation;
using Filum.Engine.Facts;
using Filum.Engine.Memory.Contracts;
using Filum.Engine.Memory.Core;
using Microsoft.Extensions.AI;

namespace Filum.Engine.Tools;

// Every call goes through Run, so the turn's limit, the timing and the step are the same for every tool. A new group is
// a new subclass listed in ToolCatalog.
internal abstract class MemoryToolGroup(MemoryToolSession session)
{
    protected MemoryToolSession Session { get; } = session;

    private IMemoryHistory History => Session.Memory;

    protected Guid UserId => Session.UserId;

    protected MemoryActor Actor => Session.Actor;

    public abstract IEnumerable<AIFunction> Functions();

    // A refusal (a string) goes back to the model and becomes a failed step.
    protected async Task<string> Run(string tool, string? path, string verb, Func<Task<object>> action)
    {
        var limit = Session.Limits.MaxToolCallsPerTurn;
        var call = Session.NextCall();
        if (call > limit)
        {
            var stop = $"the limit of {limit} memory actions for this message is reached; answer with what you have.";
            if (call == limit + 1)
            {
                Session.Record(new ToolStep(ToolStep.Failed, tool, path, $"Stopped: the limit of {limit} memory actions was reached", 0, null, stop));
            }

            return $"Refused: {stop}";
        }

        var stopwatch = Stopwatch.StartNew();
        Session.LastStructured = null;
        var result = await action();
        var elapsed = (int)stopwatch.ElapsedMilliseconds;
        if (result is ToolDone done)
        {
            Session.Record(new ToolStep(done.Kind, tool, done.Path, done.Description, elapsed, done.RevisionId, Rows: done.Rows, Data: done.Data));
            Session.LastStructured = done.Structured;
            return done.ForModel;
        }

        var reason = (string)result;
        Session.Record(new ToolStep(ToolStep.Failed, tool, path, $"Could not {verb} {path}".TrimEnd(), elapsed, null, reason));
        return $"Refused: {reason}";
    }

    // The consolidation rules first; the turn keeps the change's revision.
    protected Task<string> Change(
        string tool, string path, string verb, Func<Task<MemoryOutcome<MemoryChange>>> change, Func<MemoryChange, string> describe, Func<MemoryChange, string> forModel,
        CancellationToken cancellationToken, string? stepPath = null) =>
        Run(tool, path, verb, async () =>
        {
            if (await RefusedToConsolidation(path, adding: tool == ToolNames.MemoryAppend, cancellationToken) is { } refused)
            {
                return refused;
            }

            var outcome = await change();
            if (outcome.Refuses(out var refusal, out var applied))
            {
                return refusal;
            }

            if (applied.Operation == MemoryOperation.Unchanged)
            {
                return new ToolDone("Already recorded: nothing changed.", ToolStep.Read, stepPath ?? path, $"Nothing to change in {path}");
            }

            Session.AddRevision(applied.RevisionId);
            return new ToolDone(forModel(applied), ToolStep.Wrote, stepPath ?? path, describe(applied), applied.RevisionId, applied.RowCount);
        });

    // Decided in code: a pass may not create a file (other than the facts), nor change a file the person changed last
    // (adding to what was learned excepted).
    private async Task<string?> RefusedToConsolidation(string path, bool adding, CancellationToken cancellationToken)
    {
        if (!Session.IsConsolidation || path == FactTable.Path)
        {
            return null;
        }

        var author = await History.LastAuthorAsync(UserId, path, cancellationToken);
        return author is null ? $"{path} does not exist and a pass cannot create files: propose it with {ConsolidationPrompt.ProposeTool}."
            : author == MemoryAuthor.Person && !(path == LearnedFile.Path && adding) ? $"The person changed {path} themselves last; leave it as they made it."
            : null;
    }
}
