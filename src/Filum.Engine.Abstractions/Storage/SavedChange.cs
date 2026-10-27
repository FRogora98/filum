namespace Filum.Engine.Abstractions.Storage;

/// <summary>A saved change: the revision it produced and the file it belongs to.</summary>
public sealed record SavedChange(long RevisionId, Guid FileId);
