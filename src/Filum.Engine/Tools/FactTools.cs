using System.ComponentModel;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Tools;
using Filum.Engine.Facts;
using Filum.Engine.Memory.Contracts;
using Microsoft.Extensions.AI;

namespace Filum.Engine.Tools;

internal sealed class FactTools(MemoryToolSession session) : MemoryToolGroup(session)
{
    private IMemoryFacts Facts => Session.Memory;

    public override IEnumerable<AIFunction> Functions() =>
    [
        AIFunctionFactory.Create(RecordFact, ToolNames.FactRecord,
            "Record a value that holds for a time; the previous one of the same subject and attribute is closed on validFrom and kept."),
        AIFunctionFactory.Create(EndFact, ToolNames.FactEnd,
            "End a fact that no longer holds, with nothing in its place."),
        AIFunctionFactory.Create(CurrentFacts, ToolNames.FactsCurrent,
            "The facts that hold now, or on a day, of one subject or all."),
        AIFunctionFactory.Create(FactHistory, ToolNames.FactsHistory,
            "Every value the facts of a subject had, oldest first, with their dates."),
    ];

    private Task<string> RecordFact(
        [Description("Who or what it is about.")] string subject,
        [Description("What about it, in a word or two.")] string attribute,
        [Description("The value it has now.")] string value,
        [Description("The day it started, yyyy-MM-dd; today if empty.")] string? validFrom = null,
        [Description("A short note.")] string? note = null,
        [Description("Ids of the events it comes from.")] List<long>? sources = null,
        [Description("True when the current value was wrong.")] bool correction = false,
        CancellationToken cancellationToken = default) =>
        Change(ToolNames.FactRecord, FactTable.Path, "record the fact in",
            () => Facts.RecordFactAsync(UserId, Actor, subject, attribute, value, validFrom, note, sources is { Count: > 0 } ? sources : Session.Sources, correction, cancellationToken),
            _ => $"Recorded {subject} · {attribute}: {value}",
            _ => $"Recorded: {subject} · {attribute} is {value}; the value before, if any, is kept with the day it ended.", cancellationToken);

    private Task<string> EndFact(
        [Description("Who or what it is about.")] string subject,
        [Description("What about it.")] string attribute,
        [Description("The day it ended, yyyy-MM-dd; today if empty.")] string? on = null,
        [Description("True when it never held.")] bool retract = false,
        [Description("Which value, when the attribute holds several (rule, open).")] string? value = null,
        [Description("Ids of the events that say it ended.")] List<long>? sources = null,
        CancellationToken cancellationToken = default) =>
        Change(ToolNames.FactEnd, FactTable.Path, "end the fact in",
            () => Facts.EndFactAsync(UserId, Actor, subject, attribute, on, retract, value, sources is { Count: > 0 } ? sources : Session.Sources, cancellationToken),
            _ => $"Ended {subject} · {attribute}",
            _ => $"Ended: {subject} · {attribute} no longer holds; its history is kept.", cancellationToken);

    private Task<string> CurrentFacts(
        [Description("Whose; all if empty.")] string? subject = null,
        [Description("A past day, yyyy-MM-dd; now if empty.")] string? on = null,
        CancellationToken cancellationToken = default) =>
        Run(ToolNames.FactsCurrent, FactTable.Path, "read the facts in", async () =>
        {
            var outcome = await Facts.CurrentFactsAsync(UserId, subject, on, cancellationToken);
            if (outcome.Refuses(out var refusal, out var facts))
            {
                return refusal;
            }

            var text = facts.Count == 0 ? "No fact holds now for that." : string.Join('\n', facts.Select(FactTable.Line));
            if (string.IsNullOrWhiteSpace(on) && (await Facts.PlannedFactsAsync(UserId, subject, cancellationToken)).Value is { Count: > 0 } planned)
            {
                text += $"\n\n{FactTable.PlannedTitle}\n{string.Join('\n', planned.Select(FactTable.PlannedLine))}";
            }
            if (facts.Count > 0 && !string.IsNullOrWhiteSpace(subject)
                && FactLinks.Text((await Facts.CurrentFactsAsync(UserId, null, cancellationToken)).Value ?? [], facts) is { Length: > 0 } links)
            {
                text += $"\n\n{links}";
            }

            return new ToolDone(text, ToolStep.Read, FactTable.Path,
                $"Read the current facts{(string.IsNullOrWhiteSpace(subject) ? string.Empty : $" of {subject}")}", Structured: ToolData.Facts(facts));
        });

    private Task<string> FactHistory(
        [Description("Whose facts.")] string subject,
        [Description("Only this attribute.")] string? attribute = null,
        CancellationToken cancellationToken = default) =>
        Run(ToolNames.FactsHistory, FactTable.Path, "read the history of the facts in", async () =>
        {
            var outcome = await Facts.FactHistoryAsync(UserId, subject, attribute, cancellationToken);
            if (outcome.Refuses(out var refusal, out var facts))
            {
                return refusal;
            }

            return new ToolDone(facts.Count == 0 ? $"No fact of {subject} is recorded." : string.Join('\n', facts.Select(FactTable.Line)), ToolStep.Read, FactTable.Path,
                $"Read how the facts of {subject} changed", Structured: ToolData.Facts(facts));
        });
}
