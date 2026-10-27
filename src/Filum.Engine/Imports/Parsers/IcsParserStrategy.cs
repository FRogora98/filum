using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Imports;
using Filum.Engine.Imports.Calendar;

namespace Filum.Engine.Imports.Parsers;

// An event is identified by its UID (or title) and start.
internal sealed class IcsParserStrategy : IImportParserStrategy
{
    public string Format => ImportFormat.Ics;

    public ImportParse Parse(string fileName, string content, DateTimeOffset now)
    {
        var scan = new IcsEventScan();
        foreach (var line in Unfold(content))
        {
            scan.Read(line);
        }

        return new ImportParse(scan.Items, scan.Skipped);
    }

    // A line that starts with a space or a tab continues the one before.
    private static List<string> Unfold(string content)
    {
        var unfolded = new List<string>();
        foreach (var line in ImportText.Clean(content).Split('\n'))
        {
            if ((line.StartsWith(' ') || line.StartsWith('\t')) && unfolded.Count > 0)
            {
                unfolded[^1] += line[1..];
            }
            else
            {
                unfolded.Add(line);
            }
        }

        return unfolded;
    }
}
