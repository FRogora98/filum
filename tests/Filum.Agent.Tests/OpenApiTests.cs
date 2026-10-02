using Filum.Agent.Tests.Infrastructure;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Filum.Agent.Tests;

/// <summary>The groups' contract, v1 (spec 019): described, frozen by a snapshot, and named in every response.</summary>
[Collection(PostgresCollection.Name)]
public sealed class OpenApiTests(PostgresFixture postgres)
{
    private static readonly string[] Groups = ["conversations", "usage", "memory", "skills", "models"];

    [Fact]
    public async Task Every_route_has_a_unique_operation_id_a_summary_a_tag_and_its_responses()
    {
        var document = await Document();

        var operations = document["paths"]!.AsObject()
            .SelectMany(path => path.Value!.AsObject().Select(op => (Route: $"{op.Key.ToUpperInvariant()} {path.Key}", Op: op.Value!.AsObject())))
            .ToList();
        Assert.Equal(20, operations.Count);
        Assert.All(operations, o =>
        {
            Assert.StartsWith("filum.", (string?)o.Op["operationId"] ?? string.Empty);
            Assert.False(string.IsNullOrWhiteSpace((string?)o.Op["summary"]), o.Route);
            Assert.Contains((string)o.Op["tags"]![0]!, Groups);
            Assert.NotEmpty(o.Op["responses"]!.AsObject());
        });
        Assert.Equal(operations.Count, operations.Select(o => (string)o.Op["operationId"]!).Distinct().Count());
        foreach (var schema in new[] { "SendMessageResponse", "MessageDto", "StepDto", "MemoryFileDetailDto", "MemoryRevisionDto", "SkillDto", "MonthlyUsageDto" })
        {
            Assert.True(document["components"]!["schemas"]!.AsObject().ContainsKey(schema), schema);
        }
    }

    [Fact]
    public async Task The_description_matches_the_committed_snapshot()
    {
        var text = (await Document()).ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n";
        var snapshot = Path.Combine(SampleHostFactory.Root(), "docs", "api", "openapi-v1.json");

        // A deliberate change to the contract: FILUM_UPDATE_SNAPSHOT=1 dotnet test, then review the diff (docs/api/README.md says what v1 allows).
        if (Environment.GetEnvironmentVariable("FILUM_UPDATE_SNAPSHOT") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(snapshot)!);
            File.WriteAllText(snapshot, text);
        }

        Assert.True(File.Exists(snapshot), "No snapshot yet: run once with FILUM_UPDATE_SNAPSHOT=1 and review it.");
        Assert.Equal(File.ReadAllText(snapshot).Replace("\r\n", "\n"), text);
    }

    [Fact]
    public async Task Every_response_of_the_groups_names_the_contracts_version()
    {
        await using var host = new SampleHostFactory(postgres, new FakeChatClient());
        var person = host.Person(Guid.NewGuid());

        var ok = await person.GetAsync("/sample/conversations");
        var invalid = await person.GetAsync("/sample/conversations/not-a-uuid/messages");
        var anonymous = await host.CreateClient().GetAsync("/sample/usage");
        var models = await host.CreateClient().GetAsync("/sample/models");

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.OK), (ok.StatusCode, invalid.StatusCode, anonymous.StatusCode, models.StatusCode));
        Assert.All(new[] { ok, invalid, anonymous, models }, r => Assert.Equal(FilumApi.Version, Assert.Single(r.Headers.GetValues(FilumApi.Header))));
    }

    private async Task<JsonNode> Document()
    {
        await using var host = new SampleHostFactory(postgres, new FakeChatClient());
        var json = await host.CreateClient().GetStringAsync("/sample/openapi/v1.json");
        var document = JsonNode.Parse(json)!;
        // The address of the server the test happened to run on is not part of the contract.
        document.AsObject().Remove("servers");
        return document;
    }
}
