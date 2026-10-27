namespace Filum.Engine.Abstractions.Events;

/// <summary>A found event, with the one that followed it in its conversation when there is one.</summary>
public sealed record MemoryEventHit(MemoryEvent Event, MemoryEvent? Next, double Score);
