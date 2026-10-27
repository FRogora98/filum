namespace Filum.Engine.Memory.Files;

internal sealed record ContentCheck(string? Error, int SizeBytes, int LineCount, string? Header, int? RowCount);
