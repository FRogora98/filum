namespace Filum.Engine.Abstractions.Facts;

/// <summary>One value a fact had, from <c>ValidFrom</c> to <c>ValidTo</c> (empty while it holds).</summary>
public sealed record Fact(string Subject, string Attribute, string Value, string ValidFrom, string ValidTo, IReadOnlyList<long> Sources, string Note)
{
    /// <summary>Whether the fact still holds: it has no end day.</summary>
    public bool IsOpen => ValidTo.Length == 0;

    /// <summary>How it ended (<see cref="FactEnding"/>); empty while it holds or when not known (spec 041).</summary>
    public string Ended { get; init; } = string.Empty;

    /// <summary>When it was recorded, UTC, <c>yyyy-MM-ddTHH:mm:ssZ</c>; empty for a value recorded before spec 041.</summary>
    public string RecordedAt { get; init; } = string.Empty;

    /// <summary>Who recorded it (<c>agent</c> or <c>person</c>); empty when not known.</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>The start of the words of its first source event, when it has one.</summary>
    public string Quote { get; init; } = string.Empty;

    /// <summary>Whether it never held: it was corrected or retracted.</summary>
    public bool NeverHeld => Ended is FactEnding.Corrected or FactEnding.Retracted;

    /// <summary>Whether it held on <paramref name="day"/> (<c>yyyy-MM-dd</c>).</summary>
    public bool HeldOn(string day) =>
        !NeverHeld && string.CompareOrdinal(ValidFrom, day) <= 0 && (IsOpen || string.CompareOrdinal(day, ValidTo) < 0);
}
