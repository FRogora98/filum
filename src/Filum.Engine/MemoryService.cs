using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace Filum.Engine;

/// <summary>
/// A person's memory: text files with their full history. Every call names the user; another person's file or
/// revision does not exist. The rules are all here (paths, checks, collections, skills, history, undo); the
/// <see cref="IMemoryStore"/> only persists. Every change adds a revision and bumps the file's version atomically:
/// two changes from the same version cannot both win.
/// </summary>
public sealed partial class MemoryService(
    IMemoryStore store,
    IOptions<MemoryOptions> options,
    ILogger<MemoryService> logger,
    Func<CancellationToken, Task>? beforeSaveForTests = null,
    Pack? pack = null)
{
    private const string ChangedAtTheSameTime = "{0} was changed at the same time by another action; read it again and retry.";

    private MemoryOptions Limits => options.Value;

    private IMemoryStore Store => store;

    private ILogger Logger => logger;

    /// <summary>The package every new memory starts from (spec 016), or null for the generic engine.</summary>
    public Pack? Pack => pack;

    /// <summary>
    /// The person's core, created the first time together with the starter skills and, with a package, its core, files
    /// and skills.
    /// </summary>
    public async Task<string> EnsureCoreAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await store.FindLiveAsync(userId, MemoryPaths.CorePath, cancellationToken) is { } existing)
        {
            return existing.Content;
        }

        foreach (var problem in await CreateMemoryAsync(userId, cancellationToken))
        {
            logger.LogWarning("[ MemoryService ] The package {Pack} could not create {Problem} for user {UserId}", pack?.Name, problem, userId);
        }

        return (await store.FindLiveAsync(userId, MemoryPaths.CorePath, cancellationToken))!.Content;
    }

    /// <summary>
    /// Creates a new memory: the core, the package's files, the starter skills (those the package does not replace) and
    /// the package's skills, all by the platform. Returns what was refused; <see cref="Pack.Load"/> runs it on an empty
    /// memory to check a package with these very rules.
    /// </summary>
    internal async Task<IReadOnlyList<string>> CreateMemoryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var core = Normalize(pack?.Core ?? PlatformInstructions.CoreTemplate);
        var created = await ChangeAsync(userId, MemoryActor.Platform, MemoryPaths.CorePath, MemoryOperation.CreateCore, cancellationToken, (file, _) =>
            file is not null ? null : new Target(MemoryPaths.CorePath, core, MemorySensitivity.Normal, Deleted: false));
        if (created.IsRefused)
        {
            if (await store.FindLiveAsync(userId, MemoryPaths.CorePath, cancellationToken) is not null)
            {
                // Another request created it first: that one made the rest too.
                logger.LogInformation("[ MemoryService ] The core of user {UserId} was created by a concurrent request", userId);
                return [];
            }

            return [$"{MemoryPaths.CorePath}: {created.Refusal}"];
        }

        var problems = new List<string>();
        foreach (var file in pack?.Files ?? [])
        {
            var written = await ChangeAsync(userId, MemoryActor.Platform, file.Path, MemoryOperation.Write, cancellationToken, (_, _) =>
                new Target(file.Path, Normalize(file.Content), file.Sensitivity, Deleted: false));
            if (written.IsRefused)
            {
                problems.Add($"{file.Path}: {written.Refusal}");
            }
        }

        var packSkills = pack?.Skills ?? [];
        foreach (var skill in Skills.Starters.Where(s => packSkills.All(p => p.Name != s.Name)).Concat(packSkills))
        {
            var saved = await SaveSkillAsync(userId, MemoryActor.Platform, skill, replace: false, cancellationToken);
            if (saved.IsRefused)
            {
                problems.Add($"{Skills.PathOf(skill.Name)}: {saved.Refusal}");
            }
        }

        logger.LogInformation("[ MemoryService ] Memory created for user {UserId} ({Pack})", userId, pack is null ? "no package" : $"package {pack.Name} {pack.Version}");
        return problems;
    }

    public async Task<MemoryOutcome<IReadOnlyList<MemoryFileInfo>>> ListAsync(Guid userId, string? prefix, bool includePrivate, CancellationToken cancellationToken)
    {
        if (MemoryPaths.ValidatePrefix(prefix) is { } error)
        {
            return MemoryOutcome<IReadOnlyList<MemoryFileInfo>>.Refused(error);
        }

        var files = (await store.ListLiveAsync(userId, prefix, includePrivate, withContent: false, cancellationToken))
            .OrderBy(f => f.Path, StringComparer.Ordinal)
            .Select(Info)
            .ToList();
        return MemoryOutcome<IReadOnlyList<MemoryFileInfo>>.Ok(files);
    }

    public async Task<MemoryOutcome<MemoryReadResult>> ReadAsync(Guid userId, string? path, int? fromLine, int? toLine, CancellationToken cancellationToken)
    {
        if (MemoryPaths.Validate(path) is { } error)
        {
            return MemoryOutcome<MemoryReadResult>.Refused(error);
        }

        var file = await store.FindLiveAsync(userId, path!, cancellationToken);
        if (file is null)
        {
            return MemoryOutcome<MemoryReadResult>.Missing($"There is no file at {path}.");
        }

        var lines = LinesOf(file.Content);
        var from = Math.Max(1, fromLine ?? 1);
        if (lines.Count > 0 && from > lines.Count)
        {
            return MemoryOutcome<MemoryReadResult>.Refused($"{path} has {lines.Count} lines; start at a line between 1 and {lines.Count}.");
        }

        var to = Math.Min(lines.Count, Math.Min(toLine ?? int.MaxValue, from + Limits.MaxReadLines - 1));
        var slice = to < from ? [] : lines.Skip(from - 1).Take(to - from + 1).ToList();
        return MemoryOutcome<MemoryReadResult>.Ok(new MemoryReadResult(path!, file.Sensitivity, from, Math.Max(to, from - 1), lines.Count, slice));
    }

    /// <summary>
    /// The map of the memory, for the agent's instructions (spec 030): every live, non-private file but the core, newest
    /// first, one line each with what it holds, until the file or character limit; then how many are left out. It is
    /// made from the files themselves at every message, never kept by hand. With it the agent does not need to look
    /// around before it acts.
    /// </summary>
    public async Task<string> BuildIndexAsync(Guid userId, CancellationToken cancellationToken)
    {
        var files = (await store.ListLiveAsync(userId, "/", includePrivate: false, withContent: true, cancellationToken))
            .Where(f => f.Path != MemoryPaths.CorePath && !Skills.IsSkillPath(f.Path))
            .OrderByDescending(f => f.UpdatedAt)
            .ToList();
        if (files.Count == 0)
        {
            return "(nothing yet besides the core)";
        }

        var lines = new List<string>();
        var length = 0;
        foreach (var file in files)
        {
            var line = file.Path == Facts.Path
                ? $"- {file.Path} (facts that hold for a time · {file.RowCount} rows · changed {file.UpdatedAt:yyyy-MM-dd}): use facts_current and facts_history"
                : file.Header is null
                    ? $"- {file.Path} (document · {file.LineCount} lines · changed {file.UpdatedAt:yyyy-MM-dd}){Summary(file.Content)}"
                    : $"- {file.Path} (collection · fields: {file.Header.Replace(",", ", ")} · {file.RowCount} rows · changed {file.UpdatedAt:yyyy-MM-dd})";
            if (lines.Count == Limits.IndexMaxFiles || length + line.Length > Limits.IndexMaxChars)
            {
                break;
            }

            lines.Add(line);
            length += line.Length + 1;
        }

        if (lines.Count < files.Count)
        {
            lines.Add($"- …and {files.Count - lines.Count} older files not listed here: memory_search finds them.");
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// The person's skills, most recently changed first. A file under /skills/ that is not a valid skill (edited by
    /// hand into another shape) is left out; it stays in memory as a document.
    /// </summary>
    public async Task<IReadOnlyList<SkillFile>> ListSkillsAsync(Guid userId, bool includePrivate, CancellationToken cancellationToken)
    {
        var files = (await store.ListLiveAsync(userId, Skills.Folder, includePrivate, withContent: true, cancellationToken))
            .OrderByDescending(f => f.UpdatedAt);
        return files
            .Select(f => (File: f, Parsed: Skills.Parse(f.Content).Skill))
            .Where(f => f.Parsed is not null && Skills.NameOf(f.File.Path) == f.Parsed.Name)
            .Select(f => new SkillFile(f.Parsed!, f.File.Path, f.File.Sensitivity, f.File.UpdatedAt))
            .ToList();
    }

    /// <summary>One skill by name, private ones included.</summary>
    public async Task<MemoryOutcome<SkillFile>> GetSkillAsync(Guid userId, string? name, CancellationToken cancellationToken)
    {
        if (Skills.ValidateName(name) is { } error)
        {
            return MemoryOutcome<SkillFile>.Refused(error);
        }

        var skill = (await ListSkillsAsync(userId, includePrivate: true, cancellationToken)).FirstOrDefault(s => s.Skill.Name == name);
        return skill is null ? MemoryOutcome<SkillFile>.Missing($"There is no skill named '{name}'.") : MemoryOutcome<SkillFile>.Ok(skill);
    }

    /// <summary>
    /// Writes a skill; the code writes its header. A name another skill already has is refused unless
    /// <paramref name="replace"/>: then the skill is changed, keeping its sensitivity.
    /// </summary>
    public Task<MemoryOutcome<MemoryChange>> SaveSkillAsync(Guid userId, MemoryActor actor, Skill skill, bool replace, CancellationToken cancellationToken)
    {
        if (Skills.Validate(skill) is { } error)
        {
            return Task.FromResult(MemoryOutcome<MemoryChange>.Refused(error));
        }

        var path = Skills.PathOf(skill.Name);
        return ChangeAsync(userId, actor, path, MemoryOperation.Write, cancellationToken, (file, _) =>
            file is not null && !replace
                ? Refuse($"There is already a skill named '{skill.Name}'; to change it, save it again with replace, or pick another name.")
                : new Target(path, Skills.Format(skill), file?.Sensitivity ?? MemorySensitivity.Normal, Deleted: false));
    }

    /// <summary>Turns a skill on or off by rewriting its header; its steps stay as they are.</summary>
    public Task<MemoryOutcome<MemoryChange>> SetSkillEnabledAsync(Guid userId, MemoryActor actor, string? name, bool enabled, CancellationToken cancellationToken)
    {
        if (Skills.ValidateName(name) is { } error)
        {
            return Task.FromResult(MemoryOutcome<MemoryChange>.Refused(error));
        }

        var path = Skills.PathOf(name!);
        return ChangeAsync(userId, actor, path, MemoryOperation.Edit, cancellationToken, (file, _) =>
        {
            if (file is null)
            {
                return Missing($"There is no skill named '{name}'.");
            }

            var (skill, invalid) = Skills.Parse(file.Content);
            return invalid is not null ? Refuse($"/{name} is not a valid skill: {invalid}")
                : skill!.Enabled == enabled ? Refuse($"The skill /{name} is already {(enabled ? "on" : "off")}.")
                : new Target(path, Skills.Format(skill with { Enabled = enabled }), file.Sensitivity, Deleted: false);
        });
    }

    /// <summary>A whole file with what the person sees about it, private files included (they are the person's own).</summary>
    public async Task<MemoryOutcome<MemoryFileContent>> GetFileAsync(Guid userId, string? path, CancellationToken cancellationToken)
    {
        if (MemoryPaths.Validate(path) is { } error)
        {
            return MemoryOutcome<MemoryFileContent>.Refused(error);
        }

        var file = await store.FindLiveAsync(userId, path!, cancellationToken);
        return file is null
            ? MemoryOutcome<MemoryFileContent>.Missing($"There is no file at {path}.")
            : MemoryOutcome<MemoryFileContent>.Ok(new MemoryFileContent(Info(file), file.Content));
    }

    /// <summary>A collection, parsed and checked, for computing over it.</summary>
    public async Task<MemoryOutcome<CsvTable>> ReadCollectionAsync(Guid userId, string? path, CancellationToken cancellationToken)
    {
        if (MemoryPaths.Validate(path) is { } error)
        {
            return MemoryOutcome<CsvTable>.Refused(error);
        }

        if (MemoryPaths.KindOf(path!) != MemoryFileKind.Collection)
        {
            return MemoryOutcome<CsvTable>.Refused($"{path} is a document; only collections (.csv) can be computed on.");
        }

        var content = (await store.FindLiveAsync(userId, path!, cancellationToken))?.Content;
        return content is null
            ? MemoryOutcome<CsvTable>.Missing($"There is no file at {path}.")
            : MemoryOutcome<CsvTable>.Ok(Csv.Check(content));
    }

    public async Task<MemoryOutcome<MemorySearchResult>> SearchAsync(Guid userId, string? query, string? prefix, bool includePrivate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 200)
        {
            return MemoryOutcome<MemorySearchResult>.Refused("The query must be between 1 and 200 characters.");
        }

        if (MemoryPaths.ValidatePrefix(prefix) is { } error)
        {
            return MemoryOutcome<MemorySearchResult>.Refused(error);
        }

        var files = (await store.SearchAsync(userId, query, prefix, includePrivate, cancellationToken))
            .OrderByDescending(f => f.UpdatedAt)
            .ToList();

        var hits = new List<MemorySearchHit>();
        foreach (var file in files)
        {
            var lines = LinesOf(file.Content);
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    if (hits.Count == Limits.MaxSearchResults)
                    {
                        return MemoryOutcome<MemorySearchResult>.Ok(new MemorySearchResult(hits, Truncated: true));
                    }

                    hits.Add(new MemorySearchHit(file.Path, i + 1, lines[i]));
                }
            }
        }

        return MemoryOutcome<MemorySearchResult>.Ok(new MemorySearchResult(hits, Truncated: false));
    }

    public Task<MemoryOutcome<MemoryChange>> WriteAsync(Guid userId, MemoryActor actor, string? path, string? content, CancellationToken cancellationToken) =>
        ChangeAsync(userId, actor, path, MemoryOperation.Write, cancellationToken, (file, _) =>
            new Target(path!, Normalize(content), file?.Sensitivity ?? MemorySensitivity.Normal, Deleted: false));

    public Task<MemoryOutcome<MemoryChange>> EditAsync(Guid userId, MemoryActor actor, string? path, string? oldText, string? newText, CancellationToken cancellationToken) =>
        ChangeAsync(userId, actor, path, MemoryOperation.Edit, cancellationToken, (file, _) =>
        {
            if (file is null)
            {
                return Missing($"There is no file at {path}.");
            }

            var old = Normalize(oldText);
            if (old.Length == 0)
            {
                return Refuse("The text to replace cannot be empty.");
            }

            var occurrences = Occurrences(file.Content, old);
            return occurrences switch
            {
                0 => Refuse($"The text to replace was not found in {path}."),
                > 1 => Refuse($"The text to replace appears {occurrences} times in {path}; include more of the text around it so it appears once."),
                _ => new Target(file.Path, ReplaceOnce(file.Content, old, Normalize(newText)), file.Sensitivity, Deleted: false)
            };
        });

    public Task<MemoryOutcome<MemoryChange>> AppendAsync(Guid userId, MemoryActor actor, string? path, string? text, CancellationToken cancellationToken) =>
        ChangeAsync(userId, actor, path, MemoryOperation.Append, cancellationToken, (file, _) =>
        {
            var addition = Normalize(text);
            if (addition.Trim().Length == 0)
            {
                return Refuse("There is nothing to add.");
            }

            if (!addition.EndsWith('\n'))
            {
                addition += "\n";
            }

            var current = file?.Content ?? string.Empty;
            var separator = current.Length == 0 || current.EndsWith('\n') ? string.Empty : "\n";
            return new Target(path!, current + separator + addition, file?.Sensitivity ?? MemorySensitivity.Normal, Deleted: false);
        });

    /// <summary>
    /// Adds rows given as field → value to a collection; the code writes the CSV. A missing collection is created
    /// with the rows' fields; a field the collection does not have is refused, listing its fields.
    /// </summary>
    public Task<MemoryOutcome<MemoryChange>> AddRowsAsync(Guid userId, MemoryActor actor, string? path, IReadOnlyList<IReadOnlyDictionary<string, string>> rows, CancellationToken cancellationToken) =>
        ChangeAsync(userId, actor, path, MemoryOperation.Append, cancellationToken, (file, _) =>
        {
            if (NotACollection(path!) is { } refusal)
            {
                return refusal;
            }

            if (rows.Count == 0 || rows.Any(r => r.Count == 0))
            {
                return Refuse("Give at least one row, each with at least one field.");
            }

            if (file is null)
            {
                var header = rows.SelectMany(r => r.Keys).Select(k => k.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                return new Target(path!, Csv.Write(header, rows.Select(r => Row(header, r))), MemorySensitivity.Normal, Deleted: false);
            }

            var table = Csv.Check(file.Content);
            if (!table.IsValid)
            {
                return Refuse($"{path} is not a valid collection: {table.Error}");
            }

            if (UnknownFields(path!, table, rows.SelectMany(r => r.Keys)) is { } unknown)
            {
                return unknown;
            }

            var all = table.Rows.Select(r => r.Fields).Concat(rows.Select(r => Row(table.Header, r)));
            return new Target(file.Path, Csv.Write(table.Header, all), file.Sensitivity, Deleted: false);
        });

    /// <summary>Changes, in every row whose fields equal all of <paramref name="where"/>, the fields of <paramref name="set"/>.</summary>
    public Task<MemoryOutcome<MemoryChange>> UpdateRowsAsync(Guid userId, MemoryActor actor, string? path, IReadOnlyDictionary<string, string> where, IReadOnlyDictionary<string, string> set, CancellationToken cancellationToken) =>
        ChangeRowsAsync(userId, actor, path, where, set.Keys, cancellationToken, (table, matches) =>
        {
            if (set.Count == 0)
            {
                return Refuse("Say which fields to change and their new values.");
            }

            var rows = table.Rows.Select((r, i) =>
            {
                if (!matches.Contains(i))
                {
                    return r.Fields;
                }

                var fields = r.Fields.ToArray();
                foreach (var (field, value) in set)
                {
                    fields[IndexOf(table, field)] = value;
                }

                return (IReadOnlyList<string>)fields;
            });
            return Csv.Write(table.Header, rows);
        });

    /// <summary>Removes every row whose fields equal all of <paramref name="where"/>.</summary>
    public Task<MemoryOutcome<MemoryChange>> RemoveRowsAsync(Guid userId, MemoryActor actor, string? path, IReadOnlyDictionary<string, string> where, CancellationToken cancellationToken) =>
        ChangeRowsAsync(userId, actor, path, where, [], cancellationToken, (table, matches) =>
            Csv.Write(table.Header, table.Rows.Where((_, i) => !matches.Contains(i)).Select(r => r.Fields)));

    /// <summary>Adds a field to a collection; existing rows get an empty value.</summary>
    public Task<MemoryOutcome<MemoryChange>> AddFieldAsync(Guid userId, MemoryActor actor, string? path, string? field, CancellationToken cancellationToken) =>
        ChangeAsync(userId, actor, path, MemoryOperation.Edit, cancellationToken, (file, _) =>
        {
            if (NotACollection(path!) is { } refusal)
            {
                return refusal;
            }

            if (file is null)
            {
                return Missing($"There is no file at {path}.");
            }

            var name = field?.Trim() ?? string.Empty;
            var table = Csv.Check(file.Content);
            if (!table.IsValid)
            {
                return Refuse($"{path} is not a valid collection: {table.Error}");
            }

            if (name.Length == 0 || table.Header.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                return Refuse(name.Length == 0 ? "Give the name of the field to add." : $"{path} already has the field '{name}'.");
            }

            return new Target(file.Path, Csv.Write([.. table.Header, name], table.Rows.Select(r => (IReadOnlyList<string>)[.. r.Fields, string.Empty])), file.Sensitivity, Deleted: false);
        });

    private Task<MemoryOutcome<MemoryChange>> ChangeRowsAsync(
        Guid userId, MemoryActor actor, string? path, IReadOnlyDictionary<string, string> where, IEnumerable<string> alsoFields, CancellationToken cancellationToken,
        Func<CsvTable, HashSet<int>, object> change) =>
        ChangeAsync(userId, actor, path, MemoryOperation.Edit, cancellationToken, (file, _) =>
        {
            if (NotACollection(path!) is { } refusal)
            {
                return refusal;
            }

            if (file is null)
            {
                return Missing($"There is no file at {path}.");
            }

            if (where.Count == 0)
            {
                return Refuse("Say which rows: give one or more fields and the values they have.");
            }

            var table = Csv.Check(file.Content);
            if (!table.IsValid)
            {
                return Refuse($"{path} is not a valid collection: {table.Error}");
            }

            if (UnknownFields(path!, table, where.Keys.Concat(alsoFields)) is { } unknown)
            {
                return unknown;
            }

            var matches = table.Rows
                .Select((r, i) => (r, i))
                .Where(x => where.All(w => string.Equals(x.r.Fields[IndexOf(table, w.Key)].Trim(), w.Value.Trim(), StringComparison.OrdinalIgnoreCase)))
                .Select(x => x.i)
                .ToHashSet();
            if (matches.Count == 0)
            {
                return Refuse($"No row of {path} has {string.Join(" and ", where.Select(w => $"{w.Key} = '{w.Value}'"))}; read it to see its rows.");
            }

            return change(table, matches) switch
            {
                Target target => target,
                string content => new Target(file.Path, content, file.Sensitivity, Deleted: false),
                _ => Refuse("Nothing to change.")
            };
        });

    private static Target? NotACollection(string path) =>
        MemoryPaths.KindOf(path) == MemoryFileKind.Collection ? null : Refuse($"{path} is a document; rows belong in a collection, a path ending in .csv.");

    private static Target? UnknownFields(string path, CsvTable table, IEnumerable<string> fields)
    {
        var unknown = fields.Select(f => f.Trim()).Where(f => !table.Header.Contains(f, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return unknown.Count == 0
            ? null
            : Refuse($"{path} has no field {string.Join(", ", unknown.Select(u => $"'{u}'"))}; its fields are {string.Join(", ", table.Header)}. Add a field on purpose with collection_add_field.");
    }

    private static int IndexOf(CsvTable table, string field) =>
        table.Header.ToList().FindIndex(h => string.Equals(h, field.Trim(), StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> Row(IReadOnlyList<string> header, IReadOnlyDictionary<string, string> values) =>
        header.Select(h => values.FirstOrDefault(v => string.Equals(v.Key.Trim(), h, StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty).ToList();

    public Task<MemoryOutcome<MemoryChange>> DeleteAsync(Guid userId, MemoryActor actor, string? path, CancellationToken cancellationToken) =>
        ChangeAsync(userId, actor, path, MemoryOperation.Delete, cancellationToken, (file, _) =>
            MemoryPaths.IsCore(path!) ? Refuse("The core (/filum.md) cannot be deleted; edit it instead.")
            : file is null ? Missing($"There is no file at {path}.")
            : new Target(file.Path, file.Content, file.Sensitivity, Deleted: true));

    public async Task<MemoryOutcome<MemoryChange>> MoveAsync(Guid userId, MemoryActor actor, string? from, string? to, CancellationToken cancellationToken)
    {
        if (MemoryPaths.Validate(to) is { } error)
        {
            return MemoryOutcome<MemoryChange>.Refused(error);
        }

        if (MemoryPaths.IsCore(from ?? string.Empty) || MemoryPaths.IsCore(to!))
        {
            return MemoryOutcome<MemoryChange>.Refused("The core (/filum.md) cannot be moved, and no file can take its place.");
        }

        if (from == to)
        {
            return MemoryOutcome<MemoryChange>.Refused("The file is already at that path.");
        }

        return await ChangeAsync(userId, actor, from, MemoryOperation.Move, cancellationToken, otherPath: to, decide: (file, targetTaken) =>
            file is null ? Missing($"There is no file at {from}.")
            : targetTaken ? Refuse($"There is already a file at {to}.")
            : new Target(to!, file.Content, file.Sensitivity, Deleted: false));
    }

    public Task<MemoryOutcome<MemoryChange>> SetSensitivityAsync(Guid userId, MemoryActor actor, string? path, string? level, CancellationToken cancellationToken) =>
        ChangeAsync(userId, actor, path, MemoryOperation.Sensitivity, cancellationToken, (file, _) =>
            !MemorySensitivity.All.Contains(level) ? Refuse("The level must be normal, sensitive or private.")
            : MemoryPaths.IsCore(path!) ? Refuse("The core is read at every message; its sensitivity cannot change.")
            : file is null ? Missing($"There is no file at {path}.")
            : new Target(file.Path, file.Content, level!, Deleted: false));

    /// <summary>The history of the file now at <paramref name="path"/>, newest first.</summary>
    public async Task<MemoryOutcome<IReadOnlyList<MemoryRevisionInfo>>> GetHistoryAsync(Guid userId, string? path, CancellationToken cancellationToken)
    {
        if (MemoryPaths.Validate(path) is { } error)
        {
            return MemoryOutcome<IReadOnlyList<MemoryRevisionInfo>>.Refused(error);
        }

        var live = await store.FindLiveAsync(userId, path!, cancellationToken);
        if (live is null)
        {
            return MemoryOutcome<IReadOnlyList<MemoryRevisionInfo>>.Missing($"There is no file at {path}.");
        }

        var revisions = await store.RevisionsOfFileAsync(userId, live.Id, cancellationToken);
        var titles = await TitlesAsync(userId, revisions.Select(r => r.ConversationId), cancellationToken);

        var history = new List<MemoryRevisionInfo>();
        StoredRevision? previous = null;
        foreach (var r in revisions)
        {
            history.Add(new MemoryRevisionInfo(r.Id, r.Path, r.Operation, r.Author, r.ConversationId, r.MessageId, r.Deleted, r.Sensitivity, r.UndoesRevisionId, r.CreatedAt,
                Summarize(r, previous), r.ConversationId is { } id ? titles.GetValueOrDefault(id) : null));
            previous = r;
        }

        history.Reverse();
        return MemoryOutcome<IReadOnlyList<MemoryRevisionInfo>>.Ok(history);
    }

    /// <summary>One version of a file, for reading it before restoring it.</summary>
    public async Task<MemoryOutcome<MemoryVersion>> GetRevisionAsync(Guid userId, long revisionId, CancellationToken cancellationToken)
    {
        var version = await store.GetRevisionAsync(userId, revisionId, cancellationToken) is { } r
            ? new MemoryVersion(r.Id, r.Path, r.Content, r.Deleted, r.CreatedAt)
            : null;
        return version is null ? MemoryOutcome<MemoryVersion>.Missing($"There is no change {revisionId}.") : MemoryOutcome<MemoryVersion>.Ok(version);
    }

    public async Task<MemoryOutcome<MemoryOrigin>> GetOriginAsync(Guid userId, string? path, CancellationToken cancellationToken)
    {
        if (MemoryPaths.Validate(path) is { } error)
        {
            return MemoryOutcome<MemoryOrigin>.Refused(error);
        }

        var live = await store.FindLiveAsync(userId, path!, cancellationToken);
        if (live is null)
        {
            return MemoryOutcome<MemoryOrigin>.Missing($"There is no file at {path}.");
        }

        var first = (await store.RevisionsOfFileAsync(userId, live.Id, cancellationToken))[0];
        var titles = await TitlesAsync(userId, [first.ConversationId], cancellationToken);
        return MemoryOutcome<MemoryOrigin>.Ok(new MemoryOrigin(first.Author, first.ConversationId is { } id ? titles.GetValueOrDefault(id) : null, first.CreatedAt));
    }

    /// <summary>Which of these revisions were taken back by an undo.</summary>
    public async Task<IReadOnlySet<long>> UndoneAmongAsync(Guid userId, IReadOnlyCollection<long> revisionIds, CancellationToken cancellationToken)
    {
        if (revisionIds.Count == 0)
        {
            return new HashSet<long>();
        }

        return await store.UndoneAmongAsync(userId, revisionIds, cancellationToken);
    }

    /// <summary>
    /// Brings back the version of <paramref name="revisionId"/>, as a new revision by <paramref name="actor"/>. Unlike an
    /// undo it is allowed after later changes: the person picked this exact version.
    /// </summary>
    public async Task<MemoryOutcome<MemoryChange>> RestoreAsync(Guid userId, MemoryActor actor, long revisionId, CancellationToken cancellationToken)
    {
        var revision = await store.GetRevisionAsync(userId, revisionId, cancellationToken);
        if (revision is null)
        {
            return MemoryOutcome<MemoryChange>.Missing($"There is no change {revisionId}.");
        }

        if (revision.Deleted)
        {
            return MemoryOutcome<MemoryChange>.Refused("That version is the deletion of the file; pick an earlier one.");
        }

        return await ChangeFileAsync(userId, actor, revision.FileId, MemoryOperation.Restore, null, revision.Path, cancellationToken, (_, pathTaken) =>
            pathTaken
                ? Refuse($"Another file now uses {revision.Path}.")
                : new Target(revision.Path, revision.Content, revision.Sensitivity, Deleted: false));
    }

    private async Task<IReadOnlyDictionary<Guid, string>> TitlesAsync(Guid userId, IEnumerable<Guid?> conversationIds, CancellationToken cancellationToken)
    {
        var ids = conversationIds.OfType<Guid>().Distinct().ToList();
        return ids.Count == 0 ? new Dictionary<Guid, string>() : await store.ConversationTitlesAsync(userId, ids, cancellationToken);
    }

    private static string CollectionEdit(StoredRevision revision, StoredRevision previous, int rowsDelta)
    {
        static string Header(string content) => content.Split('\n', 2)[0].Trim();
        string Rows(int n) => n == 1 ? "1 row" : $"{n} rows";
        return rowsDelta < 0 ? $"Removed {Rows(-rowsDelta)}"
            : rowsDelta > 0 ? $"Added {Rows(rowsDelta)}"
            : Header(revision.Content) != Header(previous.Content) ? "Changed the fields"
            : "Changed rows";
    }

    /// <summary>What a revision did, in a few words, by comparing it with the one before.</summary>
    private static string Summarize(StoredRevision revision, StoredRevision? previous)
    {
        var collection = MemoryPaths.KindOf(revision.Path) == MemoryFileKind.Collection;
        int Units(StoredRevision? r) => r is null || r.Deleted ? 0
            : collection ? Csv.Check(r.Content) is { IsValid: true } table ? table.Rows.Count : 0
            : LinesOf(r.Content).Count;
        string Count(int n) => n == 1 ? $"1 {(collection ? "row" : "line")}" : $"{n} {(collection ? "rows" : "lines")}";

        return revision.Operation switch
        {
            MemoryOperation.CreateCore => "Created",
            MemoryOperation.Write => previous is null || previous.Deleted ? "Created" : "Rewrote",
            MemoryOperation.Edit when Skills.IsSkillPath(revision.Path) && previous is not null && !previous.Deleted
                && Skills.Parse(revision.Content).Skill is { } now && Skills.Parse(previous.Content).Skill is { } before && now.Enabled != before.Enabled => now.Enabled ? "Turned on" : "Turned off",
            MemoryOperation.Edit when collection && previous is not null && !previous.Deleted => CollectionEdit(revision, previous, Units(revision) - Units(previous)),
            MemoryOperation.Edit => "Edited",
            MemoryOperation.Append when previous is null || previous.Deleted => $"Created with {Count(Units(revision))}",
            MemoryOperation.Append => $"Added {Count(Math.Max(0, Units(revision) - Units(previous)))}",
            MemoryOperation.Delete => "Deleted",
            MemoryOperation.Move => previous is null ? "Moved" : $"Moved from {previous.Path}",
            MemoryOperation.Sensitivity => $"Marked {revision.Sensitivity}",
            MemoryOperation.Undo => "Undid a change",
            MemoryOperation.Rollback => "Took back the changes of a failed answer",
            MemoryOperation.Restore => "Restored an earlier version",
            MemoryOperation.Outside => revision.Deleted ? "Deleted outside Filum" : previous is null || previous.Deleted ? "Created outside Filum" : "Changed outside Filum",
            _ => revision.Operation
        };
    }

    /// <summary>
    /// Takes back the change of one revision, as a new revision. Refused when the file changed again afterwards:
    /// undoing would silently lose that later change.
    /// </summary>
    public Task<MemoryOutcome<MemoryChange>> UndoAsync(Guid userId, MemoryActor actor, long revisionId, CancellationToken cancellationToken) =>
        TakeBackAsync(userId, actor, revisionId, new HashSet<long> { revisionId }, MemoryOperation.Undo, cancellationToken);

    /// <summary>
    /// Takes back the changes of a failed turn: every file it touched returns to its state before the turn, as one
    /// rollback revision per file. A file changed by someone else after the turn is kept as it is, and logged.
    /// </summary>
    public async Task RollbackAsync(Guid userId, IReadOnlyList<long> revisionIds, CancellationToken cancellationToken)
    {
        if (revisionIds.Count == 0)
        {
            return;
        }

        var turn = (await store.RevisionsAsync(userId, revisionIds, cancellationToken)).Select(r => (r.FileId, r.Id)).ToList();

        foreach (var file in turn.GroupBy(r => r.FileId))
        {
            var ids = file.Select(r => r.Id).ToHashSet();
            var outcome = await TakeBackAsync(userId, MemoryActor.Platform, ids.Min(), ids, MemoryOperation.Rollback, cancellationToken);
            if (outcome.IsRefused)
            {
                logger.LogWarning("[ MemoryService ] A file of user {UserId} changed by a failed turn could not be rolled back and is kept (revisions {RevisionIds})", userId, string.Join(",", ids));
            }
        }
    }

    /// <summary>
    /// Brings a file back to its state before <paramref name="revisionId"/>. Allowed only while the file's latest
    /// revision is one of <paramref name="ownRevisions"/>: otherwise someone changed it since, and that would be lost.
    /// </summary>
    private async Task<MemoryOutcome<MemoryChange>> TakeBackAsync(Guid userId, MemoryActor actor, long revisionId, IReadOnlySet<long> ownRevisions, string operation, CancellationToken cancellationToken)
    {
        var revision = await store.GetRevisionAsync(userId, revisionId, cancellationToken);
        if (revision is null)
        {
            return MemoryOutcome<MemoryChange>.Missing($"There is no change {revisionId}.");
        }

        var ofFile = await store.RevisionsOfFileAsync(userId, revision.FileId, cancellationToken);
        var latest = ofFile[^1];
        var previous = ofFile.LastOrDefault(r => r.Id < revision.Id);

        if (!ownRevisions.Contains(latest.Id))
        {
            return MemoryOutcome<MemoryChange>.Refused(
                $"{latest.Path} changed again after that ({Describe(latest)} at {latest.CreatedAt:yyyy-MM-dd HH:mm} UTC, change {latest.Id}); undo that first.");
        }

        return await ChangeFileAsync(userId, actor, revision.FileId, operation, revision.Id, previous?.Path, cancellationToken, (file, previousPathTaken) =>
            previous is null
                ? new Target(file.Path, file.Content, file.Sensitivity, Deleted: true)
                : !previous.Deleted && previousPathTaken
                    ? Refuse($"Another file now uses {previous.Path}.")
                    : new Target(previous.Path, previous.Content, previous.Sensitivity, previous.Deleted));
    }

    private sealed record Target(string Path, string Content, string Sensitivity, bool Deleted, string? Refusal = null, bool Missing = false);

    private static Target Refuse(string reason) => new(string.Empty, string.Empty, string.Empty, false, reason);

    private static Target Missing(string reason) => new(string.Empty, string.Empty, string.Empty, false, reason, Missing: true);

    /// <summary>
    /// A change to the live file at <paramref name="path"/>, or a new file there. <paramref name="decide"/> gets the
    /// file (null when there is none) and whether <paramref name="otherPath"/> is taken by another live file.
    /// </summary>
    private async Task<MemoryOutcome<MemoryChange>> ChangeAsync(
        Guid userId, MemoryActor actor, string? path, string operation, CancellationToken cancellationToken,
        Func<StoredFile?, bool, Target?> decide, string? otherPath = null)
    {
        if (MemoryPaths.Validate(path) is { } error)
        {
            return MemoryOutcome<MemoryChange>.Refused(error);
        }

        var file = await store.FindLiveAsync(userId, path!, cancellationToken);
        var taken = otherPath is not null && await store.IsPathTakenAsync(userId, otherPath, null, cancellationToken);
        return await SaveAsync(userId, actor, operation, null, path!, file, decide(file, taken), cancellationToken);
    }

    /// <summary>A change to one file by id, deleted or not (undo and rollback).</summary>
    private async Task<MemoryOutcome<MemoryChange>> ChangeFileAsync(
        Guid userId, MemoryActor actor, Guid fileId, string operation, long? undoes, string? otherPath, CancellationToken cancellationToken,
        Func<StoredFile, bool, Target> decide)
    {
        var file = await store.FindAsync(userId, fileId, cancellationToken)
            ?? throw new InvalidOperationException($"The file {fileId} of a revision is missing from the store.");
        var taken = otherPath is not null && await store.IsPathTakenAsync(userId, otherPath, fileId, cancellationToken);
        return await SaveAsync(userId, actor, operation, undoes, null, file, decide(file, taken), cancellationToken);
    }

    private async Task<MemoryOutcome<MemoryChange>> SaveAsync(
        Guid userId, MemoryActor actor, string operation, long? undoes, string? pathForMessages, StoredFile? file, Target? target, CancellationToken cancellationToken)
    {
        if (target is null)
        {
            return MemoryOutcome<MemoryChange>.Refused("Nothing to change.");
        }

        if (target.Refusal is not null)
        {
            return target.Missing ? MemoryOutcome<MemoryChange>.Missing(target.Refusal) : MemoryOutcome<MemoryChange>.Refused(target.Refusal);
        }

        var check = CheckContent(target.Path, target.Content);
        if (check.Error is not null && !target.Deleted)
        {
            return MemoryOutcome<MemoryChange>.Refused(check.Error);
        }

        var created = file is null;
        if (created || (!file!.IsLive && !target.Deleted))
        {
            var count = await store.CountLiveAsync(userId, cancellationToken);
            if (count >= Limits.MaxFilesPerUser)
            {
                return MemoryOutcome<MemoryChange>.Refused($"The memory is full ({Limits.MaxFilesPerUser} files); merge or delete files before adding new ones.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var fromPath = file?.Path;
        var oldRows = file?.IsLive != false ? file?.RowCount ?? 0 : 0;
        var oldLines = file?.IsLive != false ? file?.LineCount ?? 0 : 0;
        if (beforeSaveForTests is not null)
        {
            await beforeSaveForTests(cancellationToken);
        }

        var next = new FileState(target.Path, target.Content, target.Sensitivity, target.Deleted, check.SizeBytes, check.LineCount, check.Header, check.RowCount);
        var saved = await store.SaveAsync(userId, file, next, actor, operation, undoes, now, cancellationToken);
        if (saved is null)
        {
            logger.LogInformation("[ MemoryService ] A {Operation} by {Author} for user {UserId} lost a race with another change", operation, actor.Author, userId);
            return MemoryOutcome<MemoryChange>.Refused(string.Format(ChangedAtTheSameTime, pathForMessages ?? target.Path));
        }

        var added = MemoryPaths.KindOf(target.Path) == MemoryFileKind.Collection
            ? (check.RowCount ?? 0) - oldRows
            : check.LineCount - oldLines;
        return MemoryOutcome<MemoryChange>.Ok(new MemoryChange(
            saved.RevisionId, target.Path, operation, created, check.LineCount, check.RowCount, added,
            fromPath != target.Path ? fromPath : null, target.Sensitivity));
    }

    private sealed record ContentCheck(string? Error, int SizeBytes, int LineCount, string? Header, int? RowCount);

    private ContentCheck CheckContent(string path, string content)
    {
        var size = Encoding.UTF8.GetByteCount(content);
        var lines = LinesOf(content).Count;
        if (size > Limits.MaxFileBytes)
        {
            return new ContentCheck($"{path} would be {size / 1024} KB, over the limit of {Limits.MaxFileBytes / 1024} KB; split it into smaller files.", size, lines, null, null);
        }

        if (MemoryPaths.IsCore(path) && content.Length > Limits.MaxCoreChars)
        {
            return new ContentCheck(
                $"The core would be {content.Length} characters, over the limit of {Limits.MaxCoreChars}; condense it, keeping the essentials and moving details into other files.",
                size, lines, null, null);
        }

        if (Skills.IsSkillPath(path) && Skills.CheckFile(path, content, Limits.MaxSkillChars) is { } skillError)
        {
            return new ContentCheck(skillError, size, lines, null, null);
        }

        if (MemoryPaths.KindOf(path) == MemoryFileKind.Collection)
        {
            var table = Csv.Check(content);
            return table.IsValid
                ? new ContentCheck(null, size, lines, string.Join(",", table.Header), table.Rows.Count)
                : new ContentCheck($"{path} is not a valid collection: {table.Error}", size, lines, null, null);
        }

        return new ContentCheck(null, size, lines, null, null);
    }

    /// <summary>What a document holds, from itself: its first line of text, or its title when it has only headings.</summary>
    private static string Summary(string content)
    {
        var lines = LinesOf(content).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        var text = lines.FirstOrDefault(l => !l.StartsWith('#')) ?? lines.FirstOrDefault()?.TrimStart('#').Trim();
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return ": " + (text.Length > 100 ? text[..100] + "…" : text);
    }

    private static MemoryFileInfo Info(StoredFile f) => new(f.Path, f.SizeBytes, f.LineCount, f.Sensitivity, f.UpdatedAt, f.Header, f.RowCount);

    /// <summary>The lines of a text; a final line break does not start another line.</summary>
    public static IReadOnlyList<string> LinesOf(string content)
    {
        if (content.Length == 0)
        {
            return [];
        }

        var lines = content.Split('\n');
        return content.EndsWith('\n') ? lines[..^1] : lines;
    }

    private static string Normalize(string? text) => (text ?? string.Empty).Replace("\r\n", "\n");

    private static int Occurrences(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static string ReplaceOnce(string text, string oldValue, string newValue)
    {
        var index = text.IndexOf(oldValue, StringComparison.Ordinal);
        return text[..index] + newValue + text[(index + oldValue.Length)..];
    }

    private static string Describe(StoredRevision revision) =>
        $"{revision.Operation} by {(revision.Author == MemoryAuthor.Agent ? "Filum" : revision.Author == MemoryAuthor.Person ? "you" : "the platform")}";
}
