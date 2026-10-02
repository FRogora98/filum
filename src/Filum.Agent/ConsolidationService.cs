using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Filum.Agent;

/// <summary>When and how the hosted product consolidates (spec 030): section <c>Consolidation</c>.</summary>
public sealed class ConsolidationOptions
{
    public const string SectionName = "Consolidation";

    /// <summary>The background passes (after quiet, nightly); a pass on demand works either way.</summary>
    public bool Enabled { get; set; }

    /// <summary>The catalog model of a pass; empty = the default model, the cheapest a host usually picks.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>A person's events are consolidated once nothing was said for this long.</summary>
    public int QuietMinutes { get; set; } = 10;

    /// <summary>The hour (UTC) of the nightly pass, which consolidates whatever is pending.</summary>
    public int NightlyHourUtc { get; set; } = 3;

    /// <summary>A pass reads at most this many events; the rest wait for the next one.</summary>
    public int MaxEvents { get; set; } = 200;
}

/// <summary>What a pass did: the events it read, the changes it made, the proposals it left, and what it cost.</summary>
public sealed record ConsolidationResult(int Events, int Changes, int Proposals, int InputTokens = 0, int OutputTokens = 0, decimal CostUsd = 0);

public enum ConsolidationOutcome
{
    Done,
    NotConfigured,
    BudgetReached,
    Busy,
    Failed
}

/// <summary>
/// One consolidation pass for one person (spec 030): a cheap model, the engine's consolidation tools, the events since
/// the last pass. The rules (no new files, the person's edits win) are the tools'; the pass is recorded as an event
/// and its cost as usage. Two passes for the same person never run at once: a Postgres advisory lock guards them.
/// </summary>
public sealed class ConsolidationService(
    DbContext db,
    MemoryService memory,
    IOptions<MemoryOptions> memoryOptions,
    IOptions<ConsolidationOptions> options,
    ModelCatalog catalog,
    UsageService usage,
    ILogger<ConsolidationService> logger,
    IChatClientProvider? clients = null)
{
    public async Task<(ConsolidationOutcome Outcome, ConsolidationResult? Result)> RunAsync(Guid userId, CancellationToken cancellationToken)
    {
        var model = catalog.Find(options.Value.Model) ?? catalog.Default;
        if (clients is null || !catalog.AnyAvailable)
        {
            return (ConsolidationOutcome.NotConfigured, null);
        }

        if (await usage.SpentThisMonthAsync(userId, cancellationToken) >= usage.MonthlyBudgetUsd)
        {
            return (ConsolidationOutcome.BudgetReached, null);
        }

        await db.Database.OpenConnectionAsync(cancellationToken);
        var key = BitConverter.ToInt64(userId.ToByteArray(), 0);
        try
        {
            if (!await db.Database.SqlQueryRaw<bool>("SELECT pg_try_advisory_lock({0}) AS \"Value\"", key).SingleAsync(cancellationToken))
            {
                return (ConsolidationOutcome.Busy, null);
            }

            try
            {
                return await PassAsync(userId, model, cancellationToken);
            }
            finally
            {
                await db.Database.SqlQueryRaw<bool>("SELECT pg_advisory_unlock({0}) AS \"Value\"", key).SingleAsync(CancellationToken.None);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task<(ConsolidationOutcome, ConsolidationResult?)> PassAsync(Guid userId, ModelDefinition model, CancellationToken cancellationToken)
    {
        var pending = await memory.PendingAsync(userId, options.Value.MaxEvents, cancellationToken);
        if (pending.Count == 0)
        {
            return (ConsolidationOutcome.Done, new ConsolidationResult(0, 0, 0));
        }

        var read = pending.Select(e => e.Id).ToList();
        var tools = new MemoryTools(memory, memoryOptions.Value, userId, MemoryActor.Consolidation, read, consolidation: true);
        UsageDetails? used;
        try
        {
            var agent = clients!.Get(model.Id)
                .AsBuilder()
                .UseFunctionInvocation(configure: loop => loop.MaximumIterationsPerRequest = memoryOptions.Value.MaxToolCallsPerTurn + 5)
                .Build()
                .AsAIAgent(instructions: Consolidation.Instructions, name: "consolidation", tools: tools.Tools);
            var run = await agent.RunAsync([new ChatMessage(ChatRole.User, Consolidation.Prompt(pending))], cancellationToken: cancellationToken);
            used = run.Usage;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // What it wrote stays (each change is a revision, undoable); the events wait for the next pass.
            logger.LogWarning("[ Consolidation ] The pass of user {UserId} ({Model}) failed after {Changes} changes: {ExceptionType}", userId, model.Id, tools.Revisions.Count, exception.GetType().Name);
            return (ConsolidationOutcome.Failed, null);
        }

        var input = (int)(used?.InputTokenCount ?? 0);
        var output = (int)(used?.OutputTokenCount ?? 0);
        var cost = ModelCatalog.CostUsd(model, input, output);
        db.Set<UsageRecord>().Add(new UsageRecord
        {
            UserId = userId,
            ConversationId = Guid.Empty,
            MessageId = Guid.NewGuid(),
            Model = model.Id,
            InputTokens = input,
            OutputTokens = output,
            CostUsd = cost,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        await memory.CloseConsolidationAsync(userId, read, tools.Revisions, cancellationToken);
        var proposals = tools.Steps.Count(s => s.Tool == Consolidation.ProposeTool && s.Kind == ToolStep.Asked);
        logger.LogInformation("[ Consolidation ] User {UserId}: {Events} events read, {Changes} changes, {Proposals} proposals ({Model}, {Input} in, {Output} out)", userId, read.Count, tools.Revisions.Count, proposals, model.Id, input, output);
        return (ConsolidationOutcome.Done, new ConsolidationResult(read.Count, tools.Revisions.Count, proposals, input, output, cost));
    }
}
