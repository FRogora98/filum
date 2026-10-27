using Filum.Engine.Abstractions.Facts;
using Filum.Engine.Abstractions.Memory;

namespace Filum.Engine.Memory.Contracts;

/// <summary>
/// A person's facts: values that hold from a day on, with every value they had before. A role of
/// <see cref="IMemoryService"/>, for callers that need only this.
/// </summary>
public interface IMemoryFacts
{
    /// <summary>
    /// Records that <paramref name="subject"/>'s <paramref name="attribute"/> is <paramref name="value"/> from
    /// <paramref name="validFrom"/> (today when null): the open fact of the same subject and attribute is closed that
    /// day, and both stay. The facts collection is created on first use.
    /// </summary>
    Task<MemoryOutcome<MemoryChange>> RecordFactAsync(
        Guid userId, MemoryActor actor, string? subject, string? attribute, string? value, string? validFrom, string? note, IReadOnlyList<long> sources, CancellationToken cancellationToken);

    /// <summary>
    /// As the other <c>RecordFactAsync</c>; with <paramref name="correction"/>, the open value was wrong: it ends as
    /// corrected, never having held (spec 041).
    /// </summary>
    Task<MemoryOutcome<MemoryChange>> RecordFactAsync(
        Guid userId, MemoryActor actor, string? subject, string? attribute, string? value, string? validFrom, string? note, IReadOnlyList<long> sources, bool correction, CancellationToken cancellationToken);

    /// <summary>
    /// Ends the open value of <paramref name="subject"/>'s <paramref name="attribute"/> on <paramref name="on"/> (today
    /// when null) with no new value; with <paramref name="retract"/>, it never held (spec 041).
    /// </summary>
    Task<MemoryOutcome<MemoryChange>> EndFactAsync(Guid userId, MemoryActor actor, string? subject, string? attribute, string? on, bool retract, string? value, CancellationToken cancellationToken);

    /// <summary>
    /// As the other <c>EndFactAsync</c>; <paramref name="sources"/> are the events that ended it, kept with the ones it
    /// came from (spec 055).
    /// </summary>
    Task<MemoryOutcome<MemoryChange>> EndFactAsync(
        Guid userId, MemoryActor actor, string? subject, string? attribute, string? on, bool retract, string? value, IReadOnlyList<long> sources, CancellationToken cancellationToken);

    /// <summary>The facts that held on <paramref name="on"/> (<c>yyyy-MM-dd</c>; now when null), of one subject or of all.</summary>
    Task<MemoryOutcome<IReadOnlyList<Fact>>> CurrentFactsAsync(Guid userId, string? subject, string? on, CancellationToken cancellationToken);

    /// <summary>The facts that hold now, of one subject or of all.</summary>
    Task<MemoryOutcome<IReadOnlyList<Fact>>> CurrentFactsAsync(Guid userId, string? subject, CancellationToken cancellationToken);

    /// <summary>The values that start after today, of one subject or of all, soonest first (spec 055).</summary>
    Task<MemoryOutcome<IReadOnlyList<Fact>>> PlannedFactsAsync(Guid userId, string? subject, CancellationToken cancellationToken);

    /// <summary>Every value a fact of the subject has had, oldest first.</summary>
    Task<MemoryOutcome<IReadOnlyList<Fact>>> FactHistoryAsync(Guid userId, string? subject, string? attribute, CancellationToken cancellationToken);

    /// <summary>
    /// The facts that hold now and may answer <paramref name="query"/>, best first, at most five: by its words and, with
    /// an embedding model, by its meaning (spec 042).
    /// </summary>
    Task<MemoryOutcome<IReadOnlyList<Fact>>> MatchFactsAsync(Guid userId, string? query, CancellationToken cancellationToken);
}
