using Filum.Engine.Abstractions.Collections;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Facts;
using Filum.Engine.Abstractions.Imports;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Skills;
using Filum.Engine.Collections;
using Filum.Engine.Consolidation;
using Filum.Engine.Events;
using Filum.Engine.Briefing;
using Filum.Engine.Facts;
using Filum.Engine.Forgetting;
using Filum.Engine.Imports;
using Filum.Engine.Memory.Core;
using Filum.Engine.Memory.Files;
using Filum.Engine.Memory.History;
using Filum.Engine.Packs;
using Filum.Engine.Skills;

namespace Filum.Engine.Memory;

// Each call goes to the service that owns it. It holds no state, so one instance serves every person.
internal sealed class MemoryService(
    MemoryFileReader reader,
    MemoryFileEditor editor,
    MemoryIndexBuilder index,
    CollectionRowService collections,
    MemoryHistoryService history,
    MemoryReverter reverter,
    SkillService skills,
    CoreBootstrapper bootstrapper,
    EventService events,
    IEventVectors vectors,
    FactService facts,
    FactRecall recall,
    BriefService briefing,
    ForgetService forgetting,
    ImportService imports,
    ConsolidationService consolidation,
    EnginePack pack) : IMemoryService
{
    public Pack? Pack => pack.Pack;

    public Task<string> EnsureCoreAsync(Guid userId, CancellationToken cancellationToken) => bootstrapper.EnsureCoreAsync(userId, cancellationToken);

    public Task<string> EnsureLearnedAsync(Guid userId, CancellationToken cancellationToken) => bootstrapper.EnsureLearnedAsync(userId, cancellationToken);

    public Task<MemoryOutcome<IReadOnlyList<MemoryFileInfo>>> ListAsync(Guid userId, string? prefix, bool includePrivate, CancellationToken cancellationToken) =>
        reader.ListAsync(userId, prefix, includePrivate, cancellationToken);

    public Task<MemoryOutcome<MemoryReadResult>> ReadAsync(Guid userId, string? path, int? fromLine, int? toLine, CancellationToken cancellationToken) =>
        reader.ReadAsync(userId, path, fromLine, toLine, cancellationToken);

    public Task<string> BuildIndexAsync(Guid userId, CancellationToken cancellationToken) => index.BuildAsync(userId, cancellationToken);

    public Task<MemoryOutcome<MemoryFileContent>> GetFileAsync(Guid userId, string? path, CancellationToken cancellationToken) =>
        reader.GetFileAsync(userId, path, cancellationToken);

    public Task<MemoryOutcome<CsvTable>> ReadCollectionAsync(Guid userId, string? path, CancellationToken cancellationToken) =>
        reader.ReadCollectionAsync(userId, path, cancellationToken);

    public Task<MemoryOutcome<MemorySearchResult>> SearchAsync(Guid userId, string? query, string? prefix, bool includePrivate, CancellationToken cancellationToken) =>
        reader.SearchAsync(userId, query, prefix, includePrivate, cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> WriteAsync(Guid userId, MemoryActor actor, string? path, string? content, CancellationToken cancellationToken) =>
        editor.WriteAsync(userId, actor, path, content, cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> EditAsync(Guid userId, MemoryActor actor, string? path, string? oldText, string? newText, CancellationToken cancellationToken) =>
        editor.EditAsync(userId, actor, path, oldText, newText, cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> AppendAsync(Guid userId, MemoryActor actor, string? path, string? text, CancellationToken cancellationToken) =>
        editor.AppendAsync(userId, actor, path, text, cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> AddRowsAsync(Guid userId, MemoryActor actor, string? path, IReadOnlyList<IReadOnlyDictionary<string, string>> rows, CancellationToken cancellationToken) =>
        collections.AddRowsAsync(userId, actor, path, rows, cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> UpdateRowsAsync(Guid userId, MemoryActor actor, string? path, IReadOnlyDictionary<string, string> where, IReadOnlyDictionary<string, string> set, CancellationToken cancellationToken) =>
        collections.UpdateRowsAsync(userId, actor, path, where, set, cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> RemoveRowsAsync(Guid userId, MemoryActor actor, string? path, IReadOnlyDictionary<string, string> where, CancellationToken cancellationToken) =>
        collections.RemoveRowsAsync(userId, actor, path, where, cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> AddFieldAsync(Guid userId, MemoryActor actor, string? path, string? field, CancellationToken cancellationToken) =>
        collections.AddFieldAsync(userId, actor, path, field, cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> DeleteAsync(Guid userId, MemoryActor actor, string? path, CancellationToken cancellationToken) =>
        editor.DeleteAsync(userId, actor, path, cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> MoveAsync(Guid userId, MemoryActor actor, string? from, string? to, CancellationToken cancellationToken) =>
        editor.MoveAsync(userId, actor, from, to, cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> SetSensitivityAsync(Guid userId, MemoryActor actor, string? path, string? level, CancellationToken cancellationToken) =>
        editor.SetSensitivityAsync(userId, actor, path, level, cancellationToken);

    public Task<IReadOnlyList<SkillFile>> ListSkillsAsync(Guid userId, bool includePrivate, CancellationToken cancellationToken) =>
        skills.ListAsync(userId, includePrivate, cancellationToken);

    public Task<MemoryOutcome<SkillFile>> GetSkillAsync(Guid userId, string? name, CancellationToken cancellationToken) => skills.GetAsync(userId, name, cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> SaveSkillAsync(Guid userId, MemoryActor actor, Skill skill, bool replace, CancellationToken cancellationToken) =>
        skills.SaveAsync(userId, actor, skill, replace, cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> SetSkillEnabledAsync(Guid userId, MemoryActor actor, string? name, bool enabled, CancellationToken cancellationToken) =>
        skills.SetEnabledAsync(userId, actor, name, enabled, cancellationToken);

    public Task<IReadOnlyDictionary<string, LineChange>> LineChangesAsync(Guid userId, string path, CancellationToken cancellationToken) =>
        history.LineChangesAsync(userId, path, cancellationToken);

    public Task<MemoryOutcome<IReadOnlyList<MemoryRevisionInfo>>> GetHistoryAsync(Guid userId, string? path, CancellationToken cancellationToken) =>
        history.GetHistoryAsync(userId, path, cancellationToken);

    public Task<MemoryOutcome<MemoryVersion>> GetRevisionAsync(Guid userId, long revisionId, CancellationToken cancellationToken) =>
        history.GetRevisionAsync(userId, revisionId, cancellationToken);

    public Task<MemoryOutcome<MemoryOrigin>> GetOriginAsync(Guid userId, string? path, CancellationToken cancellationToken) => history.GetOriginAsync(userId, path, cancellationToken);

    public Task<IReadOnlySet<long>> UndoneAmongAsync(Guid userId, IReadOnlyCollection<long> revisionIds, CancellationToken cancellationToken) =>
        history.UndoneAmongAsync(userId, revisionIds, cancellationToken);

    public Task<string?> LastAuthorAsync(Guid userId, string path, CancellationToken cancellationToken) => history.LastAuthorAsync(userId, path, cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> RestoreAsync(Guid userId, MemoryActor actor, long revisionId, CancellationToken cancellationToken) =>
        reverter.RestoreAsync(userId, actor, revisionId, cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> UndoAsync(Guid userId, MemoryActor actor, long revisionId, CancellationToken cancellationToken) =>
        reverter.UndoAsync(userId, actor, revisionId, cancellationToken);

    public Task RollbackAsync(Guid userId, IReadOnlyList<long> revisionIds, CancellationToken cancellationToken) => reverter.RollbackAsync(userId, revisionIds, cancellationToken);

    public Task<MemoryEvent> RecordAsync(Guid userId, NewMemoryEvent next, CancellationToken cancellationToken) => events.RecordAsync(userId, next, cancellationToken);

    public Task<IReadOnlyList<MemoryEvent>> EventsAsync(Guid userId, EventQuery query, CancellationToken cancellationToken) => events.EventsAsync(userId, query, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, string>> ConversationTitlesAsync(Guid userId, IEnumerable<Guid?> conversationIds, CancellationToken cancellationToken) =>
        history.TitlesAsync(userId, conversationIds, cancellationToken);

    public Task<MemoryOutcome<IReadOnlyList<MemoryEventHit>>> SearchEventsAsync(
        Guid userId, string? query, DateTimeOffset? from, DateTimeOffset? before, bool includePrivate, CancellationToken cancellationToken) =>
        events.SearchAsync(userId, query, from, before, includePrivate, cancellationToken);

    public Task<MemoryOutcome<IReadOnlyList<MemoryEvent>>> ListEventsAsync(
        Guid userId, string? query, DateTimeOffset? from, DateTimeOffset? before, bool includePrivate, int max, CancellationToken cancellationToken) =>
        events.ListAsync(userId, query, from, before, includePrivate, max, cancellationToken);

    public Task<int> MakeVectorsAsync(Guid userId, int max, CancellationToken cancellationToken) => vectors.MakeAsync(userId, max, cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> RecordFactAsync(
        Guid userId, MemoryActor actor, string? subject, string? attribute, string? value, string? validFrom, string? note, IReadOnlyList<long> sources, CancellationToken cancellationToken) =>
        RecordFactAsync(userId, actor, subject, attribute, value, validFrom, note, sources, correction: false, cancellationToken);

    public Task<MemoryOutcome<IReadOnlyList<Fact>>> CurrentFactsAsync(Guid userId, string? subject, CancellationToken cancellationToken) =>
        facts.CurrentAsync(userId, subject, null, cancellationToken);

    public Task<MemoryOutcome<IReadOnlyList<Fact>>> CurrentFactsAsync(Guid userId, string? subject, string? on, CancellationToken cancellationToken) =>
        facts.CurrentAsync(userId, subject, on, cancellationToken);

    public Task<MemoryOutcome<IReadOnlyList<Fact>>> PlannedFactsAsync(Guid userId, string? subject, CancellationToken cancellationToken) =>
        facts.PlannedAsync(userId, subject, cancellationToken);

    public async Task<MemoryOutcome<MemoryChange>> RecordFactAsync(
        Guid userId, MemoryActor actor, string? subject, string? attribute, string? value, string? validFrom, string? note, IReadOnlyList<long> sources, bool correction, CancellationToken cancellationToken)
    {
        // The words of the first source, so the fact keeps where it came from (spec 041).
        var quote = sources.Count == 0 ? null
            : (await events.EventsAsync(userId, new EventQuery(AfterId: sources[0] - 1, IncludePrivate: false), cancellationToken)).FirstOrDefault(e => e.Id == sources[0])?.Text;
        return await facts.RecordAsync(userId, actor, new FactService.Asked(subject, attribute, value, validFrom, note, sources, correction, quote), cancellationToken);
    }

    public Task<MemoryOutcome<MemoryChange>> EndFactAsync(Guid userId, MemoryActor actor, string? subject, string? attribute, string? on, bool retract, string? value, CancellationToken cancellationToken) =>
        facts.EndAsync(userId, actor, subject, attribute, on, retract, value, [], cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> EndFactAsync(
        Guid userId, MemoryActor actor, string? subject, string? attribute, string? on, bool retract, string? value, IReadOnlyList<long> sources, CancellationToken cancellationToken) =>
        facts.EndAsync(userId, actor, subject, attribute, on, retract, value, sources, cancellationToken);

    public Task<MemoryOutcome<IReadOnlyList<Fact>>> FactHistoryAsync(Guid userId, string? subject, string? attribute, CancellationToken cancellationToken) =>
        facts.HistoryAsync(userId, subject, attribute, cancellationToken);

    public Task<MemoryOutcome<IReadOnlyList<Fact>>> MatchFactsAsync(Guid userId, string? query, CancellationToken cancellationToken) =>
        recall.MatchAsync(userId, query, cancellationToken);

    public Task<string> BriefAsync(Guid userId, int? maxChars, CancellationToken cancellationToken) => briefing.BriefAsync(userId, maxChars, cancellationToken);

    public Task<string> RecallAsync(Guid userId, string? message, int? maxChars, CancellationToken cancellationToken) =>
        briefing.RecallAsync(userId, message, maxChars, cancellationToken);

    public Task<string> RulesAsync(Guid userId, int? maxChars, CancellationToken cancellationToken) => briefing.RulesAsync(userId, maxChars, cancellationToken);

    public Task ForgetAsync(Guid userId, IReadOnlyCollection<long> eventIds, IReadOnlyCollection<string> paths, CancellationToken cancellationToken) =>
        forgetting.ForgetAsync(userId, eventIds, paths, cancellationToken);

    public Task<int> ForgetWhatWasSaidAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken) =>
        forgetting.ForgetWhatWasSaidAsync(userId, conversationId, cancellationToken);

    public Task<ForgetResult> ForgetConversationAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken) =>
        forgetting.ForgetConversationAsync(userId, conversationId, cancellationToken);

    public Task<MemoryOutcome<ImportResult>> ImportAsync(Guid userId, ImportRequest request, CancellationToken cancellationToken) =>
        imports.ImportAsync(userId, request, cancellationToken);

    public Task<MemoryOutcome<RecordedItems>> RecordItemsAsync(Guid userId, Guid sourceId, string source, IReadOnlyList<ImportItem> items, string sensitivity, CancellationToken cancellationToken) =>
        imports.RecordItemsAsync(userId, sourceId, source, items, sensitivity, cancellationToken);

    public Task<IReadOnlyList<MemoryEvent>> PendingAsync(Guid userId, int max, CancellationToken cancellationToken) => consolidation.PendingAsync(userId, max, cancellationToken);

    public Task<IReadOnlyList<MemoryEvent>> GateAsync(Guid userId, IReadOnlyList<MemoryEvent> pending, CancellationToken cancellationToken) =>
        consolidation.GateAsync(userId, pending, cancellationToken);

    public Task<MemoryEvent> CloseConsolidationAsync(Guid userId, IReadOnlyList<long> read, IReadOnlyList<long> revisions, CancellationToken cancellationToken) =>
        consolidation.CloseAsync(userId, read, revisions, cancellationToken);

    public Task<MemoryEvent> ProposeAsync(Guid userId, string text, IReadOnlyList<long> sources, CancellationToken cancellationToken) =>
        consolidation.ProposeAsync(userId, text, sources, cancellationToken);

    public Task<IReadOnlyList<MemoryEvent>> OpenProposalsAsync(Guid userId, CancellationToken cancellationToken) => consolidation.OpenProposalsAsync(userId, cancellationToken);

    public Task<MemoryOutcome<MemoryEvent>> AnswerProposalAsync(Guid userId, long proposalId, bool accepted, string source, CancellationToken cancellationToken) =>
        consolidation.AnswerProposalAsync(userId, proposalId, accepted, source, cancellationToken);
}
