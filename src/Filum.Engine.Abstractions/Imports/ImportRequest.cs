using Filum.Engine.Abstractions.Constants;

namespace Filum.Engine.Abstractions.Imports;

/// <summary>A file to import: its name, its text, its format when known, the sensitivity of every item, and whether to only preview.</summary>
public sealed record ImportRequest(string FileName, string Content, string? Format = null, string Sensitivity = MemorySensitivity.Normal, bool Preview = true);
