using Filum.Engine.Abstractions.Constants;

namespace Filum.Engine.Storage.LocalFolder;

// The disk fields tell a hand edit from an untouched file. The properties and their order are the on-disk format.
internal sealed class FileEntry
{
    public Guid Id { get; set; }

    public string Path { get; set; } = string.Empty;

    public string Sensitivity { get; set; } = MemorySensitivity.Normal;

    public int SizeBytes { get; set; }

    public int LineCount { get; set; }

    public string? Header { get; set; }

    public int? RowCount { get; set; }

    public int Version { get; set; }

    public long LatestRevisionId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string Hash { get; set; } = string.Empty;

    public long DiskLength { get; set; }

    public DateTime DiskTimeUtc { get; set; }

    // How many changes the latest revision is from a whole content in the log (1.0.0); 0 in a state written before.
    public int RevisionDepth { get; set; }
}
