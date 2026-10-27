namespace Filum.Engine.Abstractions.Imports;

/// <summary>What a parser made of a file: its items, how many lines or entries it skipped, or why it could not.</summary>
public sealed record ImportParse(IReadOnlyList<ImportItem> Items, int Skipped, string? Error = null);
