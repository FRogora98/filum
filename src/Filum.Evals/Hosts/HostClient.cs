using Filum.Evals.Checks;
using Filum.Evals.Hosts.Wire;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;

namespace Filum.Evals.Hosts;

// Through its public API only: the records it reads are its own copy (Wire/), so the runner needs none of the host's
// code.
internal sealed class HostClient(HttpClient service, HostTarget target)
{
    public async Task<EvalPerson> CreatePersonAsync(CancellationToken cancellationToken)
    {
        var person = EvalPerson.New();
        if (target.PersonHeader is not null)
        {
            return person with { Header = target.PersonHeader };
        }

        using var response = await service.PostAsJsonAsync(target.Register, new RegisterRequest(person.Email, $"eval-{Guid.NewGuid():N}-1"), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Registering a synthetic person at {target.Register} answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }

        var session = await response.Content.ReadFromJsonAsync<RegisteredSession>(cancellationToken)
            ?? throw new InvalidOperationException($"Registering a synthetic person at {target.Register} answered no session.");
        return person with { Token = session.AccessToken };
    }

    // No model for the host's own.
    public Task<HostReply<SendMessageResponse>> SendMessageAsync(EvalPerson person, Guid conversation, string content, string model, CancellationToken cancellationToken) =>
        PostAsync<SendMessageResponse>(person, target.Url($"{EvalsConstants.HostRoutes.Conversations}/{conversation}/messages"),
            JsonContent.Create(new SendMessageRequest(Guid.NewGuid(), content, model == HostTarget.HostModel ? null : model)), cancellationToken);

    public Task<HostReply<ConsolidationPass>> ConsolidateAsync(EvalPerson person, CancellationToken cancellationToken) =>
        PostAsync<ConsolidationPass>(person, target.Url(EvalsConstants.HostRoutes.Consolidate), null, cancellationToken);

    // Unavailable when the host maps no memory group.
    public async Task<MemorySnapshot> ReadMemoryAsync(EvalPerson person, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, target.Url(EvalsConstants.HostRoutes.MemoryFiles), person);
        using var response = await service.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
        {
            return MemorySnapshot.Unavailable;
        }

        var files = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<List<MemoryFileDto>>(cancellationToken) ?? [] : [];
        var details = new List<MemoryFileDetailDto>();
        foreach (var file in files)
        {
            if (await GetAsync<MemoryFileDetailDto>(person, target.Url($"/memory/file?path={Uri.EscapeDataString(file.Path)}"), cancellationToken) is { } detail)
            {
                details.Add(detail);
            }
        }

        return new MemorySnapshot(details);
    }

    public async Task<bool> ConversationExistsAsync(EvalPerson person, Guid conversation, CancellationToken cancellationToken) =>
        (await GetAsync<List<ConversationDto>>(person, target.Url(EvalsConstants.HostRoutes.Conversations), cancellationToken) ?? []).Any(c => c.Id == conversation);

    // Null when it cannot be read.
    public async Task<string?> ReadRawAsync(EvalPerson person, string path, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, path, person);
        using var response = await service.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(cancellationToken) : null;
    }

    private async Task<HostReply<T>> PostAsync<T>(EvalPerson person, string url, HttpContent? content, CancellationToken cancellationToken)
        where T : class
    {
        using var request = Request(HttpMethod.Post, url, person);
        request.Content = content;
        var clock = Stopwatch.StartNew();
        using var response = await service.SendAsync(request, cancellationToken);
        var seconds = clock.Elapsed.TotalSeconds;
        var status = (int)response.StatusCode;
        return response.IsSuccessStatusCode
            ? new HostReply<T>(status, await response.Content.ReadFromJsonAsync<T>(cancellationToken), null, seconds)
            : new HostReply<T>(status, null, $"{status} {await response.Content.ReadAsStringAsync(cancellationToken)}", seconds);
    }

    private async Task<T?> GetAsync<T>(EvalPerson person, string url, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, url, person);
        using var response = await service.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>(cancellationToken) : default;
    }

    private static HttpRequestMessage Request(HttpMethod method, string url, EvalPerson person)
    {
        var request = new HttpRequestMessage(method, url);
        person.Apply(request);
        return request;
    }
}
