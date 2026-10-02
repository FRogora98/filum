using Filum.Agent.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using static Filum.Agent.Tests.Infrastructure.FakeChatClient;

namespace Filum.Agent.Tests;

/// <summary>Consolidation in the hosted product (spec 030), through the sample host.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ConsolidationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_pass_on_demand_adds_what_the_turn_did_not_write_with_its_events_as_sources_and_it_can_be_undone()
    {
        var llm = new FakeChatClient { Reply = _ => "Lovely." };
        await using var host = new SampleHostFactory(postgres, llm);
        var id = Guid.NewGuid();
        var person = host.Person(id);
        await Send(person, Guid.NewGuid(), "We have four plants at home now, not three.");
        var spoken = Events(host, id).Where(e => e.Kind is MemoryEventKind.Said or MemoryEventKind.Answered).Select(e => e.Id).ToList();
        llm.Then(Call("fact_record", new Dictionary<string, object?> { ["subject"] = "the person", ["attribute"] = "plants", ["value"] = "4", ["sources"] = new[] { spoken[0] } }));

        var response = await person.PostAsync("/sample/memory/consolidate", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new ConsolidationResultDto(2, 1, 0), await response.Content.ReadFromJsonAsync<ConsolidationResultDto>());
        Assert.StartsWith(Consolidation.Instructions[..40], llm.InstructionsOf(llm.Calls.Count - 1));
        Assert.Contains($"[event {spoken[0]} ·", string.Join("\n", llm.Calls[^1].Select(m => m.Text)));
        var pass = Events(host, id).Single(e => e.Source == MemoryEventSource.Consolidation && e.Text == Consolidation.PassText);
        Assert.Equal(spoken, pass.Sources);
        var revision = Assert.Single(pass.Revisions);

        var history = await person.GetFromJsonAsync<List<MemoryRevisionDto>>($"/sample/memory/file/history?path={Uri.EscapeDataString(Facts.Path)}");
        Assert.Equal(MemoryAuthor.Consolidation, Assert.Single(history!).Author);
        Assert.Equal(HttpStatusCode.OK, (await person.PostAsync($"/sample/memory/revisions/{revision}/undo", null)).StatusCode);

        var again = await person.PostAsync("/sample/memory/consolidate", null);
        Assert.Equal(new ConsolidationResultDto(0, 0, 0), await again.Content.ReadFromJsonAsync<ConsolidationResultDto>());
    }

    [Fact]
    public async Task The_worker_waits_for_a_quiet_conversation_then_consolidates_it_once()
    {
        var llm = new FakeChatClient { Reply = _ => "Ok." };
        await using var host = new SampleHostFactory(postgres, llm);
        var id = Guid.NewGuid();
        await Send(host.Person(id), Guid.NewGuid(), "The meeting moved to Thursday.");
        var options = Options.Create(new ConsolidationOptions { QuietMinutes = 10, NightlyHourUtc = (DateTimeOffset.UtcNow.Hour + 12) % 24 });
        var worker = new ConsolidationWorker(host.Services.GetRequiredService<IServiceScopeFactory>(), options, NullLogger<ConsolidationWorker>.Instance);

        await worker.PassAsync(DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.NotEmpty(await Pending(host, id));

        await worker.PassAsync(DateTimeOffset.UtcNow.AddMinutes(11), CancellationToken.None);
        Assert.Empty(await Pending(host, id));
        Assert.Single(Events(host, id), e => e.Source == MemoryEventSource.Consolidation);

        await worker.PassAsync(DateTimeOffset.UtcNow.AddMinutes(12), CancellationToken.None);
        Assert.Single(Events(host, id), e => e.Source == MemoryEventSource.Consolidation);
    }

    private static async Task<IReadOnlyList<MemoryEvent>> Pending(SampleHostFactory host, Guid person)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<MemoryService>().PendingAsync(person, 100, CancellationToken.None);
    }

    private static IReadOnlyList<MemoryEvent> Events(SampleHostFactory host, Guid person)
    {
        using var scope = host.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<MemoryService>().EventsAsync(person, new EventQuery(), CancellationToken.None).GetAwaiter().GetResult();
    }

    private static async Task Send(HttpClient person, Guid conversation, string content)
    {
        var response = await person.Send(conversation, content);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }
}
