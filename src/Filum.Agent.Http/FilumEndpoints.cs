using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Filum.Agent.Http;

/// <summary>
/// The version of the endpoint groups' contract (spec 019). Within a version a change may only add (an optional field,
/// a route, an optional parameter); anything else is a new version, mapped beside the old one. See docs/api/README.md.
/// </summary>
public static class FilumApi
{
    public const string Version = "1";

    /// <summary>The header every response of the groups carries, so a client can check what it talks to.</summary>
    public const string Header = "Filum-Api-Version";

    internal static void Mark(HttpContext http) => http.Response.Headers[Header] = Version;
}

/// <summary>
/// The turn's endpoints as groups a host maps one by one, under the prefix it wants, on a group that went through
/// <see cref="PersonFilter.RequirePerson"/> (except models, which name no person). Every call works for the request's
/// person only: another person's conversation, file, revision or message answers 404, like one that does not exist.
/// Every route has a stable name (its OpenAPI operationId), a summary, a tag and its responses: the contract of
/// <see cref="FilumApi.Version"/>.
/// </summary>
public static class FilumEndpoints
{
    private const string InvalidConversationId = "The conversation id must be a UUID.";
    private const string ConversationNotFound = "The conversation was not found.";

    /// <summary>Conversations and their messages: <c>/conversations</c>, <c>/conversations/{id}/messages</c>.</summary>
    public static RouteGroupBuilder MapFilumConversations(this RouteGroupBuilder api)
    {
        api.MapGet("/conversations", async (HttpContext http, ConversationService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.ListAsync(http.PersonId(), cancellationToken)))
            .Contract("filum.conversations.list", "The person's conversations, the most recent first.", "conversations")
            .Produces<List<ConversationDto>>();

        api.MapDelete("/conversations/{conversationId}", async (string conversationId, HttpContext http, ConversationService service, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(conversationId, out var id))
                {
                    return Results.Problem(title: InvalidConversationId, statusCode: StatusCodes.Status400BadRequest);
                }

                return await service.DeleteAsync(http.PersonId(), id, cancellationToken)
                    ? Results.NoContent()
                    : Results.Problem(title: ConversationNotFound, statusCode: StatusCodes.Status404NotFound);
            })
            .Contract("filum.conversations.delete", "Delete a conversation and its messages, with what was said in the memory's log; what the memory made of it and the usage stay.", "conversations")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/conversations/{conversationId}/messages", async (string conversationId, HttpContext http, ConversationService service, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(conversationId, out var id))
                {
                    return Results.Problem(title: InvalidConversationId, statusCode: StatusCodes.Status400BadRequest);
                }

                var messages = await service.GetMessagesAsync(http.PersonId(), id, cancellationToken);
                return messages is null
                    ? Results.Problem(title: ConversationNotFound, statusCode: StatusCodes.Status404NotFound)
                    : Results.Ok(messages);
            })
            .Contract("filum.conversations.messages", "The messages of a conversation in order, each answer with its steps and proposal.", "conversations")
            .Produces<List<MessageDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPost("/conversations/{conversationId}/messages", async (string conversationId, SendMessageRequest request, HttpContext http, ConversationService service, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(conversationId, out var id))
                {
                    return Results.Problem(title: InvalidConversationId, statusCode: StatusCodes.Status400BadRequest);
                }

                var result = await service.SendAsync(http.PersonId(), id, request, cancellationToken);
                return result.Outcome switch
                {
                    TurnOutcome.Answered => Results.Ok(result.Response),
                    TurnOutcome.Invalid => Results.Problem(title: result.Error, statusCode: StatusCodes.Status400BadRequest),
                    TurnOutcome.NotFound => Results.Problem(title: result.Error, statusCode: StatusCodes.Status404NotFound),
                    TurnOutcome.BudgetReached => Results.Problem(title: result.Error, statusCode: StatusCodes.Status402PaymentRequired),
                    TurnOutcome.NotConfigured => Results.Problem(title: result.Error, statusCode: StatusCodes.Status503ServiceUnavailable),
                    TurnOutcome.Refused => Results.Problem(title: result.Error, statusCode: result.Status ?? StatusCodes.Status403Forbidden),
                    _ => Results.Problem(title: result.Error, statusCode: StatusCodes.Status502BadGateway)
                };
            })
            .Contract("filum.conversations.send", "Send the person's message and get the answer; a conversation id not seen before starts one. Sending the same message id again returns the stored answer.", "conversations")
            .Produces<SendMessageResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status402PaymentRequired)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return api;
    }

    /// <summary>The person's usage this month: <c>/usage</c>.</summary>
    public static RouteGroupBuilder MapFilumUsage(this RouteGroupBuilder api)
    {
        api.MapGet("/usage", async (HttpContext http, UsageService usage, CancellationToken cancellationToken) =>
                Results.Ok(await usage.GetCurrentMonthAsync(http.PersonId(), cancellationToken)))
            .Contract("filum.usage.month", "What the person spent this calendar month (UTC), by model.", "usage")
            .Produces<MonthlyUsageDto>();

        return api;
    }

    /// <summary>
    /// The person's own way into their memory, <c>/memory/…</c>: the same <see cref="MemoryService"/> the agent uses,
    /// with the person as author, so every check applies. Paths travel as the <c>path</c> query parameter.
    /// </summary>
    public static RouteGroupBuilder MapFilumMemory(this RouteGroupBuilder api)
    {
        var memory = api.MapGroup("/memory");

        memory.MapGet("/files", async (HttpContext http, MemoryService service, CancellationToken cancellationToken) =>
                Answer(await service.ListAsync(http.PersonId(), "/", includePrivate: true, cancellationToken), files => files.Select(ToDto).ToList()))
            .Contract("filum.memory.files", "Every file of the person's memory, private ones included, without content.", "memory")
            .Produces<List<MemoryFileDto>>();

        memory.MapGet("/file", async (string? path, HttpContext http, MemoryService service, CancellationToken cancellationToken) =>
            {
                var file = await service.GetFileAsync(http.PersonId(), path, cancellationToken);
                if (file.IsRefused)
                {
                    return Answer(file, _ => file);
                }

                var origin = await service.GetOriginAsync(http.PersonId(), path, cancellationToken);
                return Answer(origin, o =>
                {
                    var info = file.Value!.Info;
                    return new MemoryFileDetailDto(info.Path, Kind(info.Path), info.SizeBytes, info.LineCount, info.Sensitivity, info.UpdatedAt, info.Header, info.RowCount,
                        file.Value.Content, new MemoryOriginDto(o.Author, o.ConversationTitle, o.CreatedAt));
                });
            })
            .Contract("filum.memory.file", "One file with its content and where it came from.", "memory")
            .Produces<MemoryFileDetailDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        memory.MapPut("/file", async (string? path, WriteMemoryFileRequest request, HttpContext http, MemoryService service, CancellationToken cancellationToken) =>
                Answer(await service.WriteAsync(http.PersonId(), MemoryActor.Person, path, request.Content, cancellationToken), ToDto))
            .Contract("filum.memory.write", "Write a whole file as the person; it is checked like any change and gets a revision.", "memory")
            .Produces<MemoryChangeDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        memory.MapPut("/file/sensitivity", async (string? path, SetSensitivityRequest request, HttpContext http, MemoryService service, CancellationToken cancellationToken) =>
                Answer(await service.SetSensitivityAsync(http.PersonId(), MemoryActor.Person, path, request.Level, cancellationToken), ToDto))
            .Contract("filum.memory.sensitivity", "Set a file's sensitivity: normal, sensitive or private.", "memory")
            .Produces<MemoryChangeDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        memory.MapDelete("/file", async (string? path, HttpContext http, MemoryService service, CancellationToken cancellationToken) =>
            {
                var deleted = await service.DeleteAsync(http.PersonId(), MemoryActor.Person, path, cancellationToken);
                return deleted.IsRefused ? Answer(deleted, _ => deleted) : Results.NoContent();
            })
            .Contract("filum.memory.delete", "Delete a file; its history stays and the deletion can be undone.", "memory")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        memory.MapGet("/file/history", async (string? path, HttpContext http, MemoryService service, CancellationToken cancellationToken) =>
                Answer(await service.GetHistoryAsync(http.PersonId(), path, cancellationToken),
                    history => history.Select(r => new MemoryRevisionDto(r.Id, r.Operation, r.Author, r.ConversationTitle, r.Summary, r.CreatedAt)).ToList()))
            .Contract("filum.memory.history", "Every change of a file, the newest first, with who made it.", "memory")
            .Produces<List<MemoryRevisionDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        memory.MapGet("/revisions/{id:long}", async (long id, HttpContext http, MemoryService service, CancellationToken cancellationToken) =>
                Answer(await service.GetRevisionAsync(http.PersonId(), id, cancellationToken), v => new MemoryVersionDto(v.Id, v.Path, v.Content, v.CreatedAt, v.Deleted)))
            .Contract("filum.memory.revision", "A file as it was after one change.", "memory")
            .Produces<MemoryVersionDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        memory.MapPost("/revisions/{id:long}/restore", async (long id, HttpContext http, MemoryService service, CancellationToken cancellationToken) =>
                Answer(await service.RestoreAsync(http.PersonId(), MemoryActor.Person, id, cancellationToken), ToDto))
            .Contract("filum.memory.restore", "Bring a file back to how it was after one change, as a new change.", "memory")
            .Produces<MemoryChangeDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        memory.MapPost("/revisions/{id:long}/undo", async (long id, HttpContext http, MemoryService service, CancellationToken cancellationToken) =>
                Answer(await service.UndoAsync(http.PersonId(), MemoryActor.Person, id, cancellationToken), ToDto))
            .Contract("filum.memory.undo", "Undo one change, as a new change.", "memory")
            .Produces<MemoryChangeDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        memory.MapPost("/consolidate", async (HttpContext http, ConsolidationService consolidation, CancellationToken cancellationToken) =>
            {
                var (outcome, result) = await consolidation.RunAsync(http.PersonId(), cancellationToken);
                return outcome switch
                {
                    ConsolidationOutcome.Done => Results.Ok(new ConsolidationResultDto(result!.Events, result.Changes, result.Proposals, new UsageDto(result.InputTokens, result.OutputTokens, result.CostUsd))),
                    ConsolidationOutcome.BudgetReached => Results.Problem(title: "The monthly budget has been reached.", statusCode: StatusCodes.Status402PaymentRequired),
                    ConsolidationOutcome.NotConfigured => Results.Problem(title: "No model provider is configured.", statusCode: StatusCodes.Status503ServiceUnavailable),
                    ConsolidationOutcome.Busy => Results.Problem(title: "A pass for this person is already running.", statusCode: StatusCodes.Status409Conflict),
                    _ => Results.Problem(title: "The pass failed; its events wait for the next one.", statusCode: StatusCodes.Status502BadGateway)
                };
            })
            .Contract("filum.memory.consolidate", "Tidy the memory now: a pass over what was said since the last one, adding what the conversations did not write.", "memory")
            .Produces<ConsolidationResultDto>()
            .ProducesProblem(StatusCodes.Status402PaymentRequired)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        memory.MapPost("/forget", async (ForgetRequest request, HttpContext http, ConversationService conversations, CancellationToken cancellationToken) =>
                await conversations.ForgetAsync(http.PersonId(), request.ConversationId, cancellationToken) is { } forgotten
                    ? Results.Ok(new ForgetResultDto(forgotten.Events, forgotten.Files, forgotten.FactRows))
                    : Results.Problem(title: ConversationNotFound, statusCode: StatusCodes.Status404NotFound))
            .Contract("filum.memory.forget", "Forget a conversation for good: what was said in it, what the memory made only of it, and the conversation. It cannot be undone.", "memory")
            .Produces<ForgetResultDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }

    /// <summary>
    /// The person's skills and the proposals the agent made, <c>/skills/…</c>. Skills are memory files: reading, editing,
    /// history and deletion go through the memory group; this adds the list and the decision on a proposal.
    /// </summary>
    public static RouteGroupBuilder MapFilumSkills(this RouteGroupBuilder api)
    {
        var skills = api.MapGroup("/skills");

        skills.MapGet("", async (HttpContext http, MemoryService memory, CancellationToken cancellationToken) =>
                Results.Ok((await memory.ListSkillsAsync(http.PersonId(), includePrivate: true, cancellationToken))
                    .OrderBy(s => s.Skill.Name, StringComparer.Ordinal)
                    .Select(s => new SkillDto(s.Skill.Name, s.Skill.Description, s.Skill.When, s.Skill.Enabled, s.Path, s.Sensitivity, s.UpdatedAt))
                    .ToList()))
            .Contract("filum.skills.list", "The person's skills by name, on and off.", "skills")
            .Produces<List<SkillDto>>();

        skills.MapPost("/proposals/{messageId:guid}/accept", async (Guid messageId, HttpContext http, ConversationService conversations, CancellationToken cancellationToken) =>
            {
                var saved = await conversations.AcceptProposalAsync(http.PersonId(), messageId, cancellationToken);
                return saved.IsMissing ? Results.Problem(title: saved.Refusal, statusCode: StatusCodes.Status404NotFound)
                    : saved.IsRefused ? Results.Problem(title: saved.Refusal, statusCode: StatusCodes.Status400BadRequest)
                    : Results.Ok(new MemoryChangeDto(saved.Value!.RevisionId, saved.Value.Path, saved.Value.LineCount, saved.Value.RowCount));
            })
            .Contract("filum.skills.accept", "Save the skill an answer proposed.", "skills")
            .Produces<MemoryChangeDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        skills.MapPost("/proposals/{messageId:guid}/decline", async (Guid messageId, HttpContext http, ConversationService conversations, CancellationToken cancellationToken) =>
                await conversations.DeclineProposalAsync(http.PersonId(), messageId, cancellationToken) switch
                {
                    null => Results.Problem(title: "There is no skill proposal on that message.", statusCode: StatusCodes.Status404NotFound),
                    false => Results.Problem(title: "This proposal was already decided.", statusCode: StatusCodes.Status400BadRequest),
                    true => Results.NoContent()
                })
            .Contract("filum.skills.decline", "Decline the skill an answer proposed; nothing is saved.", "skills")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }

    /// <summary>The models a person may choose, <c>/models</c>; it names no person, so it can be mapped without login.</summary>
    public static RouteGroupBuilder MapFilumModels(this RouteGroupBuilder api)
    {
        api.MapGet("/models", (ModelCatalog catalog) => catalog.Models
                .Select(m => new ModelDto(m.Id, m.Name, m.Description, m.InputPricePerMillionUsd, m.OutputPricePerMillionUsd, m.Id == catalog.Default.Id)))
            .Contract("filum.models.list", "The models a message may name, with their prices; one is the default.", "models")
            .Produces<List<ModelDto>>();

        return api;
    }

    /// <summary>A route of the contract: its stable name (the operationId), summary and tag, and the version header.</summary>
    private static RouteHandlerBuilder Contract(this RouteHandlerBuilder route, string name, string summary, string tag) =>
        route.WithName(name)
            .WithSummary(summary)
            .WithTags(tag)
            .AddEndpointFilter(async (context, next) =>
            {
                FilumApi.Mark(context.HttpContext);
                return await next(context);
            });

    private static IResult Answer<T>(MemoryOutcome<T> outcome, Func<T, object> body) where T : class =>
        outcome.IsMissing ? Results.Problem(title: outcome.Refusal, statusCode: StatusCodes.Status404NotFound)
        : outcome.IsRefused ? Results.Problem(title: outcome.Refusal, statusCode: StatusCodes.Status400BadRequest)
        : Results.Ok(body(outcome.Value!));

    private static string Kind(string path) => MemoryPaths.KindOf(path) == MemoryFileKind.Collection ? "collection" : "document";

    private static MemoryFileDto ToDto(MemoryFileInfo f) => new(f.Path, Kind(f.Path), f.SizeBytes, f.LineCount, f.Sensitivity, f.UpdatedAt, f.Header, f.RowCount);

    private static MemoryChangeDto ToDto(MemoryChange c) => new(c.RevisionId, c.Path, c.LineCount, c.RowCount);
}
