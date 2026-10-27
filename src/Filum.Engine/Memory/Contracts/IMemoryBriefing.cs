namespace Filum.Engine.Memory.Contracts;

/// <summary>
/// What an agent should know without asking (spec 045): the person at the start of a session, and what the memory
/// holds about each message. Deterministic and read only; nothing private is in either.
/// </summary>
public interface IMemoryBriefing
{
    /// <summary>
    /// The person's brief, at most <paramref name="maxChars"/> characters (<c>MemoryOptions.BriefChars</c> when null): their
    /// core, what was learned (newest first), the facts that hold now (most recently recorded first), the map of the
    /// files and how much was said.
    /// </summary>
    Task<string> BriefAsync(Guid userId, int? maxChars, CancellationToken cancellationToken);

    /// <summary>
    /// What the memory holds about <paramref name="message"/>, at most <paramref name="maxChars"/> characters
    /// (<c>MemoryOptions.RecallChars</c> when null): the facts that may answer it and the best few things said that match
    /// it; empty when nothing does.
    /// </summary>
    Task<string> RecallAsync(Guid userId, string? message, int? maxChars, CancellationToken cancellationToken);

    /// <summary>
    /// The rules the person set, as the brief opens with them (most recently recorded first), in whole lines within
    /// <paramref name="maxChars"/> when given; empty with no rule, or when the facts file is private (spec 056).
    /// </summary>
    Task<string> RulesAsync(Guid userId, int? maxChars, CancellationToken cancellationToken);
}
