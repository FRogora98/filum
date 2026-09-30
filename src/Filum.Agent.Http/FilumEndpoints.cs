using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Filum.Agent.Http;

/// <summary>
/// The turn's endpoints as groups a host maps one by one, under the prefix it wants, on a group that went through
/// <see cref="PersonFilter.RequirePerson"/> (except models, which name no person). Every call works for the request's
/// person only: another person's conversation, file, revision or message answers 404, like one that does not exist.
/// </summary>
public static class FilumEndpoints
{
    private const string InvalidConversationId = "The conversation id must be a UUID.";
    private const string ConversationNotFound = "The conversation was not found.";

    /// <summary>Conversations and their messages: <c>/conversations</c>, <c>/conversations/{id}/messages</c>.</summary>
    public static RouteGroupBuilder MapFilumConversations(this RouteGroupBuilder api)
    {
        api.MapGet("/conversations", async (HttpContext http, ConversationService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(http.PersonId(), cancellationToken)));

        api.MapDelete("/conversations/{conversationId}", async (string conversationId, HttpContext http, ConversationService service, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(conversationId, out var id))
            {
                return Results.Problem(title: InvalidConversationId, statusCode: StatusCodes.Status400BadRequest);
            }

            return await service.DeleteAsync(http.PersonId(), id, cancellationToken)
                ? Results.NoContent()
                : Results.Problem(title: ConversationNotFound, statusCode: StatusCodes.Status404NotFound);
        });

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
        });

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
        });

        return api;
    }

    /// <summary>The person's usage this month: <c>/usage</c>.</summary>
    public static RouteGroupBuilder MapFilumUsage(this RouteGroupBuilder api)
    {
        api.MapGet("/usage", async (HttpContext http, UsageService usage, CancellationToken cancellationToken) =>
            Results.Ok(await usage.GetCurrentMonthAsync(http.PersonId(), cancellationToken)));

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
            Answer(await service.ListAsync(http.PersonId(), "/", includePrivate: true, cancellationToken), files => files.Select(ToDto).ToList()));

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
        });

        memory.MapPut("/file", async (string? path, WriteMemoryFileRequest request, HttpContext http, MemoryService service, CancellationToken cancellationToken) =>
            Answer(await service.WriteAsync(http.PersonId(), MemoryActor.Person, path, request.Content, cancellationToken), ToDto));

        memory.MapPut("/file/sensitivity", async (string? path, SetSensitivityRequest request, HttpContext http, MemoryService service, CancellationToken cancellationToken) =>
            Answer(await service.SetSensitivityAsync(http.PersonId(), MemoryActor.Person, path, request.Level, cancellationToken), ToDto));

        memory.MapDelete("/file", async (string? path, HttpContext http, MemoryService service, CancellationToken cancellationToken) =>
        {
            var deleted = await service.DeleteAsync(http.PersonId(), MemoryActor.Person, path, cancellationToken);
            return deleted.IsRefused ? Answer(deleted, _ => deleted) : Results.NoContent();
        });

        memory.MapGet("/file/history", async (string? path, HttpContext http, MemoryService service, CancellationToken cancellationToken) =>
            Answer(await service.GetHistoryAsync(http.PersonId(), path, cancellationToken),
                history => history.Select(r => new MemoryRevisionDto(r.Id, r.Operation, r.Author, r.ConversationTitle, r.Summary, r.CreatedAt)).ToList()));

        memory.MapGet("/revisions/{id:long}", async (long id, HttpContext http, MemoryService service, CancellationToken cancellationToken) =>
            Answer(await service.GetRevisionAsync(http.PersonId(), id, cancellationToken), v => new MemoryVersionDto(v.Id, v.Path, v.Content, v.CreatedAt, v.Deleted)));

        memory.MapPost("/revisions/{id:long}/restore", async (long id, HttpContext http, MemoryService service, CancellationToken cancellationToken) =>
            Answer(await service.RestoreAsync(http.PersonId(), MemoryActor.Person, id, cancellationToken), ToDto));

        memory.MapPost("/revisions/{id:long}/undo", async (long id, HttpContext http, MemoryService service, CancellationToken cancellationToken) =>
            Answer(await service.UndoAsync(http.PersonId(), MemoryActor.Person, id, cancellationToken), ToDto));

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
                .ToList()));

        skills.MapPost("/proposals/{messageId:guid}/accept", async (Guid messageId, HttpContext http, ConversationService conversations, CancellationToken cancellationToken) =>
        {
            var saved = await conversations.AcceptProposalAsync(http.PersonId(), messageId, cancellationToken);
            return saved.IsMissing ? Results.Problem(title: saved.Refusal, statusCode: StatusCodes.Status404NotFound)
                : saved.IsRefused ? Results.Problem(title: saved.Refusal, statusCode: StatusCodes.Status400BadRequest)
                : Results.Ok(new MemoryChangeDto(saved.Value!.RevisionId, saved.Value.Path, saved.Value.LineCount, saved.Value.RowCount));
        });

        skills.MapPost("/proposals/{messageId:guid}/decline", async (Guid messageId, HttpContext http, ConversationService conversations, CancellationToken cancellationToken) =>
            await conversations.DeclineProposalAsync(http.PersonId(), messageId, cancellationToken) switch
            {
                null => Results.Problem(title: "There is no skill proposal on that message.", statusCode: StatusCodes.Status404NotFound),
                false => Results.Problem(title: "This proposal was already decided.", statusCode: StatusCodes.Status400BadRequest),
                true => Results.NoContent()
            });

        return api;
    }

    /// <summary>The models a person may choose, <c>/models</c>; it names no person, so it can be mapped without login.</summary>
    public static RouteGroupBuilder MapFilumModels(this RouteGroupBuilder api)
    {
        api.MapGet("/models", (ModelCatalog catalog) => catalog.Models
            .Select(m => new ModelDto(m.Id, m.Name, m.Description, m.InputPricePerMillionUsd, m.OutputPricePerMillionUsd, m.Id == catalog.Default.Id)));

        return api;
    }

    private static IResult Answer<T>(MemoryOutcome<T> outcome, Func<T, object> body) where T : class =>
        outcome.IsMissing ? Results.Problem(title: outcome.Refusal, statusCode: StatusCodes.Status404NotFound)
        : outcome.IsRefused ? Results.Problem(title: outcome.Refusal, statusCode: StatusCodes.Status400BadRequest)
        : Results.Ok(body(outcome.Value!));

    private static string Kind(string path) => MemoryPaths.KindOf(path) == MemoryFileKind.Collection ? "collection" : "document";

    private static MemoryFileDto ToDto(MemoryFileInfo f) => new(f.Path, Kind(f.Path), f.SizeBytes, f.LineCount, f.Sensitivity, f.UpdatedAt, f.Header, f.RowCount);

    private static MemoryChangeDto ToDto(MemoryChange c) => new(c.RevisionId, c.Path, c.LineCount, c.RowCount);
}
