using Filum.Engine.Abstractions.Collections;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Facts;
using Filum.Engine.Abstractions.Imports;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Skills;
using Filum.Engine.Memory.Contracts;
using Filum.Engine.Packs;

namespace Filum.Engine.Memory;

// The members of 1.0 stay declared on IMemoryService itself, so code built against it keeps running; each role's
// member reaches the service's own here, so any implementation of IMemoryService serves every role as it is.
public partial interface IMemoryService
{
    Pack? IMemoryCore.Pack => Pack;

    Task<string> IMemoryCore.EnsureCoreAsync(Guid userId, CancellationToken cancellationToken) =>
        EnsureCoreAsync(userId, cancellationToken);

    Task<string> IMemoryCore.EnsureLearnedAsync(Guid userId, CancellationToken cancellationToken) =>
        EnsureLearnedAsync(userId, cancellationToken);

    Task<MemoryOutcome<IReadOnlyList<MemoryFileInfo>>> IMemoryReader.ListAsync(Guid userId, string? prefix, bool includePrivate, CancellationToken cancellationToken) =>
        ListAsync(userId, prefix, includePrivate, cancellationToken);

    Task<MemoryOutcome<MemoryReadResult>> IMemoryReader.ReadAsync(Guid userId, string? path, int? fromLine, int? toLine, CancellationToken cancellationToken) =>
        ReadAsync(userId, path, fromLine, toLine, cancellationToken);

    Task<string> IMemoryReader.BuildIndexAsync(Guid userId, CancellationToken cancellationToken) =>
        BuildIndexAsync(userId, cancellationToken);

    Task<MemoryOutcome<MemoryFileContent>> IMemoryReader.GetFileAsync(Guid userId, string? path, CancellationToken cancellationToken) =>
        GetFileAsync(userId, path, cancellationToken);

    Task<MemoryOutcome<CsvTable>> IMemoryReader.ReadCollectionAsync(Guid userId, string? path, CancellationToken cancellationToken) =>
        ReadCollectionAsync(userId, path, cancellationToken);

    Task<MemoryOutcome<MemorySearchResult>> IMemoryReader.SearchAsync(Guid userId, string? query, string? prefix, bool includePrivate, CancellationToken cancellationToken) =>
        SearchAsync(userId, query, prefix, includePrivate, cancellationToken);

    Task<MemoryOutcome<MemoryChange>> IMemoryWriter.WriteAsync(Guid userId, MemoryActor actor, string? path, string? content, CancellationToken cancellationToken) =>
        WriteAsync(userId, actor, path, content, cancellationToken);

    Task<MemoryOutcome<MemoryChange>> IMemoryWriter.EditAsync(Guid userId, MemoryActor actor, string? path, string? oldText, string? newText, CancellationToken cancellationToken) =>
        EditAsync(userId, actor, path, oldText, newText, cancellationToken);

    Task<MemoryOutcome<MemoryChange>> IMemoryWriter.AppendAsync(Guid userId, MemoryActor actor, string? path, string? text, CancellationToken cancellationToken) =>
        AppendAsync(userId, actor, path, text, cancellationToken);

    Task<MemoryOutcome<MemoryChange>> IMemoryWriter.AddRowsAsync(Guid userId, MemoryActor actor, string? path, IReadOnlyList<IReadOnlyDictionary<string, string>> rows, CancellationToken cancellationToken) =>
        AddRowsAsync(userId, actor, path, rows, cancellationToken);

    Task<MemoryOutcome<MemoryChange>> IMemoryWriter.UpdateRowsAsync(Guid userId, MemoryActor actor, string? path, IReadOnlyDictionary<string, string> where, IReadOnlyDictionary<string, string> set, CancellationToken cancellationToken) =>
        UpdateRowsAsync(userId, actor, path, where, set, cancellationToken);

    Task<MemoryOutcome<MemoryChange>> IMemoryWriter.RemoveRowsAsync(Guid userId, MemoryActor actor, string? path, IReadOnlyDictionary<string, string> where, CancellationToken cancellationToken) =>
        RemoveRowsAsync(userId, actor, path, where, cancellationToken);

    Task<MemoryOutcome<MemoryChange>> IMemoryWriter.AddFieldAsync(Guid userId, MemoryActor actor, string? path, string? field, CancellationToken cancellationToken) =>
        AddFieldAsync(userId, actor, path, field, cancellationToken);

    Task<MemoryOutcome<MemoryChange>> IMemoryWriter.DeleteAsync(Guid userId, MemoryActor actor, string? path, CancellationToken cancellationToken) =>
        DeleteAsync(userId, actor, path, cancellationToken);

    Task<MemoryOutcome<MemoryChange>> IMemoryWriter.MoveAsync(Guid userId, MemoryActor actor, string? from, string? to, CancellationToken cancellationToken) =>
        MoveAsync(userId, actor, from, to, cancellationToken);

    Task<MemoryOutcome<MemoryChange>> IMemoryWriter.SetSensitivityAsync(Guid userId, MemoryActor actor, string? path, string? level, CancellationToken cancellationToken) =>
        SetSensitivityAsync(userId, actor, path, level, cancellationToken);

    Task<IReadOnlyList<SkillFile>> IMemorySkills.ListSkillsAsync(Guid userId, bool includePrivate, CancellationToken cancellationToken) =>
        ListSkillsAsync(userId, includePrivate, cancellationToken);

    Task<MemoryOutcome<SkillFile>> IMemorySkills.GetSkillAsync(Guid userId, string? name, CancellationToken cancellationToken) =>
        GetSkillAsync(userId, name, cancellationToken);

    Task<MemoryOutcome<MemoryChange>> IMemorySkills.SaveSkillAsync(Guid userId, MemoryActor actor, Skill skill, bool replace, CancellationToken cancellationToken) =>
        SaveSkillAsync(userId, actor, skill, replace, cancellationToken);

    Task<MemoryOutcome<MemoryChange>> IMemorySkills.SetSkillEnabledAsync(Guid userId, MemoryActor actor, string? name, bool enabled, CancellationToken cancellationToken) =>
        SetSkillEnabledAsync(userId, actor, name, enabled, cancellationToken);

    Task<IReadOnlyDictionary<string, LineChange>> IMemoryHistory.LineChangesAsync(Guid userId, string path, CancellationToken cancellationToken) =>
        LineChangesAsync(userId, path, cancellationToken);

    Task<MemoryOutcome<IReadOnlyList<MemoryRevisionInfo>>> IMemoryHistory.GetHistoryAsync(Guid userId, string? path, CancellationToken cancellationToken) =>
        GetHistoryAsync(userId, path, cancellationToken);

    Task<MemoryOutcome<MemoryVersion>> IMemoryHistory.GetRevisionAsync(Guid userId, long revisionId, CancellationToken cancellationToken) =>
        GetRevisionAsync(userId, revisionId, cancellationToken);

    Task<MemoryOutcome<MemoryOrigin>> IMemoryHistory.GetOriginAsync(Guid userId, string? path, CancellationToken cancellationToken) =>
        GetOriginAsync(userId, path, cancellationToken);

    Task<IReadOnlySet<long>> IMemoryHistory.UndoneAmongAsync(Guid userId, IReadOnlyCollection<long> revisionIds, CancellationToken cancellationToken) =>
        UndoneAmongAsync(userId, revisionIds, cancellationToken);

    Task<string?> IMemoryHistory.LastAuthorAsync(Guid userId, string path, CancellationToken cancellationToken) =>
        LastAuthorAsync(userId, path, cancellationToken);

    Task<MemoryOutcome<MemoryChange>> IMemoryHistory.RestoreAsync(Guid userId, MemoryActor actor, long revisionId, CancellationToken cancellationToken) =>
        RestoreAsync(userId, actor, revisionId, cancellationToken);

    Task<MemoryOutcome<MemoryChange>> IMemoryHistory.UndoAsync(Guid userId, MemoryActor actor, long revisionId, CancellationToken cancellationToken) =>
        UndoAsync(userId, actor, revisionId, cancellationToken);

    Task IMemoryHistory.RollbackAsync(Guid userId, IReadOnlyList<long> revisionIds, CancellationToken cancellationToken) =>
        RollbackAsync(userId, revisionIds, cancellationToken);

    Task<MemoryEvent> IMemoryEvents.RecordAsync(Guid userId, NewMemoryEvent next, CancellationToken cancellationToken) =>
        RecordAsync(userId, next, cancellationToken);

    Task<IReadOnlyList<MemoryEvent>> IMemoryEvents.EventsAsync(Guid userId, EventQuery query, CancellationToken cancellationToken) =>
        EventsAsync(userId, query, cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> IMemoryEvents.ConversationTitlesAsync(Guid userId, IEnumerable<Guid?> conversationIds, CancellationToken cancellationToken) =>
        ConversationTitlesAsync(userId, conversationIds, cancellationToken);

    Task<MemoryOutcome<IReadOnlyList<MemoryEventHit>>> IMemoryEvents.SearchEventsAsync( Guid userId, string? query, DateTimeOffset? from, DateTimeOffset? before, bool includePrivate, CancellationToken cancellationToken) =>
        SearchEventsAsync(userId, query, from, before, includePrivate, cancellationToken);

    Task<MemoryOutcome<IReadOnlyList<MemoryEvent>>> IMemoryEvents.ListEventsAsync(Guid userId, string? query, DateTimeOffset? from, DateTimeOffset? before, bool includePrivate, int max, CancellationToken cancellationToken) =>
        ListEventsAsync(userId, query, from, before, includePrivate, max, cancellationToken);

    Task<int> IMemoryEvents.MakeVectorsAsync(Guid userId, int max, CancellationToken cancellationToken) =>
        MakeVectorsAsync(userId, max, cancellationToken);

    Task<MemoryOutcome<MemoryChange>> IMemoryFacts.RecordFactAsync( Guid userId, MemoryActor actor, string? subject, string? attribute, string? value, string? validFrom, string? note, IReadOnlyList<long> sources, CancellationToken cancellationToken) =>
        RecordFactAsync(userId, actor, subject, attribute, value, validFrom, note, sources, cancellationToken);

    Task<MemoryOutcome<IReadOnlyList<Fact>>> IMemoryFacts.CurrentFactsAsync(Guid userId, string? subject, CancellationToken cancellationToken) =>
        CurrentFactsAsync(userId, subject, cancellationToken);

    Task<MemoryOutcome<MemoryChange>> IMemoryFacts.RecordFactAsync(
        Guid userId, MemoryActor actor, string? subject, string? attribute, string? value, string? validFrom, string? note, IReadOnlyList<long> sources, bool correction, CancellationToken cancellationToken) =>
        RecordFactAsync(userId, actor, subject, attribute, value, validFrom, note, sources, correction, cancellationToken);

    Task<MemoryOutcome<MemoryChange>> IMemoryFacts.EndFactAsync(Guid userId, MemoryActor actor, string? subject, string? attribute, string? on, bool retract, string? value, CancellationToken cancellationToken) =>
        EndFactAsync(userId, actor, subject, attribute, on, retract, value, cancellationToken);

    Task<MemoryOutcome<MemoryChange>> IMemoryFacts.EndFactAsync(
        Guid userId, MemoryActor actor, string? subject, string? attribute, string? on, bool retract, string? value, IReadOnlyList<long> sources, CancellationToken cancellationToken) =>
        EndFactAsync(userId, actor, subject, attribute, on, retract, value, sources, cancellationToken);

    Task<MemoryOutcome<IReadOnlyList<Fact>>> IMemoryFacts.CurrentFactsAsync(Guid userId, string? subject, string? on, CancellationToken cancellationToken) =>
        CurrentFactsAsync(userId, subject, on, cancellationToken);

    Task<MemoryOutcome<IReadOnlyList<Fact>>> IMemoryFacts.PlannedFactsAsync(Guid userId, string? subject, CancellationToken cancellationToken) =>
        PlannedFactsAsync(userId, subject, cancellationToken);

    Task<MemoryOutcome<IReadOnlyList<Fact>>> IMemoryFacts.FactHistoryAsync(Guid userId, string? subject, string? attribute, CancellationToken cancellationToken) =>
        FactHistoryAsync(userId, subject, attribute, cancellationToken);

    Task<MemoryOutcome<IReadOnlyList<Fact>>> IMemoryFacts.MatchFactsAsync(Guid userId, string? query, CancellationToken cancellationToken) =>
        MatchFactsAsync(userId, query, cancellationToken);

    Task<string> IMemoryBriefing.BriefAsync(Guid userId, int? maxChars, CancellationToken cancellationToken) =>
        BriefAsync(userId, maxChars, cancellationToken);

    Task<string> IMemoryBriefing.RecallAsync(Guid userId, string? message, int? maxChars, CancellationToken cancellationToken) =>
        RecallAsync(userId, message, maxChars, cancellationToken);

    Task<string> IMemoryBriefing.RulesAsync(Guid userId, int? maxChars, CancellationToken cancellationToken) =>
        RulesAsync(userId, maxChars, cancellationToken);

    Task IMemoryForgetting.ForgetAsync(Guid userId, IReadOnlyCollection<long> eventIds, IReadOnlyCollection<string> paths, CancellationToken cancellationToken) =>
        ForgetAsync(userId, eventIds, paths, cancellationToken);

    Task<int> IMemoryForgetting.ForgetWhatWasSaidAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken) =>
        ForgetWhatWasSaidAsync(userId, conversationId, cancellationToken);

    Task<ForgetResult> IMemoryForgetting.ForgetConversationAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken) =>
        ForgetConversationAsync(userId, conversationId, cancellationToken);

    Task<MemoryOutcome<ImportResult>> IMemoryImports.ImportAsync(Guid userId, ImportRequest request, CancellationToken cancellationToken) =>
        ImportAsync(userId, request, cancellationToken);

    Task<MemoryOutcome<RecordedItems>> IMemoryImports.RecordItemsAsync(Guid userId, Guid sourceId, string source, IReadOnlyList<ImportItem> items, string sensitivity, CancellationToken cancellationToken) =>
        RecordItemsAsync(userId, sourceId, source, items, sensitivity, cancellationToken);

    Task<IReadOnlyList<MemoryEvent>> IMemoryConsolidation.PendingAsync(Guid userId, int max, CancellationToken cancellationToken) =>
        PendingAsync(userId, max, cancellationToken);

    Task<IReadOnlyList<MemoryEvent>> IMemoryConsolidation.GateAsync(Guid userId, IReadOnlyList<MemoryEvent> pending, CancellationToken cancellationToken) =>
        GateAsync(userId, pending, cancellationToken);

    Task<MemoryEvent> IMemoryConsolidation.CloseConsolidationAsync(Guid userId, IReadOnlyList<long> read, IReadOnlyList<long> revisions, CancellationToken cancellationToken) =>
        CloseConsolidationAsync(userId, read, revisions, cancellationToken);

    Task<MemoryEvent> IMemoryConsolidation.ProposeAsync(Guid userId, string text, IReadOnlyList<long> sources, CancellationToken cancellationToken) =>
        ProposeAsync(userId, text, sources, cancellationToken);

    Task<IReadOnlyList<MemoryEvent>> IMemoryConsolidation.OpenProposalsAsync(Guid userId, CancellationToken cancellationToken) =>
        OpenProposalsAsync(userId, cancellationToken);

    Task<MemoryOutcome<MemoryEvent>> IMemoryConsolidation.AnswerProposalAsync(Guid userId, long proposalId, bool accepted, string source, CancellationToken cancellationToken) =>
        AnswerProposalAsync(userId, proposalId, accepted, source, cancellationToken);
}
