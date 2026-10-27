using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Imports;

namespace Filum.Engine.Imports.Parsers;

/// <summary>Reads the files of one format into items; one implementation per format of <see cref="ImportFormat"/>.</summary>
internal interface IImportParserStrategy
{
    /// <summary>The format this parser reads, one of <see cref="ImportFormat.All"/>.</summary>
    string Format { get; }

    /// <summary>
    /// The items of a file, before repeats are ranked. The identity of an item is the dedupe key of its stored event,
    /// so its shape must never change.
    /// </summary>
    ImportParse Parse(string fileName, string content, DateTimeOffset now);
}
