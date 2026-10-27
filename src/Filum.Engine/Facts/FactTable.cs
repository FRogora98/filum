using System.Globalization;
using Filum.Engine.Abstractions.Collections;
using Filum.Engine.Abstractions.Facts;

namespace Filum.Engine.Facts;

/// <summary>
/// The facts of a person, kept as a plain collection the person can open and edit: one row per value a fact has had,
/// from <c>valid_from</c> to <c>valid_to</c>, with how it ended, when and by whom it was recorded and the words it came
/// from (spec 041). The current value is a query, never an overwrite: recording a fact ends the open one of the same
/// subject and attribute and opens the new one.
/// </summary>
public static class FactTable
{
    /// <summary>Where the facts collection lives in every memory.</summary>
    public const string Path = "/facts.csv";

    /// <summary>The attribute that gives a subject another name: <c>Sam Lee · alias: Sam</c> (spec 041).</summary>
    public const string Alias = "alias";

    /// <summary>A lasting instruction of the person, its reason in the note (spec 046).</summary>
    public const string Rule = "rule";

    /// <summary>Something promised or still to do, its due day in the note (spec 046).</summary>
    public const string Open = "open";

    /// <summary>Whether an attribute holds many values at once: a new one never closes the others (spec 046).</summary>
    public static bool IsManyValued(string attribute) => Same(attribute, Alias) || Same(attribute, Rule) || Same(attribute, Open);

    /// <summary>The fields of the facts collection, in order: the seven of spec 037, then the four of spec 041.</summary>
    public static readonly IReadOnlyList<string> Header =
        ["subject", "attribute", "value", "valid_from", "valid_to", "sources", "note", "recorded_at", "ended", "author", "quote"];

    // A table needs these; the later ones are read as empty when missing (a table written before spec 041).
    private const int Required = 7;

    // How much of a source's words a fact keeps.
    internal const int MaxQuote = 200;

    internal static string Day(DateTimeOffset when) => when.UtcDateTime.ToString(EngineConstants.Dates.Day, CultureInfo.InvariantCulture);

    internal static string Moment(DateTimeOffset when) => when.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    internal static bool IsDay(string? text) =>
        DateTime.TryParseExact(text, EngineConstants.Dates.Day, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    // Null when the table lacks a field of the first seven.
    internal static IReadOnlyList<Fact>? Read(CsvTable table)
    {
        var at = Columns(table);
        if (!table.IsValid || at.Take(Required).Any(i => i < 0))
        {
            return null;
        }

        string Field(CsvRecord r, int column) => at[column] < 0 ? string.Empty : r.Fields[at[column]].Trim();
        return table.Rows.Select(r => new Fact(Field(r, 0), Field(r, 1), Field(r, 2), Field(r, 3), Field(r, 4), SourcesOf(Field(r, 5)), Field(r, 6))
        {
            RecordedAt = Field(r, 7),
            Ended = Field(r, 8),
            Author = Field(r, 9),
            Quote = Field(r, 10),
        }).ToList();
    }

    // The table's header with every field of the facts, the missing ones added at the end, and its rows padded to it.
    internal static (IReadOnlyList<string> Header, List<string[]> Rows) Complete(CsvTable table)
    {
        var header = table.Header.ToList();
        header.AddRange(Header.Where(h => !header.Any(c => string.Equals(c, h, StringComparison.OrdinalIgnoreCase))));
        var rows = table.Rows.Select(r =>
        {
            var fields = new string[header.Count];
            Array.Fill(fields, string.Empty);
            r.Fields.ToArray().CopyTo(fields, 0);
            return fields;
        }).ToList();
        return (header, rows);
    }

    // Where each field of the facts is in the table; -1 when missing.
    internal static List<int> Columns(CsvTable table) => Columns(table.Header);

    internal static List<int> Columns(IReadOnlyList<string> header) =>
        Header.Select(h => header.ToList().FindIndex(c => string.Equals(c, h, StringComparison.OrdinalIgnoreCase))).ToList();

    internal static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    internal static string SourcesText(IEnumerable<long> sources) => string.Join(' ', sources.Distinct().Order().Select(s => s.ToString(CultureInfo.InvariantCulture)));

    internal static IReadOnlyList<long> SourcesOf(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : (long?)null)
            .OfType<long>()
            .ToList();

    internal static string Quote(string? words)
    {
        var text = (words ?? string.Empty).ReplaceLineEndings(" ").Trim();
        return text.Length <= MaxQuote ? text : text[..MaxQuote].TrimEnd() + "…";
    }

    // The subjects a name stands for: itself, and every subject it is an open alias of.
    internal static HashSet<string> Names(IReadOnlyList<Fact> facts, string name)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { name.Trim() };
        foreach (var alias in facts.Where(f => f.IsOpen && Same(f.Attribute, Alias) && Same(f.Value, name)))
        {
            names.Add(alias.Subject.Trim());
        }

        return names;
    }

    /// <summary>The title over the values that start later (spec 055).</summary>
    internal const string PlannedTitle = "Planned: these start on a later day and do not hold yet.";

    internal static string PlannedLine(Fact f) => $"- from {f.ValidFrom}: {f.Subject} · {f.Attribute}: {f.Value}";

    internal static string Line(Fact f) =>
        $"- {f.Subject} · {f.Attribute}: {f.Value} ({Span(f)}"
        + (f.Author.Length == 0 ? string.Empty : $" by the {f.Author}")
        + (f.Sources.Count == 0 ? string.Empty : $"; from event {SourcesText(f.Sources)}")
        + (f.Note.Length == 0 ? string.Empty : $"; {f.Note}") + ")";

    private static string Span(Fact f) => f.Ended switch
    {
        FactEnding.Corrected => $"{f.ValidFrom}, corrected: it never held",
        FactEnding.Retracted => $"{f.ValidFrom}, retracted: it never held",
        FactEnding.Ended => $"{f.ValidFrom} to {f.ValidTo}, ended",
        _ when f.IsOpen => $"since {f.ValidFrom}",
        _ => $"{f.ValidFrom} to {f.ValidTo}",
    };
}
