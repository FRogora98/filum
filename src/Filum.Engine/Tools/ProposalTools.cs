using System.ComponentModel;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Tools;
using Filum.Engine.Events;
using Filum.Engine.Memory.Contracts;
using Microsoft.Extensions.AI;

namespace Filum.Engine.Tools;

// A consolidation pass proposes; a turn records the person's answer.
internal sealed class ProposalTools(MemoryToolSession session) : MemoryToolGroup(session)
{
    private const int MaxProposalLength = 500;
    private const int ProposalSummaryLength = 100;

    private IMemoryConsolidation Proposals => Session.Memory;

    public override IEnumerable<AIFunction> Functions() =>
    [
        AIFunctionFactory.Create(AnswerProposal, ToolNames.ProposalAnswer,
            "Record the person's answer to a waiting proposal."),
        AIFunctionFactory.Create(Propose, ToolNames.MemoryPropose,
            "Propose to the person a new file or a new section that the memory needs (a collection for a new kind of entries, a document for a new topic, a skill, a section of the core): what, where, and why, in one or two sentences, with the ids of the events it comes from. Nothing is created; the person decides."),
    ];

    private Task<string> AnswerProposal(
        [Description("The id of the proposal.")] long id,
        [Description("Whether they accepted.")] bool accepted,
        CancellationToken cancellationToken = default) =>
        Run(ToolNames.ProposalAnswer, null, $"record the answer to proposal {id}", async () =>
        {
            var outcome = await Proposals.AnswerProposalAsync(UserId, id, accepted, MemoryEventSource.Chat, cancellationToken);
            if (outcome.Refuses(out var refusal, out _))
            {
                return refusal;
            }

            return new ToolDone(accepted ? $"Proposal {id} accepted: now create what it proposed." : $"Proposal {id} declined; it will not be shown again.",
                ToolStep.Asked, null, accepted ? $"The person accepted proposal {id}" : $"The person declined proposal {id}");
        });

    private Task<string> Propose(
        [Description("What to create, where and why, in one or two sentences.")] string proposal,
        [Description("The ids of the events it comes from.")] List<long>? sources = null,
        CancellationToken cancellationToken = default) =>
        Run(ToolNames.MemoryPropose, null, "propose", async () =>
        {
            if (string.IsNullOrWhiteSpace(proposal) || proposal.Length > MaxProposalLength)
            {
                return $"A proposal is one or two sentences, at most {MaxProposalLength} characters.";
            }

            var open = await Proposals.OpenProposalsAsync(UserId, cancellationToken);
            if (open.Any(p => string.Equals(p.Text, proposal.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return "That proposal is already waiting for the person.";
            }

            var proposed = await Proposals.ProposeAsync(UserId, proposal.Trim(), sources ?? Session.Sources, cancellationToken);
            return new ToolDone($"Proposed (proposal {proposed.Id}); nothing was created.", ToolStep.Asked, null, $"Proposed: {EventSearch.Cut(proposal.Trim(), ProposalSummaryLength)}");
        });
}
