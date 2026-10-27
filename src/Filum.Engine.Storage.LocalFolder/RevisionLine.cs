using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Filum.Engine.Abstractions.Storage;

namespace Filum.Engine.Storage.LocalFolder;

// One line of revisions.jsonl. The disk's own record, apart from the public model: its fields, their names and their
// order are the format every version since 0.7 writes and reads.
//
// Since 1.0.0 a line can hold a change instead of a content: Base is the previous revision of the same file, and Hunks
// turn Base's content into this one's (Content stays empty). Both are left out of a whole line, which stays the 0.7
// format, and a pre-release reader never meets them in a folder it wrote.
internal sealed record RevisionLine(
    long Id,
    Guid FileId,
    string Path,
    string Content,
    string Sensitivity,
    bool Deleted,
    string Operation,
    string Author,
    Guid? ConversationId,
    Guid? MessageId,
    long? UndoesRevisionId,
    DateTimeOffset CreatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? Base = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<RevisionHunk>? Hunks = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Sha = null)
{
    // Since 1.0.0 (format 3, spec 039) every line written carries Sha, the hash of its content, last; a line without one was
    // written before and is read as before.
    [JsonIgnore]
    public bool IsChange => Base is not null;

    public static RevisionLine From(StoredRevision revision) =>
        new(revision.Id, revision.FileId, revision.Path, revision.Content, revision.Sensitivity, revision.Deleted, revision.Operation,
            revision.Author, revision.ConversationId, revision.MessageId, revision.UndoesRevisionId, revision.CreatedAt, Sha: Hash(revision.Content));

    // SHA-256 of the UTF-8 content, lower-case hex.
    public static string Hash(string content) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    // Whether content is what this line was written with; a line without a hash cannot tell and is taken as it is.
    public bool Holds(string content) => Sha is null || Sha == Hash(content);

    // The change from previous (the content of revision baseId) to the revision's content.
    public static RevisionLine Change(StoredRevision revision, long baseId, string previous) =>
        From(revision) with { Content = string.Empty, Base = baseId, Hunks = RevisionDiff.Hunks(previous, revision.Content) };

    // The content of a change line, given its base's.
    public string ContentAfter(string previous) => RevisionDiff.Apply(previous, Hunks ?? []);

    public StoredRevision ToRevision() => ToRevision(Content);

    public StoredRevision ToRevision(string content) =>
        new(Id, FileId, Path, content, Sensitivity, Deleted, Operation, Author, ConversationId, MessageId, UndoesRevisionId, CreatedAt);
}
