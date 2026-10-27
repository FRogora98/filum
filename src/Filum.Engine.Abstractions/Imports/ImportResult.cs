namespace Filum.Engine.Abstractions.Imports;

/// <summary>
/// What an import found and did: its format, the id of its source (the events' conversation id, which forgets it),
/// how many items, how many new and duplicate, how many lines skipped, the first new items as they are recorded, and
/// whether anything was written.
/// </summary>
public sealed record ImportResult(string Format, Guid SourceId, int Items, int New, int Duplicates, int Skipped, IReadOnlyList<string> Sample, bool Written);
