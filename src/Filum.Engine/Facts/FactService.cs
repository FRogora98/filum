using Filum.Engine.Abstractions.Collections;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Facts;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Collections;
using Filum.Engine.Memory.Files;

namespace Filum.Engine.Facts;

// The facts as a ledger (spec 041): a value ends as changed, corrected, retracted or ended; an earlier value is placed
// in time; the same value again changes nothing; every row says when and by whom it was recorded and from which words.
internal sealed partial class FactService(IMemoryFileStore store, MemoryWriter writer, TimeProvider time)
{
    // The positions in FactTable.Header.
    private const int Subject = 0;
    private const int Attribute = 1;
    private const int Value = 2;
    private const int ValidFrom = 3;
    private const int ValidTo = 4;
    private const int Sources = 5;
    private const int Note = 6;
    private const int RecordedAt = 7;
    private const int Ended = 8;
    private const int Author = 9;
    private const int Quote = 10;

    private static string NotAFactsCollection => $"{FactTable.Path} is not a facts collection: it needs the fields {string.Join(", ", FactTable.Header.Take(7))}.";

    public sealed record Asked(string? Subject, string? Attribute, string? Value, string? ValidFrom, string? Note, IReadOnlyList<long> Sources, bool Correction, string? Words);

    public async Task<MemoryOutcome<MemoryChange>> RecordAsync(Guid userId, MemoryActor actor, Asked asked, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(asked.Subject) || string.IsNullOrWhiteSpace(asked.Attribute) || string.IsNullOrWhiteSpace(asked.Value))
        {
            return MemoryOutcome<MemoryChange>.Refused("A fact needs a subject, an attribute and a value.");
        }

        if (!string.IsNullOrWhiteSpace(asked.ValidFrom) && !FactTable.IsDay(asked.ValidFrom.Trim()))
        {
            return MemoryOutcome<MemoryChange>.Refused("validFrom must be a day, yyyy-MM-dd.");
        }

        // The same value again changes nothing, and is not an error.
        if (await AlreadyAsync(userId, asked, cancellationToken) is { } unchanged)
        {
            return MemoryOutcome<MemoryChange>.Ok(unchanged);
        }

        var now = time.GetUtcNow();
        return await writer.ChangeAsync(userId, actor, FactTable.Path, MemoryOperation.Edit, (file, _) => Record(file, asked, actor, now), cancellationToken);
    }

    public Task<MemoryOutcome<MemoryChange>> EndAsync(
        Guid userId, MemoryActor actor, string? subject, string? attribute, string? on, bool retract, string? value, IReadOnlyList<long> sources, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(attribute))
        {
            return Task.FromResult(MemoryOutcome<MemoryChange>.Refused("Say which fact: a subject and an attribute."));
        }

        var day = string.IsNullOrWhiteSpace(on) ? FactTable.Day(time.GetUtcNow()) : on.Trim();
        if (!FactTable.IsDay(day))
        {
            return Task.FromResult(MemoryOutcome<MemoryChange>.Refused("on must be a day, yyyy-MM-dd."));
        }

        return writer.ChangeAsync(userId, actor, FactTable.Path, MemoryOperation.Edit, (file, _) => End(file, subject, attribute, day, retract, value, sources), cancellationToken);
    }

    public async Task<MemoryOutcome<IReadOnlyList<Fact>>> CurrentAsync(Guid userId, string? subject, string? on, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(on) && !FactTable.IsDay(on.Trim()))
        {
            return MemoryOutcome<IReadOnlyList<Fact>>.Refused("on must be a day, yyyy-MM-dd.");
        }

        var all = await AllAsync(userId, cancellationToken);
        if (all.IsRefused)
        {
            return all;
        }

        // Now is today on the engine's clock: a value that starts later is planned, and the one it replaces still holds
        // (spec 055).
        var day = string.IsNullOrWhiteSpace(on) ? FactTable.Day(time.GetUtcNow()) : on.Trim();
        var names = string.IsNullOrWhiteSpace(subject) ? null : FactTable.Names(all.Value, subject);
        return MemoryOutcome<IReadOnlyList<Fact>>.Ok(all.Value
            .Where(f => f.HeldOn(day))
            .Where(f => names is null || names.Contains(f.Subject.Trim()))
            .ToList());
    }

    public async Task<MemoryOutcome<IReadOnlyList<Fact>>> HistoryAsync(Guid userId, string? subject, string? attribute, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return MemoryOutcome<IReadOnlyList<Fact>>.Refused("Say whose facts: a subject.");
        }

        var all = await AllAsync(userId, cancellationToken);
        if (all.IsRefused)
        {
            return all;
        }

        var names = FactTable.Names(all.Value, subject);
        return MemoryOutcome<IReadOnlyList<Fact>>.Ok(all.Value
            .Where(f => names.Contains(f.Subject.Trim()) && (string.IsNullOrWhiteSpace(attribute) || FactTable.Same(f.Attribute, attribute)))
            .OrderBy(f => f.ValidFrom, StringComparer.Ordinal)
            .ToList());
    }

    private async Task<MemoryOutcome<IReadOnlyList<Fact>>> AllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var file = await store.FindLiveAsync(userId, FactTable.Path, cancellationToken);
        if (file is null)
        {
            return MemoryOutcome<IReadOnlyList<Fact>>.Ok([]);
        }

        var facts = FactTable.Read(Csv.Check(file.Content));
        return facts is null
            ? MemoryOutcome<IReadOnlyList<Fact>>.Refused(NotAFactsCollection)
            : MemoryOutcome<IReadOnlyList<Fact>>.Ok(facts);
    }

    // The change the same call would not make: the value already holds now, or on the day given (no correction asked).
    private async Task<MemoryChange?> AlreadyAsync(Guid userId, Asked asked, CancellationToken cancellationToken)
    {
        var file = await store.FindLiveAsync(userId, FactTable.Path, cancellationToken);
        if (asked.Correction || file is null || FactTable.Read(Csv.Check(file.Content)) is not { } facts)
        {
            return null;
        }

        var subject = Canonical(facts, asked.Subject!, asked.Attribute!);
        var day = asked.ValidFrom?.Trim();
        var holds = facts.Any(f => !f.NeverHeld && FactTable.Same(f.Subject, subject) && FactTable.Same(f.Attribute, asked.Attribute!) && FactTable.Same(f.Value, asked.Value!)
            && (string.IsNullOrEmpty(day) ? f.IsOpen : HeldOn(f, day)));
        return holds ? new MemoryChange(file.LatestRevisionId, FactTable.Path, MemoryOperation.Unchanged, Created: false, file.LineCount, file.RowCount, Added: 0) : null;
    }

    // A fact asked under an alias goes to the subject the alias names; an alias itself is recorded as it is said.
    private static string Canonical(IReadOnlyList<Fact> facts, string subject, string attribute) =>
        FactTable.Same(attribute, FactTable.Alias)
            ? subject.Trim()
            : facts.FirstOrDefault(f => f.IsOpen && FactTable.Same(f.Attribute, FactTable.Alias) && FactTable.Same(f.Value, subject))?.Subject.Trim() ?? subject.Trim();

    private static ChangeTarget Record(StoredFile? file, Asked asked, MemoryActor actor, DateTimeOffset now)
    {
        var table = file is null ? new CsvTable(FactTable.Header, [], null) : Csv.Check(file.Content);
        var facts = FactTable.Read(table);
        if (facts is null)
        {
            return ChangeTarget.Refuse(NotAFactsCollection);
        }

        var (header, rows) = FactTable.Complete(table);
        var at = FactTable.Columns(header);
        var subject = Canonical(facts, asked.Subject!, asked.Attribute!);
        var same = facts.Select((f, i) => (Fact: f, Row: i))
            .Where(x => !x.Fact.NeverHeld && FactTable.Same(x.Fact.Subject, subject) && FactTable.Same(x.Fact.Attribute, asked.Attribute!)).ToList();
        var open = same.Where(x => x.Fact.IsOpen).ToList();
        var from = asked.ValidFrom?.Trim() is { Length: > 0 } given ? given
            : asked.Correction && open.Count > 0 ? open.Min(x => x.Fact.ValidFrom)! : FactTable.Day(now);
        var placed = FactTable.IsManyValued(asked.Attribute!) ? (from, string.Empty, string.Empty)
            : asked.Correction && Correct(rows, at, same, string.IsNullOrWhiteSpace(asked.ValidFrom) ? null : from) is { } corrected ? corrected
            : Place(rows, at, same, from, asked.Value!);
        if (placed is not var (start, until, ending))
        {
            // The next value was the same: it now starts earlier, and no row is added.
            return new ChangeTarget(FactTable.Path, Csv.Write(header, rows), file?.Sensitivity ?? MemorySensitivity.Normal, Deleted: false);
        }

        var added = new string[header.Count];
        Array.Fill(added, string.Empty);
        Set(added, at, Subject, subject);
        Set(added, at, Attribute, asked.Attribute!.Trim());
        Set(added, at, Value, asked.Value!.Trim());
        Set(added, at, ValidFrom, start);
        Set(added, at, ValidTo, until);
        Set(added, at, Ended, ending);
        Set(added, at, Sources, FactTable.SourcesText(asked.Sources));
        Set(added, at, Note, asked.Note?.Trim() ?? string.Empty);
        Set(added, at, RecordedAt, FactTable.Moment(now));
        Set(added, at, Author, actor.Author);
        Set(added, at, Quote, FactTable.Quote(asked.Words));
        rows.Add(added);
        return new ChangeTarget(FactTable.Path, Csv.Write(header, rows), file?.Sensitivity ?? MemorySensitivity.Normal, Deleted: false);
    }

    // A value that was wrong never held: the one of the day given (the open one when no day is given) is corrected, and
    // the new value takes its time. Null when no value held then: the new one is placed as any other.
    private static (string Start, string Until, string Ending)? Correct(List<string[]> rows, List<int> at, List<(Fact Fact, int Row)> same, string? day)
    {
        var wrong = same.Where(x => day is null ? x.Fact.IsOpen : HeldOn(x.Fact, day)).ToList();
        if (wrong.Count == 0)
        {
            return null;
        }

        foreach (var x in wrong)
        {
            Set(rows[x.Row], at, ValidTo, x.Fact.ValidFrom);
            Set(rows[x.Row], at, Ended, FactEnding.Corrected);
        }

        var last = wrong.OrderBy(x => x.Fact.ValidFrom, StringComparer.Ordinal).Last().Fact;
        return (wrong.Min(x => x.Fact.ValidFrom)!, last.IsOpen ? string.Empty : last.ValidTo, last.IsOpen ? string.Empty : last.Ended);
    }

    // A value starts on its day and holds until the next recorded value starts, or until the value it falls inside ended
    // (open when neither). What held on that day changes there; a value of the same day is replaced, the later said wins
    // (it keeps that value's end). When the next value is the same one, it starts earlier instead: null, nothing to add.
    private static (string Start, string Until, string Ending)? Place(List<string[]> rows, List<int> at, List<(Fact Fact, int Row)> same, string from, string value)
    {
        var sameDay = same.Where(x => x.Fact.ValidFrom == from && x.Fact.ValidTo != from).OrderBy(x => x.Row).LastOrDefault();
        var next = same.Where(x => string.CompareOrdinal(x.Fact.ValidFrom, from) > 0).OrderBy(x => x.Fact.ValidFrom, StringComparer.Ordinal).FirstOrDefault();
        var inside = same.Where(x => !x.Fact.IsOpen && string.CompareOrdinal(x.Fact.ValidFrom, from) < 0 && HeldOn(x.Fact, from)).Select(x => x.Fact).FirstOrDefault();
        var (until, ending) = sameDay.Fact is { } replaced ? (replaced.IsOpen ? string.Empty : replaced.ValidTo, replaced.IsOpen ? string.Empty : replaced.Ended)
            : next.Fact is { } after && (inside is null || string.CompareOrdinal(after.ValidFrom, inside.ValidTo) <= 0) ? (after.ValidFrom, FactEnding.Changed)
            : inside is not null ? (inside.ValidTo, inside.Ended) : (string.Empty, string.Empty);
        foreach (var x in same.Where(x => x.Fact.ValidFrom == from ? x.Fact.ValidTo != from : HeldOn(x.Fact, from)))
        {
            Set(rows[x.Row], at, ValidTo, from);
            Set(rows[x.Row], at, Ended, FactEnding.Changed);
        }

        if (sameDay.Fact is null && next.Fact is { } following && until == following.ValidFrom && FactTable.Same(following.Value, value))
        {
            Set(rows[next.Row], at, ValidFrom, from);
            return null;
        }

        return (from, until, ending);
    }

    // Held on that day: started on or before it, and still open or ended after it.
    private static bool HeldOn(Fact fact, string day) =>
        string.CompareOrdinal(fact.ValidFrom, day) <= 0 && (fact.IsOpen || string.CompareOrdinal(fact.ValidTo, day) > 0);

    // One value ends when it is named (spec 046: rules and open items hold many); otherwise every open one.
    private static ChangeTarget End(StoredFile? file, string subject, string attribute, string day, bool retract, string? value, IReadOnlyList<long> sources)
    {
        var table = file is null ? null : Csv.Check(file.Content);
        var facts = table is null ? [] : FactTable.Read(table);
        if (facts is null)
        {
            return ChangeTarget.Refuse(NotAFactsCollection);
        }

        var name = Canonical(facts, subject, attribute);
        var open = facts.Select((f, i) => (Fact: f, Row: i))
            .Where(x => x.Fact.IsOpen && !x.Fact.NeverHeld && FactTable.Same(x.Fact.Subject, name) && FactTable.Same(x.Fact.Attribute, attribute)
                && (string.IsNullOrWhiteSpace(value) || FactTable.Same(x.Fact.Value, value))).ToList();
        if (open.Count == 0 || table is null)
        {
            return ChangeTarget.Refuse($"{subject.Trim()} · {attribute.Trim()} has no open value to end.");
        }

        if (!retract && open.Any(x => string.CompareOrdinal(x.Fact.ValidFrom, day) > 0))
        {
            return ChangeTarget.Refuse($"{subject.Trim()} · {attribute.Trim()} holds since a later day than {day}.");
        }

        var (header, rows) = FactTable.Complete(table);
        var at = FactTable.Columns(header);
        foreach (var x in open)
        {
            Set(rows[x.Row], at, ValidTo, retract ? x.Fact.ValidFrom : day);
            Set(rows[x.Row], at, Ended, retract ? FactEnding.Retracted : FactEnding.Ended);

            // The events that ended it join those it came from, so the ending can be checked (spec 055).
            Set(rows[x.Row], at, Sources, FactTable.SourcesText([.. x.Fact.Sources, .. sources]));
        }

        return new ChangeTarget(FactTable.Path, Csv.Write(header, rows), file!.Sensitivity, Deleted: false);
    }

    private static void Set(string[] row, List<int> at, int field, string value) => row[at[field]] = value;
}
