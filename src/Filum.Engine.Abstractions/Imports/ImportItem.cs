namespace Filum.Engine.Abstractions.Imports;

/// <summary>
/// One item of an imported file: when it happened (null when the file does not say), who said or sent it (null when
/// nobody did), its text, and its identity in the source, stable across imports of the same file.
/// </summary>
public sealed record ImportItem(DateTimeOffset? When, string? Who, string Text, string Identity);
