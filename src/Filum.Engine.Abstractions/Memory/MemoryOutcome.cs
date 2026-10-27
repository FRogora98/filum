using System.Diagnostics.CodeAnalysis;

namespace Filum.Engine.Abstractions.Memory;

/// <summary>A value, or the reason it was refused. Expected refusals are results, not exceptions.</summary>
public sealed record MemoryOutcome<T>(T? Value, string? Refusal, bool IsMissing = false) where T : class
{
    /// <summary>
    /// Whether the call was refused; <see cref="Refusal"/> then says why. An outcome that is not refused is made by
    /// <see cref="Ok"/>, so its <see cref="Value"/> is not null.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Refusal))]
    [MemberNotNullWhen(false, nameof(Value))]
    public bool IsRefused => Refusal is not null;

    /// <summary>A call that gave <paramref name="value"/>.</summary>
    public static MemoryOutcome<T> Ok(T value) => new(value, null);

    /// <summary>A call refused for <paramref name="reason"/>, written for the person or the model to read.</summary>
    public static MemoryOutcome<T> Refused(string reason) => new(null, reason);

    /// <summary>A refusal because the file or change does not exist for this person.</summary>
    public static MemoryOutcome<T> Missing(string reason) => new(null, reason, IsMissing: true);
}
