namespace Filum.Engine.Abstractions.Imports;

/// <summary>What recording items from a live source did: how many of them were new, and so were recorded.</summary>
public sealed record RecordedItems(int New);
