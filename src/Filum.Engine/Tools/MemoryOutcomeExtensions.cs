using System.Diagnostics.CodeAnalysis;
using Filum.Engine.Abstractions.Memory;

namespace Filum.Engine.Tools;

internal static class MemoryOutcomeExtensions
{
    public static bool Refuses<T>(this MemoryOutcome<T> outcome, [NotNullWhen(true)] out string? refusal, [NotNullWhen(false)] out T? value)
        where T : class
    {
        if (outcome.IsRefused)
        {
            refusal = outcome.Refusal;
            value = null;
            return true;
        }

        refusal = null;
        value = outcome.Value;
        return false;
    }
}
