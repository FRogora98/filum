using System.ComponentModel;
using System.Globalization;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Tools;
using Filum.Engine.Memory.Contracts;
using Microsoft.Extensions.AI;

namespace Filum.Engine.Tools;

internal sealed class HistoryTools(MemoryToolSession session) : MemoryToolGroup(session)
{
    private IMemoryHistory Revisions => Session.Memory;

    public override IEnumerable<AIFunction> Functions() =>
    [
        AIFunctionFactory.Create(History, ToolNames.MemoryHistory,
            $"A file's changes, newest first, each with the id {ToolNames.MemoryUndo} takes."),
        AIFunctionFactory.Create(Undo, ToolNames.MemoryUndo,
            "Undo one change by its id; refused if the file changed again since."),
    ];

    private Task<string> History(
        [Description("Path of the file.")] string path,
        CancellationToken cancellationToken = default) =>
        Run(ToolNames.MemoryHistory, path, "read the history of", async () =>
        {
            var outcome = await Revisions.GetHistoryAsync(UserId, path, cancellationToken);
            if (outcome.Refuses(out var refusal, out var revisions))
            {
                return refusal;
            }

            var lines = revisions.Select(r =>
                $"- change {r.Id}: {r.Summary} · by {(r.Author == MemoryAuthor.Agent ? "the agent" : r.Author == MemoryAuthor.Person ? "the person" : r.Author == MemoryAuthor.Consolidation ? "the tidying pass" : "the platform")}"
                + (r.ConversationTitle is null ? string.Empty : $" in \"{r.ConversationTitle}\"")
                + $" · {r.CreatedAt.ToString(EngineConstants.Dates.DayAndMinute, CultureInfo.InvariantCulture)} UTC");
            return new ToolDone(string.Join('\n', lines), ToolStep.Read, path, $"Read the history of {path}");
        });

    private Task<string> Undo(
        [Description("The id of the change to undo.")] long revisionId,
        CancellationToken cancellationToken = default) =>
        Run(ToolNames.MemoryUndo, null, $"undo change {revisionId}", async () =>
        {
            var outcome = await Revisions.UndoAsync(UserId, Actor, revisionId, cancellationToken);
            if (outcome.Refuses(out var refusal, out var applied))
            {
                return refusal;
            }

            Session.AddRevision(applied.RevisionId);
            return new ToolDone($"Undid change {revisionId}: {applied.Path} is back to how it was before it (change {applied.RevisionId}).",
                ToolStep.Wrote, applied.Path, $"Undid a change to {applied.Path}", applied.RevisionId, applied.RowCount);
        });
}
