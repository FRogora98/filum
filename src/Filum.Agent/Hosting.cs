using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Text.Json;

namespace Filum.Agent;

/// <summary>How a host shapes the turn (spec 017): section <c>Agent</c>.</summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>The assistant's name in the platform instructions and the agent loop.</summary>
    public string Name { get; set; } = "Filum";

    /// <summary>Off: the model a request names is ignored and every turn uses the default model.</summary>
    public bool AllowModelChoice { get; set; } = true;

    /// <summary>The person's time zone, an IANA name, given to the host's tools.</summary>
    public string TimeZone { get; set; } = "UTC";

    /// <summary>The person's past conversations, searchable by the agent (spec 029); off until measured and decided.</summary>
    public bool EpisodicMemory { get; set; }
}

/// <summary>What a host's code gets about the turn it runs in.</summary>
public sealed record TurnContext(
    Guid Person, Guid ConversationId, Guid MessageId, IServiceProvider Services, DateTimeOffset Now, TimeZoneInfo TimeZone, CancellationToken CancellationToken);

/// <summary>
/// A host's own tools, offered to the model beside the engine's. Registered as a scoped service and resolved from the
/// request's scope, so its tools can use the host's scoped services. <see cref="Tools"/> only builds the functions: it is
/// also called once at start, with an empty person, to check the names.
/// </summary>
public interface ITurnToolSource
{
    IReadOnlyList<AIFunction> Tools(TurnContext turn);
}

/// <summary>What a host tool built with <see cref="HostTools.Create"/> returns to surface a value: the model gets
/// <paramref name="Text"/>; <paramref name="Data"/> goes on the turn's step, for the person's app.</summary>
public sealed record SurfacedResult(string Text, object? Data);

/// <summary>Called before the model with the turn; it may refuse the turn, and then nothing is saved or spent.</summary>
public interface ITurnGate
{
    Task<TurnGateResult> CheckAsync(TurnContext turn);
}

public sealed record TurnGateResult(bool Allowed, int Status, string? Message)
{
    public static TurnGateResult Allow { get; } = new(true, 200, null);

    public static TurnGateResult Refuse(int status, string message) => new(false, status, message);
}

/// <summary>What a turn that answered used, at its models' prices.</summary>
public sealed record TurnUsage(string Model, int InputTokens, int OutputTokens, decimal CostUsd);

/// <summary>Called only after a turn that answered, never after one that failed.</summary>
public interface ITurnObserver
{
    Task AnsweredAsync(TurnContext turn, TurnUsage usage);
}

/// <summary>Helpers for a host's own tools.</summary>
public static class HostTools
{
    /// <summary>The largest value a tool may surface, as JSON.</summary>
    public const int MaxDataBytes = 4096;

    /// <summary>
    /// A host tool from a method. Its return value reaches the turn as it is, so a <see cref="SurfacedResult"/> can be
    /// surfaced; a tool built another way still works, but its result only goes to the model.
    /// </summary>
    public static AIFunction Create(Delegate method, string name, string description) =>
        AIFunctionFactory.Create(method, new AIFunctionFactoryOptions
        {
            Name = name,
            Description = description,
            MarshalResult = (result, _, _) => new ValueTask<object?>(result)
        });

    /// <summary>
    /// Builds every source's tools once, with an empty person, and throws when a name clashes with an engine tool or
    /// with another host tool. A host calls it at start.
    /// </summary>
    public static void CheckNames(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var engine = MemoryTools.Catalog(new MemoryOptions()).Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var turn = new TurnContext(Guid.Empty, Guid.Empty, Guid.Empty, scope.ServiceProvider, DateTimeOffset.UtcNow, TimeZoneInfo.Utc, CancellationToken.None);
        foreach (var source in scope.ServiceProvider.GetServices<ITurnToolSource>())
        {
            foreach (var tool in source.Tools(turn))
            {
                if (engine.Contains(tool.Name))
                {
                    throw new InvalidOperationException($"The host tool '{tool.Name}' of {source.GetType().Name} has the name of an engine tool; give it another name.");
                }

                if (!seen.Add(tool.Name))
                {
                    throw new InvalidOperationException($"Two host tools are named '{tool.Name}'; give each a name of its own.");
                }
            }
        }
    }
}

/// <summary>
/// A host tool in a turn: each call becomes a step of the turn, a surfaced value goes on the step, and a failure is a
/// failed step with a plain message for the model, never the exception's detail.
/// </summary>
internal sealed class RecordedHostTool(AIFunction tool, MemoryTools steps, ILogger logger) : DelegatingAIFunction(tool)
{
    private const string Failed = "The tool failed; tell the person it did not work, without guessing its result.";

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await base.InvokeCoreAsync(arguments, cancellationToken);
            var elapsed = (int)stopwatch.ElapsedMilliseconds;
            if (result is not SurfacedResult surfaced)
            {
                steps.Record(new ToolStep(ToolStep.Used, Name, null, Summary(), elapsed, null));
                return result;
            }

            var data = surfaced.Data is null ? (JsonElement?)null : JsonSerializer.SerializeToElement(surfaced.Data);
            if (data is { } value && value.GetRawText().Length > HostTools.MaxDataBytes)
            {
                logger.LogWarning("[ HostTool ] {Tool} surfaced {Bytes} bytes, over the limit of {Limit}", Name, value.GetRawText().Length, HostTools.MaxDataBytes);
                steps.Record(new ToolStep(ToolStep.Failed, Name, null, $"{Summary()} failed", elapsed, null, "The result was too large."));
                return Failed;
            }

            steps.Record(new ToolStep(ToolStep.Used, Name, null, Summary(), elapsed, null, Data: data));
            return surfaced.Text;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("[ HostTool ] {Tool} failed after {ElapsedMs}ms: {ExceptionType}", Name, stopwatch.ElapsedMilliseconds, exception.GetType().Name);
            steps.Record(new ToolStep(ToolStep.Failed, Name, null, $"{Summary()} failed", (int)stopwatch.ElapsedMilliseconds, null, "The tool failed."));
            return Failed;
        }
    }

    /// <summary>The step's one line: the first sentence of the tool's own description.</summary>
    private string Summary()
    {
        var text = string.IsNullOrWhiteSpace(Description) ? $"Used {Name}" : Description.Split('\n')[0];
        var end = text.IndexOf(". ", StringComparison.Ordinal);
        text = (end > 0 ? text[..end] : text).TrimEnd('.');
        return text.Length > 120 ? text[..120] : text;
    }
}
