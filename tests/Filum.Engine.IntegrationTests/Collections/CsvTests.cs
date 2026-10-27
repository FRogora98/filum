using System.Globalization;
using System.Text;
using Filum.Engine.Collections;
using Filum.Engine.IntegrationTests.Snapshots;
using static Filum.Engine.IntegrationTests.Snapshots.SnapshotText;

namespace Filum.Engine.IntegrationTests.Collections;

// Pinned in a snapshot: every collection is read and written through the CSV reader and writer.
public sealed class CsvTests
{
    [Fact]
    public void Csv_WhenReadingAndCheckingTexts_ShouldMatchItsSnapshot()
    {
        string[] samples =
        [
            "a,b\n1,2\n",
            "a,b\r\n1,2\r\n\r\n3,4",
            " a , b \n\"x, y\",\"say \"\"hi\"\"\"\n",
            "a,b\n\"multi\nline\",2\n3,4\n",
            "a,b\n\"\",\n,\n",
            "a,b\n1,\"2\"x\n",
            "a,b\n1,2\"3\n",
            "a,b\n\"open,2\n3,4\n",
            "",
            "\n\n",
            ",b\n1,2\n",
            "a,A\n1,2\n",
            "a,b\n1\n",
            "a\n\"\"\"\"\n"
        ];
        var text = new StringBuilder();
        foreach (var sample in samples)
        {
            text.Append("## ").Append(Escape(sample)).Append('\n');
            var (records, error) = Csv.Parse(sample);
            text.Append("parse error=").Append(Escape(error ?? "null")).Append('\n');
            foreach (var record in records)
            {
                text.Append(record.Line.ToString(CultureInfo.InvariantCulture)).Append(": ").AppendJoin(" | ", record.Fields.Select(Escape)).Append('\n');
            }

            var table = Csv.Check(sample);
            text.Append("check error=").Append(Escape(table.Error ?? "null")).Append(" header=").AppendJoin(" | ", table.Header.Select(Escape))
                .Append(" rows=").Append(table.Rows.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        text.Append("## write\n").Append(Escape(Csv.Write(["a", " b", "c,d"], [["1", "say \"hi\"", "x\ny"], ["", "\r", "plain"]]))).Append('\n');
        SnapshotFile.Match("Csv.snapshot.txt", text.ToString());
    }
}
