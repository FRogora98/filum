namespace Filum.Engine.Imports.Parsers;

// A new format is a new IImportParserStrategy added here.
internal static class ImportParsers
{
    private static readonly IReadOnlyDictionary<string, IImportParserStrategy> ByFormat =
        new IImportParserStrategy[] { new WhatsAppParserStrategy(), new MboxParserStrategy(), new IcsParserStrategy(), new CsvParserStrategy() }
            .ToDictionary(p => p.Format, StringComparer.Ordinal);

    public static IImportParserStrategy? Find(string format) => ByFormat.GetValueOrDefault(format);
}
