using Filum.Engine.Abstractions.Collections;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Facts;
using Filum.Engine.Abstractions.Imports;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Skills;
using Filum.Engine.Memory.Contracts;
using Filum.Engine.Packs;

namespace Filum.Engine.Memory;

/// <summary>
/// A person's memory: text files with their full history, and the log of events. Every call names the user; another
/// person's file or revision does not exist. Every change adds a revision and bumps the file's version atomically: two
/// changes from the same version cannot both win. A caller that needs only part of it depends on one of its roles
/// (<see cref="IMemoryReader"/>, <see cref="IMemoryWriter"/>, <see cref="IMemoryEvents"/>…), the same instance.
/// </summary>
public partial interface IMemoryService :
    IMemoryCore,
    IMemoryReader,
    IMemoryWriter,
    IMemorySkills,
    IMemoryHistory,
    IMemoryEvents,
    IMemoryFacts,
    IMemoryForgetting,
    IMemoryImports,
    IMemoryConsolidation,
    IMemoryBriefing
{
    /// <inheritdoc cref="IMemoryCore.Pack"/>
    new Pack? Pack { get; }

    /// <inheritdoc cref="IMemoryBriefing.BriefAsync"/>
    new Task<string> BriefAsync(Guid userId, int? maxChars, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryBriefing.RecallAsync"/>
    new Task<string> RecallAsync(Guid userId, string? message, int? maxChars, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryBriefing.RulesAsync"/>
    new Task<string> RulesAsync(Guid userId, int? maxChars, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryCore.EnsureCoreAsync"/>
    new Task<string> EnsureCoreAsync(Guid userId, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryCore.EnsureLearnedAsync"/>
    new Task<string> EnsureLearnedAsync(Guid userId, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryReader.ListAsync"/>
    new Task<MemoryOutcome<IReadOnlyList<MemoryFileInfo>>> ListAsync(Guid userId, string? prefix, bool includePrivate, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryReader.ReadAsync"/>
    new Task<MemoryOutcome<MemoryReadResult>> ReadAsync(Guid userId, string? path, int? fromLine, int? toLine, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryReader.BuildIndexAsync"/>
    new Task<string> BuildIndexAsync(Guid userId, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryReader.GetFileAsync"/>
    new Task<MemoryOutcome<MemoryFileContent>> GetFileAsync(Guid userId, string? path, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryReader.ReadCollectionAsync"/>
    new Task<MemoryOutcome<CsvTable>> ReadCollectionAsync(Guid userId, string? path, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryReader.SearchAsync"/>
    new Task<MemoryOutcome<MemorySearchResult>> SearchAsync(Guid userId, string? query, string? prefix, bool includePrivate, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryWriter.WriteAsync"/>
    new Task<MemoryOutcome<MemoryChange>> WriteAsync(Guid userId, MemoryActor actor, string? path, string? content, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryWriter.EditAsync"/>
    new Task<MemoryOutcome<MemoryChange>> EditAsync(Guid userId, MemoryActor actor, string? path, string? oldText, string? newText, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryWriter.AppendAsync"/>
    new Task<MemoryOutcome<MemoryChange>> AppendAsync(Guid userId, MemoryActor actor, string? path, string? text, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryWriter.AddRowsAsync"/>
    new Task<MemoryOutcome<MemoryChange>> AddRowsAsync(Guid userId, MemoryActor actor, string? path, IReadOnlyList<IReadOnlyDictionary<string, string>> rows, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryWriter.UpdateRowsAsync"/>
    new Task<MemoryOutcome<MemoryChange>> UpdateRowsAsync(Guid userId, MemoryActor actor, string? path, IReadOnlyDictionary<string, string> where, IReadOnlyDictionary<string, string> set, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryWriter.RemoveRowsAsync"/>
    new Task<MemoryOutcome<MemoryChange>> RemoveRowsAsync(Guid userId, MemoryActor actor, string? path, IReadOnlyDictionary<string, string> where, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryWriter.AddFieldAsync"/>
    new Task<MemoryOutcome<MemoryChange>> AddFieldAsync(Guid userId, MemoryActor actor, string? path, string? field, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryWriter.DeleteAsync"/>
    new Task<MemoryOutcome<MemoryChange>> DeleteAsync(Guid userId, MemoryActor actor, string? path, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryWriter.MoveAsync"/>
    new Task<MemoryOutcome<MemoryChange>> MoveAsync(Guid userId, MemoryActor actor, string? from, string? to, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryWriter.SetSensitivityAsync"/>
    new Task<MemoryOutcome<MemoryChange>> SetSensitivityAsync(Guid userId, MemoryActor actor, string? path, string? level, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemorySkills.ListSkillsAsync"/>
    new Task<IReadOnlyList<SkillFile>> ListSkillsAsync(Guid userId, bool includePrivate, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemorySkills.GetSkillAsync"/>
    new Task<MemoryOutcome<SkillFile>> GetSkillAsync(Guid userId, string? name, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemorySkills.SaveSkillAsync"/>
    new Task<MemoryOutcome<MemoryChange>> SaveSkillAsync(Guid userId, MemoryActor actor, Skill skill, bool replace, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemorySkills.SetSkillEnabledAsync"/>
    new Task<MemoryOutcome<MemoryChange>> SetSkillEnabledAsync(Guid userId, MemoryActor actor, string? name, bool enabled, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryHistory.LineChangesAsync"/>
    new Task<IReadOnlyDictionary<string, LineChange>> LineChangesAsync(Guid userId, string path, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryHistory.GetHistoryAsync"/>
    new Task<MemoryOutcome<IReadOnlyList<MemoryRevisionInfo>>> GetHistoryAsync(Guid userId, string? path, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryHistory.GetRevisionAsync"/>
    new Task<MemoryOutcome<MemoryVersion>> GetRevisionAsync(Guid userId, long revisionId, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryHistory.GetOriginAsync"/>
    new Task<MemoryOutcome<MemoryOrigin>> GetOriginAsync(Guid userId, string? path, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryHistory.UndoneAmongAsync"/>
    new Task<IReadOnlySet<long>> UndoneAmongAsync(Guid userId, IReadOnlyCollection<long> revisionIds, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryHistory.LastAuthorAsync"/>
    new Task<string?> LastAuthorAsync(Guid userId, string path, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryHistory.RestoreAsync"/>
    new Task<MemoryOutcome<MemoryChange>> RestoreAsync(Guid userId, MemoryActor actor, long revisionId, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryHistory.UndoAsync"/>
    new Task<MemoryOutcome<MemoryChange>> UndoAsync(Guid userId, MemoryActor actor, long revisionId, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryHistory.RollbackAsync"/>
    new Task RollbackAsync(Guid userId, IReadOnlyList<long> revisionIds, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryEvents.RecordAsync"/>
    new Task<MemoryEvent> RecordAsync(Guid userId, NewMemoryEvent next, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryEvents.EventsAsync"/>
    new Task<IReadOnlyList<MemoryEvent>> EventsAsync(Guid userId, EventQuery query, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryEvents.ConversationTitlesAsync"/>
    new Task<IReadOnlyDictionary<Guid, string>> ConversationTitlesAsync(Guid userId, IEnumerable<Guid?> conversationIds, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryEvents.SearchEventsAsync"/>
    new Task<MemoryOutcome<IReadOnlyList<MemoryEventHit>>> SearchEventsAsync(
        Guid userId, string? query, DateTimeOffset? from, DateTimeOffset? before, bool includePrivate, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryEvents.MakeVectorsAsync"/>
    new Task<int> MakeVectorsAsync(Guid userId, int max, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryFacts.RecordFactAsync(Guid, MemoryActor, string?, string?, string?, string?, string?, IReadOnlyList{long}, CancellationToken)"/>
    new Task<MemoryOutcome<MemoryChange>> RecordFactAsync(
        Guid userId, MemoryActor actor, string? subject, string? attribute, string? value, string? validFrom, string? note, IReadOnlyList<long> sources, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryFacts.CurrentFactsAsync(Guid, string?, CancellationToken)"/>
    new Task<MemoryOutcome<IReadOnlyList<Fact>>> CurrentFactsAsync(Guid userId, string? subject, CancellationToken cancellationToken);

    /// <summary>
    /// As the other <c>RecordFactAsync</c>; with <paramref name="correction"/>, the open value was wrong (spec 041).
    /// </summary>
    new Task<MemoryOutcome<MemoryChange>> RecordFactAsync(
        Guid userId, MemoryActor actor, string? subject, string? attribute, string? value, string? validFrom, string? note, IReadOnlyList<long> sources, bool correction, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryFacts.EndFactAsync(Guid, MemoryActor, string?, string?, string?, bool, string?, CancellationToken)"/>
    new Task<MemoryOutcome<MemoryChange>> EndFactAsync(Guid userId, MemoryActor actor, string? subject, string? attribute, string? on, bool retract, string? value, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryFacts.EndFactAsync(Guid, MemoryActor, string?, string?, string?, bool, string?, IReadOnlyList{long}, CancellationToken)"/>
    new Task<MemoryOutcome<MemoryChange>> EndFactAsync(
        Guid userId, MemoryActor actor, string? subject, string? attribute, string? on, bool retract, string? value, IReadOnlyList<long> sources, CancellationToken cancellationToken);

    /// <summary>The facts that held on <paramref name="on"/> (<c>yyyy-MM-dd</c>; now when null), of one subject or of all.</summary>
    new Task<MemoryOutcome<IReadOnlyList<Fact>>> CurrentFactsAsync(Guid userId, string? subject, string? on, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryFacts.PlannedFactsAsync"/>
    new Task<MemoryOutcome<IReadOnlyList<Fact>>> PlannedFactsAsync(Guid userId, string? subject, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryFacts.FactHistoryAsync"/>
    new Task<MemoryOutcome<IReadOnlyList<Fact>>> FactHistoryAsync(Guid userId, string? subject, string? attribute, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryFacts.MatchFactsAsync"/>
    new Task<MemoryOutcome<IReadOnlyList<Fact>>> MatchFactsAsync(Guid userId, string? query, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryForgetting.ForgetAsync"/>
    new Task ForgetAsync(Guid userId, IReadOnlyCollection<long> eventIds, IReadOnlyCollection<string> paths, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryForgetting.ForgetWhatWasSaidAsync"/>
    new Task<int> ForgetWhatWasSaidAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryForgetting.ForgetConversationAsync"/>
    new Task<ForgetResult> ForgetConversationAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryImports.ImportAsync"/>
    new Task<MemoryOutcome<ImportResult>> ImportAsync(Guid userId, ImportRequest request, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryImports.RecordItemsAsync"/>
    new Task<MemoryOutcome<RecordedItems>> RecordItemsAsync(Guid userId, Guid sourceId, string source, IReadOnlyList<ImportItem> items, string sensitivity, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryConsolidation.PendingAsync"/>
    new Task<IReadOnlyList<MemoryEvent>> PendingAsync(Guid userId, int max, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryConsolidation.GateAsync"/>
    new Task<IReadOnlyList<MemoryEvent>> GateAsync(Guid userId, IReadOnlyList<MemoryEvent> pending, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryConsolidation.CloseConsolidationAsync"/>
    new Task<MemoryEvent> CloseConsolidationAsync(Guid userId, IReadOnlyList<long> read, IReadOnlyList<long> revisions, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryConsolidation.ProposeAsync"/>
    new Task<MemoryEvent> ProposeAsync(Guid userId, string text, IReadOnlyList<long> sources, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryConsolidation.OpenProposalsAsync"/>
    new Task<IReadOnlyList<MemoryEvent>> OpenProposalsAsync(Guid userId, CancellationToken cancellationToken);

    /// <inheritdoc cref="IMemoryConsolidation.AnswerProposalAsync"/>
    new Task<MemoryOutcome<MemoryEvent>> AnswerProposalAsync(Guid userId, long proposalId, bool accepted, string source, CancellationToken cancellationToken);
}
