using Filum.Engine.Abstractions.Memory;

namespace Filum.Engine.Testing.Builders;

/// <summary>What the tests read from a <see cref="MemoryOutcome{T}"/>.</summary>
public static class Outcomes
{
    /// <summary>The value of an outcome that must not be refused: a refusal fails the test with its text.</summary>
    public static T Ok<T>(MemoryOutcome<T> outcome) where T : class
    {
        Assert.False(outcome.IsRefused, outcome.Refusal);
        return outcome.Value!;
    }
}
