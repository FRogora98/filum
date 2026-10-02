using System.Globalization;

namespace Filum.Engine;

/// <summary>A statement that holds for a time (spec 030): open while <see cref="ValidTo"/> is empty.</summary>
public sealed record Fact(string Subject, string Attribute, string Value, string ValidFrom, string ValidTo, IReadOnlyList<long> Sources, string Note)
{
    public bool IsOpen => ValidTo.Length == 0;
}

/// <summary>
/// The facts of a person, kept as a plain collection the person can open and edit: one row per value a fact has had,
/// from <c>valid_from</c> to <c>valid_to</c>. The current value is a query, never an overwrite: recording a fact closes
/// the open one of the same subject and attribute and opens the new one.
/// </summary>
public static class Facts
{
    public const string Path = "/facts.csv";

    public static readonly IReadOnlyList<string> Header = ["subject", "attribute", "value", "valid_from", "valid_to", "sources", "note"];

    public static string Day(DateTimeOffset when) => when.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static bool IsDay(string? text) =>
        DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    /// <summary>The facts of a table that has at least the facts' fields; null when it does not.</summary>
    public static IReadOnlyList<Fact>? Read(CsvTable table)
    {
        var at = Header.Select(h => table.Header.ToList().FindIndex(c => string.Equals(c, h, StringComparison.OrdinalIgnoreCase))).ToList();
        if (!table.IsValid || at.Any(i => i < 0))
        {
            return null;
        }

        return table.Rows.Select(r => new Fact(r.Fields[at[0]].Trim(), r.Fields[at[1]].Trim(), r.Fields[at[2]].Trim(), r.Fields[at[3]].Trim(), r.Fields[at[4]].Trim(),
            SourcesOf(r.Fields[at[5]]), r.Fields[at[6]].Trim())).ToList();
    }

    public static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    public static string SourcesText(IEnumerable<long> sources) => string.Join(' ', sources.Distinct().Order().Select(s => s.ToString(CultureInfo.InvariantCulture)));

    public static IReadOnlyList<long> SourcesOf(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : (long?)null)
            .OfType<long>()
            .ToList();

    /// <summary>One line for the model: subject · attribute: value, with its dates and sources.</summary>
    public static string Line(Fact f) =>
        $"- {f.Subject} · {f.Attribute}: {f.Value} ({(f.IsOpen ? $"since {f.ValidFrom}" : $"{f.ValidFrom} to {f.ValidTo}")}"
        + (f.Sources.Count == 0 ? string.Empty : $"; from event {SourcesText(f.Sources)}")
        + (f.Note.Length == 0 ? string.Empty : $"; {f.Note}") + ")";
}
