namespace Filum.Engine.Storage.LocalFolder;

// One change of a revision line: at the base's line At (from 0), Remove lines are replaced by Insert (whole lines, with
// their line breaks).
internal sealed record RevisionHunk(int At, int Remove, string Insert);
