namespace Filum.Engine.Abstractions.Memory;

/// <summary>What a line said before it was replaced, and when.</summary>
public sealed record LineChange(string Before, DateTimeOffset At);
