using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Filum.Evals;

/// <summary>
/// The growth test of filum-mcp (spec 018): the same scenarios in new sessions of a real agent, in two arms, with only
/// filum-mcp mounted and with no MCP server at all. The agent's own file and memory tools are off in both, so only
/// Filum can remember. Run by hand: every session uses the owner's own agent.
/// </summary>
public static class McpEvalCli
{
    public const string WithFilum = "with filum";
    public const string WithoutFilum = "without";

    public static async Task<int> MainAsync(string[] args)
    {
        const string help = """
            The growth test of filum-mcp: each turn a new session of a real agent, with filum-mcp and without.

              dotnet run --project src/Filum.Evals -- mcp --filum-mcp <path> --max-sessions 20 [options]

              --filum-mcp <path>    the filum-mcp to mount (required): a release's executable, or a build's filum-mcp.dll
              --max-sessions <n>    the most agent sessions of the run (required); a turn is one session per arm
              --force               start even if the scenarios need more sessions (the cap still stops the run)
              --agent-model <name>  the agent's model, for example haiku (default: the agent's own)
              --scenarios-dir <dir> --scenarios <glob> --reps <n> (default 1) --cap <usd> (the judge, default 0.5) --out <dir>
            """;

        var options = Args.Parse(args);
        if (options.ContainsKey("help") || !options.TryGetValue("max-sessions", out var max) || !options.TryGetValue("filum-mcp", out var filumMcp))
        {
            Console.WriteLine(help);
            return options.ContainsKey("help") ? 0 : 1;
        }

        var scenarios = ScenarioLoader.LoadAll(options.GetValueOrDefault("scenarios-dir") ?? Path.Combine(AppContext.BaseDirectory, "scenarios"), options.GetValueOrDefault("scenarios"));
        var repetitions = int.Parse(options.GetValueOrDefault("reps", "1"), CultureInfo.InvariantCulture);
        var sessions = SessionBudget.Needed(scenarios, repetitions);
        var budget = new SessionBudget(int.Parse(max, CultureInfo.InvariantCulture));
        Console.WriteLine($"{scenarios.Count} scenarios × 2 arms × {repetitions} repetitions · {sessions} agent sessions · cap {budget.Max} sessions");
        if (sessions > budget.Max && !options.ContainsKey("force"))
        {
            Console.Error.WriteLine("The run needs more sessions than --max-sessions: pick fewer scenarios or repetitions, or add --force (the cap still stops the run).");
            return 2;
        }

        var agent = new ClaudeCodeAgent(options.GetValueOrDefault("agent-model"));
        filumMcp = Path.GetFullPath(filumMcp);
        var runner = new McpScenarioRunner(agent, CreateJudge(), filumMcp, budget);
        var cap = decimal.Parse(options.GetValueOrDefault("cap", "0.5"), CultureInfo.InvariantCulture);
        var result = await new EvalRun(runner, cap, Console.Out, $"filum-mcp in {agent.Name}{(agent.Model is null ? "" : $" ({agent.Model})")}")
            .RunAsync(scenarios, [WithFilum, WithoutFilum], repetitions, CancellationToken.None);

        var folder = await EvalReport.WriteAsync(result, options.GetValueOrDefault("out", Path.Combine("evals", "results")));
        Console.WriteLine();
        Console.WriteLine(EvalReport.Markdown(result));
        Console.WriteLine($"Written to {Path.GetFullPath(folder)}");
        return 0;
    }

    private static IClaimJudge CreateJudge() => EvalCli.CreateJudge();
}

/// <summary>How many agent sessions a run may still start.</summary>
public sealed class SessionBudget(int max)
{
    public int Max { get; } = max;

    public int Used { get; private set; }

    public static int Needed(IReadOnlyList<Scenario> scenarios, int repetitions) => scenarios.Sum(s => s.TurnCount) * repetitions * 2;

    public bool CanStart(int sessions) => Used + sessions <= Max;

    public void Spend() => Used++;
}

/// <summary>One agent session: a message in, the answer and the tools it called out.</summary>
public sealed record AgentSession(string Answer, IReadOnlyList<string> Tools, string? Error);

/// <summary>An agent run through its command line, one new session per call.</summary>
public interface IAgent
{
    string Name { get; }

    Task<AgentSession> AskAsync(string message, string workingDirectory, string mcpConfig, bool filumMounted, CancellationToken cancellationToken);
}

/// <summary>
/// Claude Code in print mode. Its own tools are off (<c>--tools ""</c>), so it cannot keep notes or memory of its own;
/// only the MCP servers of the given config exist (<c>--strict-mcp-config</c>).
/// </summary>
public sealed class ClaudeCodeAgent(string? model) : IAgent
{
    public string Name => "Claude Code";

    public string? Model => model;

    public async Task<AgentSession> AskAsync(string message, string workingDirectory, string mcpConfig, bool filumMounted, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("claude") { WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
        foreach (var argument in (string[])["-p", message, "--output-format", "stream-json", "--verbose", "--strict-mcp-config", "--mcp-config", mcpConfig, "--tools", ""])
        {
            start.ArgumentList.Add(argument);
        }

        if (filumMounted)
        {
            start.ArgumentList.Add("--allowedTools");
            start.ArgumentList.Add("mcp__filum__*");
        }

        if (model is not null)
        {
            start.ArgumentList.Add("--model");
            start.ArgumentList.Add(model);
        }

        if (OperatingSystem.IsWindows())
        {
            // claude is a script on Windows: run it through the shell, which finds claude.cmd on the PATH.
            start.ArgumentList.Insert(0, "claude");
            start.ArgumentList.Insert(0, "/c");
            start.FileName = "cmd";
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("The agent could not start.");
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errors = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var session = AgentStream.Parse(await output);
        return process.ExitCode == 0 || session.Error is not null ? session : session with { Error = $"the agent exited with {process.ExitCode}: {(await errors).Trim()}" };
    }
}

/// <summary>Reads the agent's stream-json output: the tools it called and its final answer.</summary>
public static class AgentStream
{
    public static AgentSession Parse(string stream)
    {
        var tools = new List<string>();
        string? answer = null;
        string? error = null;
        foreach (var line in stream.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            JsonElement item;
            try
            {
                item = JsonDocument.Parse(line).RootElement;
            }
            catch (JsonException)
            {
                continue;
            }

            var type = item.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (type == "assistant" && item.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                tools.AddRange(content.EnumerateArray()
                    .Where(c => c.TryGetProperty("type", out var kind) && kind.GetString() == "tool_use" && c.TryGetProperty("name", out _))
                    .Select(c => c.GetProperty("name").GetString()!));
            }
            else if (type == "result")
            {
                answer = item.TryGetProperty("result", out var result) ? result.GetString() : null;
                if (item.TryGetProperty("is_error", out var isError) && isError.ValueKind == JsonValueKind.True)
                {
                    error = $"the agent reported an error: {answer}";
                }
            }
        }

        return new AgentSession(answer ?? string.Empty, tools, answer is null && error is null ? "the agent gave no result" : error);
    }
}

/// <summary>
/// Plays a scenario in one arm: every turn a new agent session in a fresh folder, the memory (with Filum) read from
/// filum-mcp's own folder with the engine. The "model" of a run is the arm.
/// </summary>
public sealed class McpScenarioRunner(IAgent agent, IClaimJudge judge, string filumMcp, SessionBudget budget) : IScenarioRunner
{
    /// <summary>The engine's tools that change the memory: a call of one is a <c>wrote</c> step.</summary>
    public static readonly IReadOnlySet<string> WritingTools = new HashSet<string>(StringComparer.Ordinal)
    {
        "memory_write", "memory_edit", "memory_append", "memory_delete", "memory_move", "memory_set_sensitivity", "memory_undo",
        "collection_add_rows", "collection_update_rows", "collection_remove_rows", "collection_add_field", "skill_save", "skill_set_enabled"
    };

    public bool CanStart(Scenario scenario) => budget.CanStart(scenario.TurnCount);

    public async Task<ScenarioRun> RunAsync(Scenario scenario, string arm, int repetition, CancellationToken cancellationToken)
    {
        var withFilum = arm == McpEvalCli.WithFilum;
        var root = Path.Combine(Path.GetTempPath(), "filum-evals-mcp", Guid.NewGuid().ToString("N"));
        var home = Path.Combine(root, "memory");
        Directory.CreateDirectory(root);
        var config = Path.Combine(root, "mcp.json");
        await File.WriteAllTextAsync(config, McpConfig(withFilum, home), cancellationToken);
        try
        {
            var turns = new List<TurnRecord>();
            var checks = new List<CheckedExpectation>();
            var claimed = new List<int>();
            var judgeCost = 0m;
            for (var i = 0; i < scenario.Turns.Count; i++)
            {
                var step = scenario.Turns[i];
                // A new folder for every session, so nothing of the agent's own carries over between turns.
                var work = Path.Combine(root, $"turn-{i + 1}");
                Directory.CreateDirectory(work);
                var clock = Stopwatch.StartNew();
                budget.Spend();
                var session = await agent.AskAsync(step.Message, work, config, withFilum, cancellationToken);
                var turn = new TurnRecord(i + 1, step.Chat, step.Message, session.Answer, Steps(session.Tools), 0, 0, 0m, clock.Elapsed.TotalSeconds, session.Error);
                turns.Add(turn);

                var memory = withFilum ? await ReadMemoryAsync(home, cancellationToken) : new MemorySnapshot([]);
                foreach (var expectation in step.Expect)
                {
                    if (expectation.Type == "judge")
                    {
                        var asked = turn.Error is null ? await judge.AskAsync(expectation.Question!, turn.Answer, cancellationToken) : null;
                        judgeCost += asked?.CostUsd ?? 0m;
                        var said = asked is null ? null : asked.Yes ? "yes" : "no";
                        checks.Add(new CheckedExpectation(i + 1, new ExpectationResult(expectation, said == expectation.PassIf, said is null ? $"the turn failed: {turn.Error}" : $"the judge said {said}")));
                    }
                    else
                    {
                        checks.Add(new CheckedExpectation(i + 1, Expectations.Check(expectation, turn, memory, turns[..^1])));
                    }
                }

                if (turn.Error is null && turn.Answer.Length > 0)
                {
                    var verdict = await judge.ClaimsChangeAsync(turn.Answer, cancellationToken);
                    judgeCost += verdict.CostUsd;
                    if (verdict.ClaimsChange && !turn.Wrote)
                    {
                        claimed.Add(i + 1);
                    }
                }
            }

            return new ScenarioRun(scenario.Id, arm, repetition, turns, checks, claimed, judgeCost);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // A server that has not exited yet may still hold a file: the temporary folder is left to the system.
            }
        }
    }

    /// <summary>The MCP config of an arm: filum-mcp alone on its own folder, or no server at all.</summary>
    public string McpConfig(bool withFilum, string home)
    {
        var servers = new Dictionary<string, object>();
        if (withFilum)
        {
            var dll = filumMcp.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            servers["filum"] = new
            {
                command = dll ? "dotnet" : filumMcp,
                args = dll ? new[] { filumMcp } : [],
                env = new Dictionary<string, string> { ["FILUM_HOME"] = home }
            };
        }

        return JsonSerializer.Serialize(new { mcpServers = servers });
    }

    /// <summary>The agent's tool calls as steps; Filum's tools are named <c>mcp__filum__&lt;tool&gt;</c> by the agent.</summary>
    public static IReadOnlyList<StepDto> Steps(IReadOnlyList<string> tools) =>
        tools.Select(name =>
        {
            var tool = name.StartsWith("mcp__filum__", StringComparison.Ordinal) ? name["mcp__filum__".Length..] : name;
            return new StepDto(WritingTools.Contains(tool) ? StepDto.Wrote : StepDto.Read, tool, null, $"Called {tool}", 0, null);
        }).ToList();

    /// <summary>The person's memory as filum-mcp left it, read with the engine from its folder.</summary>
    private static async Task<MemorySnapshot> ReadMemoryAsync(string home, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(Path.Combine(home, ".filum")))
        {
            return new MemorySnapshot([]);
        }

        var store = new LocalFolderStore(home);
        var memory = new MemoryService(store, Options.Create(new MemoryOptions()), NullLogger<MemoryService>.Instance);
        var files = (await memory.ListAsync(store.Owner, "/", includePrivate: true, cancellationToken)).Value ?? [];
        var details = new List<MemoryFileDetailDto>();
        foreach (var info in files)
        {
            if ((await memory.GetFileAsync(store.Owner, info.Path, cancellationToken)).Value is { } file)
            {
                var kind = MemoryPaths.KindOf(info.Path) == MemoryFileKind.Collection ? "collection" : "document";
                details.Add(new MemoryFileDetailDto(info.Path, kind, info.SizeBytes, info.LineCount, info.Sensitivity, info.UpdatedAt, info.Header, info.RowCount,
                    file.Content, new MemoryOriginDto("agent", null, info.UpdatedAt)));
            }
        }

        return new MemorySnapshot(details);
    }
}
