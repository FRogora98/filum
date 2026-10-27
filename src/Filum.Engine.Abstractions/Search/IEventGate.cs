namespace Filum.Engine.Abstractions.Search;

/// <summary>
/// Tells from an event's vector, in about a millisecond, whether it says something about the person's life:
/// what a consolidation pass is handed. A discarded event stays in the log; it is only not read by the pass.
/// </summary>
public interface IEventGate
{
    /// <summary>Whether the event of <paramref name="vector"/> should be read by a consolidation pass.</summary>
    bool Keep(float[] vector);
}
