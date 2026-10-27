using System.Diagnostics;
using System.Text.Json.Nodes;
using Filum.Evals.Mcp;

namespace Filum.Evals.UnitTests.Mcp;

// The harness confound found on 2026-10-19: a session saw the account running it and Claude Code's own memory.
public sealed class ClaudeIsolationTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("filum-evals-home-").FullName;

    public ClaudeIsolationTests()
    {
        Directory.CreateDirectory(Path.Combine(_home, ".claude"));
        var expires = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeMilliseconds();
        var login = new JsonObject { ["claudeAiOauth"] = new JsonObject { ["accessToken"] = "t", ["expiresAt"] = expires }, ["mcpOAuth"] = new JsonObject() };
        File.WriteAllText(Path.Combine(_home, ".claude", ".credentials.json"), login.ToJsonString());
        File.WriteAllText(Path.Combine(_home, ".claude.json"), """{"oauthAccount":{"emailAddress":"someone@example.invalid","accountUuid":"u"}}""");
    }

    public void Dispose() => Directory.Delete(_home, recursive: true);

    [Fact]
    public async Task ApplyAsync_WhenTheModelSeesNothingOfTheAccount_ShouldPointTheSessionAtABlankedCopyWithTheLoginOnly()
    {
        var calls = new List<(ProcessStartInfo Start, string Input)>();
        var isolation = new ClaudeIsolation(null, _home, (start, input, _) =>
        {
            calls.Add((start, input));
            // As Claude Code does at a new folder's first start: it fetches the account's profile.
            File.WriteAllText(Path.Combine(start.Environment["CLAUDE_CONFIG_DIR"]!, ".claude.json"), """{"oauthAccount":{"emailAddress":"someone@example.invalid","accountUuid":"u"}}""");
            return Task.FromResult(input.StartsWith("What is my email", StringComparison.Ordinal) ? "NONE, NONE, NONE, NONE" : "ok");
        });
        var start = new ProcessStartInfo("claude");

        await isolation.ApplyAsync(start, CancellationToken.None);

        var folder = start.Environment["CLAUDE_CONFIG_DIR"]!;
        var account = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, ".claude.json")))!["oauthAccount"]!;
        var login = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, ".credentials.json")))!.AsObject();
        Assert.Equal(("", "u"), (account["emailAddress"]!.GetValue<string>(), account["accountUuid"]!.GetValue<string>()));
        Assert.Equal(["claudeAiOauth"], login.Select(p => p.Key));
        Assert.Equal(("1", "1"), (start.Environment["CLAUDE_CODE_DISABLE_AUTO_MEMORY"], start.Environment["CLAUDE_CODE_DISABLE_CLAUDE_MDS"]));
        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public async Task ApplyAsync_WhenTheProfileComesBackOnceAndIsBlankedAgain_ShouldRun()
    {
        var checks = 0;
        var isolation = new ClaudeIsolation(null, _home, (_, input, _) =>
            Task.FromResult(input.StartsWith("What is my email", StringComparison.Ordinal) && checks++ == 0 ? "Your email is someone@example.invalid." : "NONE"));

        await isolation.ApplyAsync(new ProcessStartInfo("claude"), CancellationToken.None);

        Assert.Equal(2, checks);
    }

    [Fact]
    public async Task ApplyAsync_WhenTheModelStillSeesTheAccount_ShouldRefuseToRun()
    {
        var isolation = new ClaudeIsolation(null, _home, (_, input, _) =>
            Task.FromResult(input.StartsWith("What is my email", StringComparison.Ordinal) ? "Your email is someone@example.invalid." : "ok"));

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => isolation.ApplyAsync(new ProcessStartInfo("claude"), CancellationToken.None));

        Assert.Contains("emailAddress", refused.Message);
        Assert.False(Directory.Exists(isolation.Folder));
    }
}
