namespace Filum.Engine;

/// <summary>Facts that hold for a time, in <see cref="Facts.Path"/> (spec 030).</summary>
public sealed partial class MemoryService
{
    /// <summary>
    /// Records that <paramref name="subject"/>'s <paramref name="attribute"/> is <paramref name="value"/> from
    /// <paramref name="validFrom"/> (today when null): the open fact of the same subject and attribute is closed that
    /// day, and both stay. The facts collection is created on first use.
    /// </summary>
    public Task<MemoryOutcome<MemoryChange>> RecordFactAsync(
        Guid userId, MemoryActor actor, string? subject, string? attribute, string? value, string? validFrom, string? note, IReadOnlyList<long> sources, CancellationToken cancellationToken)
    {
        var from = string.IsNullOrWhiteSpace(validFrom) ? Facts.Day(DateTimeOffset.UtcNow) : validFrom.Trim();
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(attribute) || string.IsNullOrWhiteSpace(value))
        {
            return Task.FromResult(MemoryOutcome<MemoryChange>.Refused("A fact needs a subject, an attribute and a value."));
        }

        if (!Facts.IsDay(from))
        {
            return Task.FromResult(MemoryOutcome<MemoryChange>.Refused("validFrom must be a day, yyyy-MM-dd."));
        }

        return ChangeAsync(userId, actor, Facts.Path, MemoryOperation.Edit, cancellationToken, (file, _) =>
        {
            var table = file is null ? new CsvTable(Facts.Header, [], null) : Csv.Check(file.Content);
            var facts = Facts.Read(table);
            if (facts is null)
            {
                return Refuse($"{Facts.Path} is not a facts collection: it needs the fields {string.Join(", ", Facts.Header)}.");
            }

            var open = facts.Where(f => f.IsOpen && Facts.Same(f.Subject, subject) && Facts.Same(f.Attribute, attribute)).ToList();
            if (open.Any(f => Facts.Same(f.Value, value)))
            {
                return Refuse($"{subject.Trim()} · {attribute.Trim()} is already {value.Trim()}; nothing to record.");
            }

            if (open.Any(f => string.CompareOrdinal(f.ValidFrom, from) > 0))
            {
                return Refuse($"The current {attribute.Trim()} of {subject.Trim()} holds since a later day than {from}; give a validFrom on or after it.");
            }

            var index = table.Header.ToList();
            int At(string field) => index.FindIndex(h => string.Equals(h, field, StringComparison.OrdinalIgnoreCase));
            var rows = table.Rows.Select((r, i) =>
            {
                if (!facts[i].IsOpen || !open.Contains(facts[i]))
                {
                    return r.Fields;
                }

                var fields = r.Fields.ToArray();
                fields[At("valid_to")] = from;
                return (IReadOnlyList<string>)fields;
            }).ToList();
            var added = new string[index.Count];
            Array.Fill(added, string.Empty);
            added[At("subject")] = subject.Trim();
            added[At("attribute")] = attribute.Trim();
            added[At("value")] = value.Trim();
            added[At("valid_from")] = from;
            added[At("sources")] = Facts.SourcesText(sources);
            added[At("note")] = note?.Trim() ?? string.Empty;
            rows.Add(added);
            return new Target(Facts.Path, Csv.Write(table.Header, rows), file?.Sensitivity ?? MemorySensitivity.Normal, Deleted: false);
        });
    }

    /// <summary>The facts that hold now, of one subject or of all.</summary>
    public async Task<MemoryOutcome<IReadOnlyList<Fact>>> CurrentFactsAsync(Guid userId, string? subject, CancellationToken cancellationToken)
    {
        var all = await AllFactsAsync(userId, cancellationToken);
        return all.IsRefused ? all : MemoryOutcome<IReadOnlyList<Fact>>.Ok(all.Value!.Where(f => f.IsOpen && (string.IsNullOrWhiteSpace(subject) || Facts.Same(f.Subject, subject))).ToList());
    }

    /// <summary>Every value a fact of the subject has had, oldest first.</summary>
    public async Task<MemoryOutcome<IReadOnlyList<Fact>>> FactHistoryAsync(Guid userId, string? subject, string? attribute, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return MemoryOutcome<IReadOnlyList<Fact>>.Refused("Say whose facts: a subject.");
        }

        var all = await AllFactsAsync(userId, cancellationToken);
        return all.IsRefused
            ? all
            : MemoryOutcome<IReadOnlyList<Fact>>.Ok(all.Value!
                .Where(f => Facts.Same(f.Subject, subject) && (string.IsNullOrWhiteSpace(attribute) || Facts.Same(f.Attribute, attribute)))
                .OrderBy(f => f.ValidFrom, StringComparer.Ordinal)
                .ToList());
    }

    private async Task<MemoryOutcome<IReadOnlyList<Fact>>> AllFactsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var file = await Store.FindLiveAsync(userId, Facts.Path, cancellationToken);
        if (file is null)
        {
            return MemoryOutcome<IReadOnlyList<Fact>>.Ok([]);
        }

        var facts = Facts.Read(Csv.Check(file.Content));
        return facts is null
            ? MemoryOutcome<IReadOnlyList<Fact>>.Refused($"{Facts.Path} is not a facts collection: it needs the fields {string.Join(", ", Facts.Header)}.")
            : MemoryOutcome<IReadOnlyList<Fact>>.Ok(facts);
    }
}
