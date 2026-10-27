namespace Filum.Engine.Storage.LocalFolder;

internal sealed class FolderLayout
{
    public FolderLayout(string folder)
    {
        Root = Path.GetFullPath(folder);
        Meta = Path.Combine(Root, Names.Meta);
        Temporary = Path.Combine(Meta, Names.Temporary);
    }

    // Where /notes/plans.md is notes/plans.md.
    public string Root { get; }

    public string Meta { get; }

    public string Temporary { get; }

    public string OwnerFile => Path.Combine(Meta, Names.Owner);

    public string FormatFile => Path.Combine(Meta, Names.Format);

    public string LockFile => Path.Combine(Meta, Names.Lock);

    public string StateFile => Path.Combine(Meta, Names.State);

    public string RevisionsFile => Path.Combine(Meta, Names.Revisions);

    public string EventsFile => Path.Combine(Meta, Names.Events);

    public string VectorsFile => Path.Combine(Meta, Names.Vectors);

    public string VectorModelFile => Path.Combine(Meta, Names.VectorModel);

    public string Full(string path) => Path.Combine(Root, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    // To write a whole file before moving it into place.
    public string NewTemporaryFile() => Path.Combine(Temporary, Guid.NewGuid().ToString("N"));

    // The on-disk format: these names never change.
    public static class Names
    {
        public const string Meta = ".filum";
        public const string Temporary = "tmp";
        public const string Owner = "owner";
        public const string Format = "format";
        public const string Lock = "lock";
        public const string State = "state.json";
        public const string Revisions = "revisions.jsonl";
        public const string Events = "events.jsonl";
        public const string Vectors = "vectors.jsonl";
        public const string VectorModel = "vectors.model";
        public const string UnreadableSuffix = ".unreadable";
    }
}
