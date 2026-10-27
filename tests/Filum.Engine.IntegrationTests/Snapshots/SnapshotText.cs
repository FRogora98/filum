using System.Globalization;
using System.Text;

namespace Filum.Engine.IntegrationTests.Snapshots;

// The same way on every machine.
internal static class SnapshotText
{
    public static string Number(double? value) => value?.ToString("R", CultureInfo.InvariantCulture) ?? "null";

    // Backslashes, line breaks and every non-ASCII character escaped.
    public static string Escape(string value)
    {
        var text = new StringBuilder();
        foreach (var c in value)
        {
            _ = c switch
            {
                '\\' => text.Append("\\\\"),
                '\n' => text.Append("\\n"),
                '\r' => text.Append("\\r"),
                '\t' => text.Append("\\t"),
                _ when c < ' ' || c > '~' => text.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture)),
                _ => text.Append(c)
            };
        }

        return text.ToString();
    }
}
