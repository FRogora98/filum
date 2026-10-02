using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

namespace Filum.Agent;

public enum TurnOutcome
{
    Answered,
    Invalid,
    NotFound,
    BudgetReached,
    NotConfigured,
    AssistantFailed,
    /// <summary>A host's turn gate refused the turn, with its own status and message (spec 017).</summary>
    Refused
}

public sealed record TurnResult(TurnOutcome Outcome, SendMessageResponse? Response = null, string? Error = null, int? Status = null);

/// <summary>
/// Conversations of one user, stateless: everything is read from and written to the database on each call,
/// and the agent is built for this request only, with the model the person chose, the person's core and the memory
/// tools of this turn. Every query is scoped by user id; another user's conversation does not exist. Logs carry ids,
/// models, durations and outcomes, never content.
/// </summary>
public sealed class ConversationService(
    DbContext db,
    IOptions<OpenAIOptions> openAIOptions,
    ModelCatalog modelCatalog,
    UsageService usageService,
    MemoryService memoryService,
    IOptions<MemoryOptions> memoryOptions,
    IOptions<ReliabilityOptions> reliabilityOptions,
    ClaimCheck claimCheck,
    ILogger<ConversationService> logger,
    IChatClientProvider? chatClientProvider = null,
    IOptions<AgentOptions>? agentOptions = null,
    IEnumerable<ITurnToolSource>? toolSources = null,
    IEnumerable<ITurnGate>? gates = null,
    IEnumerable<ITurnObserver>? observers = null,
    IServiceProvider? services = null,
    TimeProvider? time = null)
{
    private AgentOptions Agent => agentOptions?.Value ?? new AgentOptions();

    public const int MaxContentLength = 8000;

    private static readonly JsonSerializerOptions StepsJson = new(JsonSerializerDefaults.Web);

    private const string AssistantFailedError = "The assistant could not answer. Try again.";

    public async Task<IReadOnlyList<ConversationDto>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await db.Set<Conversation>()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new
            {
                c.Id,
                c.Title,
                c.Model,
                c.CreatedAt,
                c.UpdatedAt,
                LastMessage = db.Set<ConversationMessage>()
                    .Where(m => m.ConversationId == c.Id)
                    .OrderByDescending(m => m.Sequence)
                    .Select(m => m.Content)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new ConversationDto(r.Id, r.Title, r.Model, Conversation.PreviewFrom(r.LastMessage), r.CreatedAt, r.UpdatedAt))
            .ToList();
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var deleted = await db.Set<Conversation>()
            .Where(c => c.Id == conversationId && c.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            // What was said goes with the conversation; what the memory made of it stays (spec 030).
            await memoryService.ForgetWhatWasSaidAsync(userId, conversationId, cancellationToken);
            logger.LogInformation("[ ConversationService ] Conversation {ConversationId} of user {UserId} deleted", conversationId, userId);
        }

        return deleted > 0;
    }

    /// <summary>
    /// Forgets a conversation for good (spec 030): what was said, what the memory made only of it, and the conversation
    /// itself. Null when the person has no such conversation, nor anything left of one.
    /// </summary>
    public async Task<ForgetResult?> ForgetAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var forgotten = await memoryService.ForgetConversationAsync(userId, conversationId, cancellationToken);
        var deleted = await DeleteAsync(userId, conversationId, cancellationToken);
        return forgotten.Events == 0 && !deleted ? null : forgotten;
    }

    /// <summary>The messages, oldest first; empty for an id nobody owns yet; null when the conversation belongs to someone else.</summary>
    public async Task<IReadOnlyList<MessageDto>?> GetMessagesAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var owner = await db.Set<Conversation>()
            .Where(c => c.Id == conversationId)
            .Select(c => (Guid?)c.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (owner is not null && owner != userId)
        {
            return null;
        }

        var messages = (await db.Set<ConversationMessage>()
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.Sequence)
            .ToListAsync(cancellationToken))
            .Select(ToDto)
            .ToList();

        // A step stays as it was stored; whether it was undone since is read from the memory's history.
        var revisions = messages.SelectMany(m => m.Steps).Select(s => s.RevisionId).OfType<long>().ToList();
        var undone = await memoryService.UndoneAmongAsync(userId, revisions, cancellationToken);
        return undone.Count == 0
            ? messages
            : messages.Select(m => m with { Steps = m.Steps.Select(s => s.RevisionId is { } id && undone.Contains(id) ? s with { Undone = true } : s).ToList() }).ToList();
    }

    public async Task<TurnResult> SendAsync(Guid userId, Guid conversationId, SendMessageRequest request, CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return new TurnResult(TurnOutcome.Invalid, Error: validationError);
        }

        // The host may keep the choice of model to itself: then the one a request names is ignored.
        var model = request.Model is null || !Agent.AllowModelChoice ? modelCatalog.Default : modelCatalog.Find(request.Model);
        if (model is null)
        {
            return new TurnResult(TurnOutcome.Invalid, Error: $"The model '{request.Model}' is not available.");
        }

        var conversation = await db.Set<Conversation>().FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);
        if (conversation is not null && conversation.UserId != userId)
        {
            return new TurnResult(TurnOutcome.NotFound, Error: "The conversation was not found.");
        }

        var userMessage = await db.Set<ConversationMessage>().FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);
        if (userMessage is not null && userMessage.ConversationId != conversationId)
        {
            return new TurnResult(TurnOutcome.Invalid, Error: "This message id belongs to another conversation.");
        }

        if (userMessage is not null)
        {
            // A resend of a message that was already answered returns the stored answer: the model is not called, nothing is charged again.
            var existingAnswer = await db.Set<ConversationMessage>().FirstOrDefaultAsync(m => m.ReplyTo == userMessage.Id, cancellationToken);
            if (existingAnswer is not null)
            {
                var storedUsage = await db.Set<UsageRecord>().FirstOrDefaultAsync(u => u.MessageId == existingAnswer.Id, cancellationToken);
                return Answered(conversation!, userMessage, existingAnswer, storedUsage);
            }
        }

        var turn = new TurnContext(userId, conversationId, request.Id, services ?? EmptyServices.Instance, (time ?? TimeProvider.System).GetUtcNow(), TimeZoneOf(Agent.TimeZone), cancellationToken);
        foreach (var gate in gates ?? [])
        {
            var decision = await gate.CheckAsync(turn);
            if (!decision.Allowed)
            {
                logger.LogInformation("[ ConversationService ] Message {MessageId} of user {UserId} refused by the host's gate {Gate} ({Status})", request.Id, userId, gate.GetType().Name, decision.Status);
                return new TurnResult(TurnOutcome.Refused, Error: decision.Message, Status: decision.Status);
            }
        }

        if (await usageService.SpentThisMonthAsync(userId, cancellationToken) >= usageService.MonthlyBudgetUsd)
        {
            logger.LogWarning("[ ConversationService ] Message {MessageId} of user {UserId} refused: the monthly budget has been reached", request.Id, userId);
            return new TurnResult(TurnOutcome.BudgetReached, Error: "The monthly budget has been reached.");
        }

        var now = DateTimeOffset.UtcNow;
        if (conversation is null)
        {
            conversation = new Conversation
            {
                Id = conversationId,
                UserId = userId,
                Title = Conversation.TitleFrom(request.Content!),
                Model = model.Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Set<Conversation>().Add(conversation);
        }

        if (userMessage is null)
        {
            userMessage = new ConversationMessage
            {
                Id = request.Id,
                ConversationId = conversationId,
                Role = ConversationMessage.UserRole,
                Content = request.Content!.Trim(),
                CreatedAt = now
            };
            db.Set<ConversationMessage>().Add(userMessage);
        }

        conversation.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        // What the person said is kept before anything else runs (spec 030): nothing is lost if the turn writes nothing.
        var said = await SaidAsync(userId, conversationId, userMessage, cancellationToken);

        if (chatClientProvider is null)
        {
            logger.LogWarning("[ ConversationService ] Message {MessageId} in conversation {ConversationId} not answered: no model provider has an API key", userMessage.Id, conversationId);
            return new TurnResult(TurnOutcome.NotConfigured, Error: "No model provider is configured.");
        }

        var history = await db.Set<ConversationMessage>()
            .Where(m => m.ConversationId == conversationId && m.Sequence <= userMessage.Sequence)
            .OrderBy(m => m.Sequence)
            .Select(m => new HistoryMessage(m.Id, m.Role, m.Content))
            .ToListAsync(cancellationToken);
        var spend = new TurnSpend();
        var messages = await FitHistoryAsync(userId, conversationId, history, spend, cancellationToken);

        var core = await memoryService.EnsureCoreAsync(userId, cancellationToken);
        var index = await memoryService.BuildIndexAsync(userId, cancellationToken);
        var tools = new MemoryTools(memoryService, memoryOptions.Value, userId, MemoryActor.Agent(conversationId, userMessage.Id), [said.Id]);
        var skills = await memoryService.ListSkillsAsync(userId, includePrivate: false, cancellationToken);

        // The host's own tools, beside the engine's: each call is a step of this turn (spec 017).
        var turnTools = tools.Tools
            .Concat((toolSources ?? [])
                .SelectMany(source => source.Tools(turn))
                .Where(t => !memoryOptions.Value.ExcludedTools.Contains(t.Name, StringComparer.Ordinal))
                .Select(t => (AITool)new RecordedHostTool(t, tools, logger)))
            .ToList();

        // A message that starts with /name gets that skill, whole, from the code: no decision of the model is needed.
        string? invoked = null;
        if (Skills.Invocation(userMessage.Content) is { } invocation)
        {
            var skill = skills.FirstOrDefault(s => s.Skill.Name == invocation.Name && s.Skill.Enabled);
            if (skill is null)
            {
                invoked = PlatformInstructions.UnknownSkill(invocation.Name, Skills.Closest(invocation.Name, skills.Where(s => s.Skill.Enabled).Select(s => s.Skill.Name)));
            }
            else
            {
                invoked = PlatformInstructions.Invoked(skill.Skill, invocation.Input);
                tools.RecordInvoked(skill);
            }
        }

        var repeated = invoked is null ? await RepeatedPathsAsync(conversationId, userMessage.Sequence, cancellationToken) : [];

        var stopwatch = Stopwatch.StartNew();
        var instructions = PlatformInstructions.Compose(core, index, PlatformInstructions.SkillList(skills, memoryOptions.Value.SkillListMax), invoked,
            repeated.Count == 0 ? null : PlatformInstructions.Repetition(repeated), memoryService.Pack, Agent.Name)
            + PlatformInstructions.Proposals(await memoryService.OpenProposalsAsync(userId, cancellationToken));
        string? answerText;
        var answerModel = model;
        var unverified = false;
        var retried = false;
        var escalated = false;
        string checkOutcome = "off";
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(openAIOptions.Value.TimeoutSeconds));

            answerText = await RunAgentAsync(model, instructions, turnTools, messages, spend, timeout.Token);

            // A turn that changed nothing is checked: if the person asked for a change, or the answer claims one, the
            // agent gets one second attempt, told plainly that nothing was saved. A turn that wrote is not checked.
            // A tool call written into the answer as text was never made: that turn gets the same second attempt,
            // whatever else it did, with no check needed.
            var leaked = ToolCallText.Contains(answerText);
            if (!string.IsNullOrEmpty(answerText) && (leaked || (claimCheck.IsEnabled && !tools.Steps.Any(s => s.Kind == StepDto.Wrote))))
            {
                var first = leaked ? null : await claimCheck.CheckAsync(userMessage.Content, answerText, timeout.Token);
                if (first is not null)
                {
                    spend.Add(first);
                }

                checkOutcome = leaked ? "tool-call-as-text" : first!.Requested || first.Claimed ? "asked-or-claimed" : "clean";
                if (leaked || first!.Requested || first.Claimed)
                {
                    retried = true;
                    var again = await RunAgentAsync(model, instructions, turnTools, [.. messages, new(ChatRole.Assistant, answerText), new(ChatRole.User, PlatformInstructions.SecondAttemptNote)], spend, timeout.Token);
                    if (!string.IsNullOrEmpty(again))
                    {
                        answerText = again;
                    }

                    if (claimCheck.IsEnabled && !tools.Steps.Any(s => s.Kind == StepDto.Wrote))
                    {
                        var second = await claimCheck.CheckAsync(userMessage.Content, answerText, timeout.Token);
                        spend.Add(second);
                        unverified = second.Claimed;
                        var stronger = modelCatalog.Find(reliabilityOptions.Value.EscalationModel);
                        if (unverified && stronger is not null && stronger.Id != model.Id
                            && await usageService.SpentThisMonthAsync(userId, timeout.Token) + spend.CostUsd < usageService.MonthlyBudgetUsd)
                        {
                            escalated = true;
                            var last = await RunAgentAsync(stronger, instructions, turnTools, [.. messages, new(ChatRole.Assistant, answerText), new(ChatRole.User, PlatformInstructions.SecondAttemptNote)], spend, timeout.Token);
                            if (!string.IsNullOrEmpty(last))
                            {
                                answerText = last;
                                answerModel = stronger;
                            }

                            if (!tools.Steps.Any(s => s.Kind == StepDto.Wrote))
                            {
                                var third = await claimCheck.CheckAsync(userMessage.Content, answerText, timeout.Token);
                                spend.Add(third);
                                unverified = third.Claimed;
                            }
                            else
                            {
                                unverified = false;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // The caller went away: nobody will see this answer, so memory goes back to how it was.
            await memoryService.RollbackAsync(userId, tools.Revisions, CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning("[ ConversationService ] The assistant ({Model}) failed on message {MessageId} in conversation {ConversationId} after {ElapsedMs}ms and {ToolCalls} memory actions: {ExceptionType}", model.Id, userMessage.Id, conversationId, stopwatch.ElapsedMilliseconds, tools.Steps.Count, exception.GetType().Name);
            await RollBackAsync(userId, tools);
            return new TurnResult(TurnOutcome.AssistantFailed, Error: AssistantFailedError);
        }

        // Still a tool call as text after the second attempt: the person never sees it, and the answer is marked.
        if (ToolCallText.Contains(answerText))
        {
            unverified = true;
            answerText = ToolCallText.Strip(answerText!);
            logger.LogWarning("[ ConversationService ] The assistant ({Model}) wrote a tool call as text on message {MessageId}, even after a second attempt", answerModel.Id, userMessage.Id);
        }

        if (string.IsNullOrEmpty(answerText))
        {
            logger.LogWarning("[ ConversationService ] The assistant ({Model}) returned an empty answer to message {MessageId} in conversation {ConversationId}", model.Id, userMessage.Id, conversationId);
            await RollBackAsync(userId, tools);
            return new TurnResult(TurnOutcome.AssistantFailed, Error: AssistantFailedError);
        }

        var steps = tools.Steps.Select(StepDto.From).ToList();

        if (!spend.UsageReported)
        {
            logger.LogWarning("[ ConversationService ] The assistant ({Model}) reported no token usage for message {MessageId}: the answer is counted at zero cost", model.Id, userMessage.Id);
        }

        var inputTokens = spend.InputTokens;
        var outputTokens = spend.OutputTokens;
        var answeredAt = DateTimeOffset.UtcNow;
        var answer = new ConversationMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = ConversationMessage.AssistantRole,
            Content = answerText,
            ReplyTo = userMessage.Id,
            CreatedAt = answeredAt,
            Model = answerModel.Id,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            StepsJson = steps.Count == 0 ? null : JsonSerializer.Serialize(steps, StepsJson),
            Unverified = unverified,
            ProposalJson = tools.Proposal is { } proposal ? JsonSerializer.Serialize(SkillProposalDto.From(proposal), StepsJson) : null
        };
        await MarkProposalsSavedAsync(conversationId, tools.SavedSkills, cancellationToken);
        var usage = new UsageRecord
        {
            UserId = userId,
            ConversationId = conversationId,
            MessageId = answer.Id,
            Model = model.Id,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CostUsd = spend.CostUsd,
            CreatedAt = answeredAt
        };
        db.Set<ConversationMessage>().Add(answer);
        db.Set<UsageRecord>().Add(usage);
        conversation.Model = model.Id;
        conversation.UpdatedAt = answeredAt;
        await db.SaveChangesAsync(cancellationToken);
        await RecordTurnAsync(userId, conversationId, said, answer, tools, core, cancellationToken);

        foreach (var observer in observers ?? [])
        {
            await observer.AnsweredAsync(turn, new TurnUsage(answerModel.Id, inputTokens, outputTokens, spend.CostUsd));
        }

        logger.LogInformation("[ ConversationService ] Message {MessageId} in conversation {ConversationId} answered by {Model} in {ElapsedMs}ms ({InputTokens} in, {OutputTokens} out, {ToolCalls} memory actions, {FailedToolCalls} refused, check {Check}, retried {Retried}, escalated {Escalated}, unverified {Unverified})", userMessage.Id, conversationId, answerModel.Id, stopwatch.ElapsedMilliseconds, inputTokens, outputTokens, steps.Count, steps.Count(s => s.Kind == StepDto.Failed), checkOutcome, retried, escalated, unverified);
        return Answered(conversation, userMessage, answer, usage);
    }

    private sealed record HistoryMessage(Guid Id, string Role, string Content);

    /// <summary>The first words of the prompt that summarizes a long conversation: a scripted model in the tests recognizes it by them.</summary>
    public const string SummaryMarker = "Filum summary.";

    private const string SummaryText = "Summary of the earlier part of this conversation";

    /// <summary>
    /// The conversation as the model gets it (spec 030). Within <see cref="MemoryOptions.MaxHistoryChars"/> it is sent
    /// whole. Past it, the latest messages that fit are sent, after a summary of the rest: made by the default model, kept
    /// as an event of the conversation, and made again only when the part it does not cover has grown. Every message
    /// stays in the log, where events_search finds it.
    /// </summary>
    private async Task<List<ChatMessage>> FitHistoryAsync(Guid userId, Guid conversationId, IReadOnlyList<HistoryMessage> history, TurnSpend spend, CancellationToken cancellationToken)
    {
        static ChatMessage Chat(HistoryMessage m) => new(m.Role == ConversationMessage.AssistantRole ? ChatRole.Assistant : ChatRole.User, m.Content);
        var budget = memoryOptions.Value.MaxHistoryChars;
        if (history.Sum(m => m.Content.Length) <= budget)
        {
            return history.Select(Chat).ToList();
        }

        // The latest messages within three quarters of the budget, the person's newest always; the rest is summarized.
        var keep = 0;
        var kept = 0;
        while (keep < history.Count && (keep == 0 || kept + history[^(keep + 1)].Content.Length <= budget * 3 / 4))
        {
            kept += history[^(keep + 1)].Content.Length;
            keep++;
        }

        var hidden = history.Take(history.Count - keep).ToList();
        var summaries = await memoryService.EventsAsync(userId, new EventQuery(Kinds: [MemoryEventKind.Derived], ConversationId: conversationId), cancellationToken);
        var last = summaries.LastOrDefault(e => e.Text.StartsWith(SummaryText, StringComparison.Ordinal));
        var covered = last is null ? 0 : hidden.FindIndex(m => m.Id == last.MessageId) + 1;
        var summary = last?.Text;
        var uncovered = hidden.Skip(covered).ToList();
        if (uncovered.Sum(m => m.Content.Length) > budget / 4 || summary is null)
        {
            summary = await SummarizeAsync(userId, conversationId, last?.Text, uncovered, hidden.Count, spend, cancellationToken) ?? summary;
        }

        var note = summary is null
            ? $"[Filum platform note] The first {hidden.Count} messages of this conversation are not shown here; events_search finds them."
            : $"[Filum platform note] {summary}\n(The full text of these messages is kept: events_search finds it.)";
        return [new ChatMessage(ChatRole.User, note), .. history.Skip(hidden.Count).Select(Chat)];
    }

    /// <summary>A summary of the earlier messages, folding in the previous one; kept as an event. Null when it fails.</summary>
    private async Task<string?> SummarizeAsync(Guid userId, Guid conversationId, string? previous, IReadOnlyList<HistoryMessage> messages, int hiddenCount, TurnSpend spend, CancellationToken cancellationToken)
    {
        var model = modelCatalog.Default;
        var text = string.Join("\n\n", messages.Select(m => $"{(m.Role == ConversationMessage.AssistantRole ? "Assistant" : "Person")}: {m.Content}"));
        try
        {
            var response = await chatClientProvider!.Get(model.Id).GetResponseAsync(
                [new ChatMessage(ChatRole.User, $"{SummaryMarker} Summarize the earlier part of a conversation between a person and their assistant, for the assistant to continue it: who said what that matters, facts, numbers, dates, decisions, open questions. At most 300 words, plain sentences, in the conversation's language.\n"
                    + (previous is null ? string.Empty : $"\nThe summary so far:\n<<<\n{previous}\n>>>\n") + $"\nThe messages to add:\n<<<\n{text}\n>>>")],
                cancellationToken: cancellationToken);
            spend.Add(model, response.Usage);
            var summary = $"{SummaryText} ({hiddenCount} messages): {response.Text?.Trim()}";
            await memoryService.RecordAsync(userId, new NewMemoryEvent(MemoryEventKind.Derived, MemoryEventSource.Agent, summary,
                ConversationId: conversationId, MessageId: messages.Count > 0 ? messages[^1].Id : null), cancellationToken);
            return summary;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("[ ConversationService ] The summary of conversation {ConversationId} ({Model}) failed: {ExceptionType}", conversationId, model.Id, exception.GetType().Name);
            return null;
        }
    }

    /// <summary>The person's message as a <c>said</c> event, once: a resend of an unanswered message keeps the first one.</summary>
    private async Task<MemoryEvent> SaidAsync(Guid userId, Guid conversationId, ConversationMessage message, CancellationToken cancellationToken)
    {
        var existing = await memoryService.EventsAsync(userId, new EventQuery(Kinds: [MemoryEventKind.Said], ConversationId: conversationId, MessageId: message.Id), cancellationToken);
        return existing.Count > 0
            ? existing[0]
            : await memoryService.RecordAsync(userId, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, message.Content, message.CreatedAt, conversationId, message.Id), cancellationToken);
    }

    /// <summary>
    /// What the turn did, in the log: the answer; the changes it made, with the person's message as their source; and,
    /// when the core's rules changed, the instruction the person gave (spec 030).
    /// </summary>
    private async Task RecordTurnAsync(Guid userId, Guid conversationId, MemoryEvent said, ConversationMessage answer, MemoryTools tools, string coreBefore, CancellationToken cancellationToken)
    {
        await memoryService.RecordAsync(userId, new NewMemoryEvent(MemoryEventKind.Answered, MemoryEventSource.Agent, answer.Content, answer.CreatedAt, conversationId, answer.Id), cancellationToken);
        var revisions = tools.Revisions;
        if (revisions.Count == 0)
        {
            return;
        }

        var paths = tools.Steps.Where(s => s.Kind == ToolStep.Wrote && s.Path is not null).Select(s => s.Path!).Distinct().ToList();
        await memoryService.RecordAsync(userId, new NewMemoryEvent(MemoryEventKind.Derived, MemoryEventSource.Agent, $"Changed {string.Join(", ", paths)}",
            ConversationId: conversationId, MessageId: said.MessageId, Sources: [said.Id], Revisions: revisions), cancellationToken);

        if (paths.Contains(MemoryPaths.CorePath) && PlatformInstructions.RulesOf(coreBefore) != PlatformInstructions.RulesOf(await memoryService.EnsureCoreAsync(userId, cancellationToken)))
        {
            await memoryService.RecordAsync(userId, new NewMemoryEvent(MemoryEventKind.Told, MemoryEventSource.Chat, said.Text, said.OccurredAt,
                conversationId, said.MessageId, Sources: [said.Id]), cancellationToken);
        }
    }

    private TimeZoneInfo TimeZoneOf(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            logger.LogWarning("[ ConversationService ] The time zone {TimeZone} is unknown: using UTC", id);
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>For a service built without DI (tests): host tools and gates get no services.</summary>
    private sealed class EmptyServices : IServiceProvider
    {
        public static EmptyServices Instance { get; } = new();

        public object? GetService(Type serviceType) => null;
    }

    private static string? Validate(SendMessageRequest request)
    {
        if (request.Id == Guid.Empty)
        {
            return "The message id is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return "The message cannot be empty.";
        }

        if (request.Content.Length > MaxContentLength)
        {
            return $"The message is too long (maximum {MaxContentLength} characters).";
        }

        return null;
    }

    private static TurnResult Answered(Conversation conversation, ConversationMessage userMessage, ConversationMessage answer, UsageRecord? usage) =>
        new(TurnOutcome.Answered, new SendMessageResponse(
            new ConversationDto(conversation.Id, conversation.Title, conversation.Model, Conversation.PreviewFrom(answer.Content), conversation.CreatedAt, conversation.UpdatedAt),
            ToDto(userMessage),
            ToDto(answer),
            usage is null ? null : new UsageDto(usage.InputTokens, usage.OutputTokens, usage.CostUsd)));

    /// <summary>One run of the agent with the turn's tools; its usage is added to the turn at the model's own prices.</summary>
    private async Task<string?> RunAgentAsync(ModelDefinition model, string instructions, IList<AITool> tools, IReadOnlyList<ChatMessage> messages, TurnSpend spend, CancellationToken cancellationToken)
    {
        // The function-invocation loop runs the tools and adds up the usage of every model call of the run.
        var agent = chatClientProvider!.Get(model.Id)
            .AsBuilder()
            .UseFunctionInvocation(configure: loop => loop.MaximumIterationsPerRequest = memoryOptions.Value.MaxToolCallsPerTurn + 5)
            .Build()
            .AsAIAgent(instructions: instructions, name: Agent.Name, tools: tools);
        var run = await agent.RunAsync(messages, cancellationToken: cancellationToken);
        spend.Add(model, run.Usage);
        return run.Text?.Trim();
    }

    /// <summary>What a turn used: every run and every check, each at its own model's prices.</summary>
    private sealed class TurnSpend
    {
        public int InputTokens { get; private set; }

        public int OutputTokens { get; private set; }

        public decimal CostUsd { get; private set; }

        public bool UsageReported { get; private set; } = true;

        public void Add(ModelDefinition model, UsageDetails? usage)
        {
            if (usage?.InputTokenCount is null || usage.OutputTokenCount is null)
            {
                UsageReported = false;
            }

            var input = (int)(usage?.InputTokenCount ?? 0);
            var output = (int)(usage?.OutputTokenCount ?? 0);
            InputTokens += input;
            OutputTokens += output;
            CostUsd += ModelCatalog.CostUsd(model, input, output);
        }

        public void Add(ClaimVerdict check)
        {
            InputTokens += check.InputTokens;
            OutputTokens += check.OutputTokens;
            CostUsd += check.CostUsd;
        }
    }

    private async Task RollBackAsync(Guid userId, MemoryTools tools)
    {
        var revisions = tools.Revisions;
        if (revisions.Count > 0)
        {
            await memoryService.RollbackAsync(userId, revisions, CancellationToken.None);
            logger.LogInformation("[ ConversationService ] {Revisions} memory changes of a failed turn of user {UserId} were rolled back", revisions.Count, userId);
        }
    }

    /// <summary>
    /// Saves the skill an answer proposed, as the person, and marks the proposal saved. Missing when the message is
    /// not the person's or proposes nothing; refused when the proposal was already decided or the name is taken.
    /// </summary>
    public async Task<MemoryOutcome<MemoryChange>> AcceptProposalAsync(Guid userId, Guid messageId, CancellationToken cancellationToken)
    {
        var (message, proposal) = await OpenProposalAsync(userId, messageId, cancellationToken);
        if (proposal is null)
        {
            return MemoryOutcome<MemoryChange>.Missing("There is no skill proposal on that message.");
        }

        if (proposal.Status != SkillProposalDto.Open)
        {
            return MemoryOutcome<MemoryChange>.Refused($"This proposal was already {proposal.Status}.");
        }

        var saved = await memoryService.SaveSkillAsync(userId, MemoryActor.Person, new Skill(proposal.Name, proposal.Description, proposal.When, true, proposal.Steps), replace: false, cancellationToken);
        if (!saved.IsRefused)
        {
            message!.ProposalJson = JsonSerializer.Serialize(proposal with { Status = SkillProposalDto.Saved }, StepsJson);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("[ ConversationService ] The skill proposal of message {MessageId} was saved by user {UserId}", messageId, userId);
        }

        return saved;
    }

    /// <summary>Marks a proposal declined; false when the message is not the person's, proposes nothing, or was already decided.</summary>
    public async Task<bool?> DeclineProposalAsync(Guid userId, Guid messageId, CancellationToken cancellationToken)
    {
        var (message, proposal) = await OpenProposalAsync(userId, messageId, cancellationToken);
        if (proposal is null)
        {
            return null;
        }

        if (proposal.Status != SkillProposalDto.Open)
        {
            return false;
        }

        message!.ProposalJson = JsonSerializer.Serialize(proposal with { Status = SkillProposalDto.Declined }, StepsJson);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<(ConversationMessage? Message, SkillProposalDto? Proposal)> OpenProposalAsync(Guid userId, Guid messageId, CancellationToken cancellationToken)
    {
        var message = await db.Set<ConversationMessage>()
            .Where(m => m.Id == messageId && m.ProposalJson != null && db.Set<Conversation>().Any(c => c.Id == m.ConversationId && c.UserId == userId))
            .FirstOrDefaultAsync(cancellationToken);
        return message is null ? (null, null) : (message, JsonSerializer.Deserialize<SkillProposalDto>(message.ProposalJson!, StepsJson));
    }

    /// <summary>
    /// The files that at least two earlier turns of this conversation changed, not through a skill: the sign of a
    /// request the person repeats. Nothing once a skill was proposed in the conversation, so the person is asked once.
    /// The core and the skills themselves do not count.
    /// </summary>
    private async Task<IReadOnlyList<string>> RepeatedPathsAsync(Guid conversationId, long beforeSequence, CancellationToken cancellationToken)
    {
        var earlier = await db.Set<ConversationMessage>()
            .Where(m => m.ConversationId == conversationId && m.Role == ConversationMessage.AssistantRole && m.Sequence < beforeSequence)
            .Select(m => new { m.StepsJson, m.ProposalJson })
            .ToListAsync(cancellationToken);
        if (earlier.Any(m => m.ProposalJson != null))
        {
            return [];
        }

        return earlier
            .Select(m => m.StepsJson is null ? [] : JsonSerializer.Deserialize<List<StepDto>>(m.StepsJson, StepsJson) ?? [])
            .Where(steps => !steps.Any(s => s.Description.StartsWith("Used skill ", StringComparison.Ordinal)))
            .SelectMany(steps => steps
                .Where(s => s.Kind == StepDto.Wrote && s.Path is not null && !MemoryPaths.IsCore(s.Path) && !Skills.IsSkillPath(s.Path))
                .Select(s => s.Path!)
                .Distinct())
            .GroupBy(path => path)
            .Where(g => g.Count() >= 2)
            .Select(g => g.Key)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>A skill saved in the chat ("yes, save it") settles the open proposal with its name in the same conversation.</summary>
    private async Task MarkProposalsSavedAsync(Guid conversationId, IReadOnlyList<string> savedSkills, CancellationToken cancellationToken)
    {
        if (savedSkills.Count == 0)
        {
            return;
        }

        var proposing = await db.Set<ConversationMessage>().Where(m => m.ConversationId == conversationId && m.ProposalJson != null).ToListAsync(cancellationToken);
        foreach (var message in proposing)
        {
            var proposal = JsonSerializer.Deserialize<SkillProposalDto>(message.ProposalJson!, StepsJson)!;
            if (proposal.Status == SkillProposalDto.Open && savedSkills.Contains(proposal.Name))
            {
                message.ProposalJson = JsonSerializer.Serialize(proposal with { Status = SkillProposalDto.Saved }, StepsJson);
            }
        }
    }

    private static MessageDto ToDto(ConversationMessage message) =>
        new(message.Id, message.Role, message.Content, message.Model, message.CreatedAt,
            message.StepsJson is null ? [] : JsonSerializer.Deserialize<List<StepDto>>(message.StepsJson, StepsJson) ?? [],
            message.Unverified,
            message.ProposalJson is null ? null : JsonSerializer.Deserialize<SkillProposalDto>(message.ProposalJson, StepsJson));
}
