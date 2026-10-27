using System.Diagnostics;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Storage.LocalFolder;
using Filum.Engine;

namespace Filum.Mcp.IntegrationTests.Hook;

public sealed class HookLogTests : IDisposable
{
    private static readonly string Server = Path.Combine(AppContext.BaseDirectory, "filum-mcp.dll");
    private readonly string _home = Path.Combine(Path.GetTempPath(), "filum-mcp-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_home))
        {
            Directory.Delete(_home, recursive: true);
        }
        else if (File.Exists(_home))
        {
            File.Delete(_home);
        }
    }

    [Fact]
    public async Task Log_WhenAHookSendsAPrompt_ShouldKeepItAsWhatThePersonSaidWithNothingOnStdout()
    {
        var (code, stdout, _) = await Run("""{"session_id":"s-1","hook_event_name":"UserPromptSubmit","prompt":"I just reached Premier Silver.","cwd":"/tmp"}""");

        Assert.Equal((0, string.Empty), (code, stdout));
        var said = Assert.Single(await Events());
        Assert.Equal((MemoryEventKind.Said, MemoryEventSource.Host, "I just reached Premier Silver."), (said.Kind, said.Source, said.Text));
    }

    [Fact]
    public async Task Log_WhenTheTextIsLong_ShouldKeepItInPartsUnderOneConversationLosingNothing()
    {
        var lines = Enumerable.Range(1, 400).Select(i => $"Line {i}: something the person said, long enough to fill the parts.").ToList();

        var (code, _, _) = await Run(string.Join("\n", lines));

        Assert.Equal(0, code);
        var events = await Events();
        Assert.True(events.Count > 1);
        Assert.All(events, e => Assert.True(e.Text.Length <= McpConstants.Limits.MaxText));
        Assert.Single(events.Select(e => e.ConversationId).Distinct());
        Assert.Equal(string.Join("\n", lines), string.Join("\n", events.Select(e => e.Text)));
    }

    [Fact]
    public async Task Log_WhenTheFolderCannotBeWritten_ShouldKeepNothingButNeverBlockTheMessage()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_home)!);
        File.WriteAllText(_home, "a file where the folder should be");

        var (code, stdout, stderr) = await Run("""{"prompt":"Hello."}""");

        Assert.Equal((0, string.Empty), (code, stdout));
        Assert.Contains("nothing kept", stderr);
    }

    [Fact]
    public async Task Log_WhenTheMemoryHoldsSomethingAboutThePrompt_ShouldAnswerTheHookWithItButNotThePrompt()
    {
        await Run("""{"prompt":"We moved the piano to the attic last week."}""");

        var (code, stdout, _) = await Run("""{"session_id":"s-2","hook_event_name":"UserPromptSubmit","prompt":"Where is the piano now?"}""");

        Assert.Equal(0, code);
        using var answer = System.Text.Json.JsonDocument.Parse(stdout);
        var hook = answer.RootElement.GetProperty("hookSpecificOutput");
        Assert.Equal("UserPromptSubmit", hook.GetProperty("hookEventName").GetString());
        var context = hook.GetProperty("additionalContext").GetString()!;
        Assert.Contains("piano to the attic", context);
        Assert.DoesNotContain("Where is the piano now?", context);
    }

    [Fact]
    public async Task Log_WhenAHostPipesPlainText_ShouldAnswerNothing()
    {
        await Run("We moved the piano to the attic.");

        var (code, stdout, _) = await Run("Where is the piano?");

        Assert.Equal((0, string.Empty), (code, stdout));
    }

    [Fact]
    public async Task Brief_WhenTheMemoryHasACore_ShouldAnswerTheSessionStartHookWithThePerson()
    {
        await Run("""{"prompt":"I planted tomatoes on the balcony."}""");

        var (code, stdout, _) = await Run("""{"hook_event_name":"SessionStart","source":"startup"}""", "brief");

        Assert.Equal(0, code);
        using var answer = System.Text.Json.JsonDocument.Parse(stdout);
        var hook = answer.RootElement.GetProperty("hookSpecificOutput");
        Assert.Equal("SessionStart", hook.GetProperty("hookEventName").GetString());
        Assert.Contains("1 messages kept", hook.GetProperty("additionalContext").GetString());
    }

    [Fact]
    public async Task Brief_WhenTheFolderIsBroken_ShouldAnswerNothingAndNeverFailTheSession()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_home)!);
        File.WriteAllText(_home, "a file where the folder should be");

        var (code, stdout, _) = await Run("{}", "brief");

        Assert.Equal((0, string.Empty), (code, stdout));
    }

    [Fact]
    public async Task Tidy_WhenMuchIsWaiting_ShouldAskOncePerSessionAndNeverLoop()
    {
        for (var i = 0; i < 12; i++)
        {
            await Run($"I said thing number {i}.");
        }

        var first = await Run("""{"session_id":"s-9","hook_event_name":"Stop","stop_hook_active":false}""", "tidy");
        var again = await Run("""{"session_id":"s-9","hook_event_name":"Stop","stop_hook_active":false}""", "tidy");
        var active = await Run("""{"session_id":"s-10","hook_event_name":"Stop","stop_hook_active":true}""", "tidy");

        using var answer = System.Text.Json.JsonDocument.Parse(first.Stdout);
        Assert.Equal("block", answer.RootElement.GetProperty("decision").GetString());
        Assert.Contains("memory_consolidate", answer.RootElement.GetProperty("reason").GetString());
        Assert.Equal((0, string.Empty), (again.Code, again.Stdout));
        Assert.Equal((0, string.Empty), (active.Code, active.Stdout));
    }

    [Fact]
    public async Task Tidy_WhenLittleIsWaiting_ShouldAskNothing()
    {
        await Run("Just one thing.");

        Assert.Equal((0, string.Empty), Pick(await Run("""{"session_id":"s-11","hook_event_name":"Stop"}""", "tidy")));
    }

    private static (int Code, string Stdout) Pick((int Code, string Stdout, string Stderr) run) => (run.Code, run.Stdout);

    private async Task<(int Code, string Stdout, string Stderr)> Run(string input, string command = "log")
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
        foreach (var argument in new[] { Server, command, "--home", _home })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        try
        {
            await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // A hook that cannot open the folder answers and exits before reading its input; the pipe is closed then.
        }

        var stderr = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(TimeSpan.FromSeconds(30)));
        return (process.ExitCode, await stdout, await stderr);
    }

    private async Task<IReadOnlyList<MemoryEvent>> Events()
    {
        using var store = new LocalFolderStore(_home);
        using var engine = FilumEngine.Create(store);
        return await engine.Memory.EventsAsync(store.Owner, new EventQuery(), CancellationToken.None);
    }
}
