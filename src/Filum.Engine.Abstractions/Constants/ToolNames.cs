namespace Filum.Engine.Abstractions.Constants;

/// <summary>The names of the engine's tools, as models and hosts see them.</summary>
public static class ToolNames
{
    /// <summary>The person's core, the map of every file and their enabled skills, in one call.</summary>
    public const string MemoryOverview = "memory_overview";

    /// <summary>Reads a file with line numbers, a page at a time.</summary>
    public const string MemoryRead = "memory_read";

    /// <summary>Finds the lines that contain a text across the files.</summary>
    public const string MemorySearch = "memory_search";

    /// <summary>Creates a document or replaces its whole content.</summary>
    public const string MemoryWrite = "memory_write";

    /// <summary>Replaces one exact occurrence of a text in a document.</summary>
    public const string MemoryEdit = "memory_edit";

    /// <summary>Adds text at the end of a document.</summary>
    public const string MemoryAppend = "memory_append";

    /// <summary>Deletes a file; it stays in the history.</summary>
    public const string MemoryDelete = "memory_delete";

    /// <summary>Renames or moves a file, keeping its history.</summary>
    public const string MemoryMove = "memory_move";

    /// <summary>Sets a file's sensitivity.</summary>
    public const string MemorySetSensitivity = "memory_set_sensitivity";

    /// <summary>A file's changes, newest first.</summary>
    public const string MemoryHistory = "memory_history";

    /// <summary>Takes back one change by its id.</summary>
    public const string MemoryUndo = "memory_undo";

    /// <summary>Proposes a structural change during a consolidation pass.</summary>
    public const string MemoryPropose = "memory_propose";

    /// <summary>Searches what was said, answered or imported in every conversation.</summary>
    public const string EventsSearch = "events_search";

    /// <summary>Reads in order every message of the conversation an event belongs to.</summary>
    public const string EventsRead = "events_read";

    /// <summary>Records a value that holds for a time.</summary>
    public const string FactRecord = "fact_record";

    /// <summary>The facts that hold now.</summary>
    public const string FactsCurrent = "facts_current";

    /// <summary>Every value the facts of a subject had.</summary>
    public const string FactsHistory = "facts_history";

    /// <summary>Ends the open value of a fact with no new value (spec 041).</summary>
    public const string FactEnd = "fact_end";

    /// <summary>Records the person's answer to a waiting proposal.</summary>
    public const string ProposalAnswer = "proposal_answer";

    /// <summary>Adds rows to a collection.</summary>
    public const string CollectionAddRows = "collection_add_rows";

    /// <summary>Changes fields of the rows that match.</summary>
    public const string CollectionUpdateRows = "collection_update_rows";

    /// <summary>Removes the rows that match.</summary>
    public const string CollectionRemoveRows = "collection_remove_rows";

    /// <summary>Adds a field to a collection.</summary>
    public const string CollectionAddField = "collection_add_field";

    /// <summary>Counts, sums, averages, minimums and maximums of a column.</summary>
    public const string CollectionAggregate = "collection_aggregate";

    /// <summary>A column over time: a forecast, its error and what stands out.</summary>
    public const string CollectionTrend = "collection_trend";

    /// <summary>Runs the agent's own read-only program on collections.</summary>
    public const string ComputeRun = "compute_run";

    /// <summary>The person's skills.</summary>
    public const string SkillList = "skill_list";

    /// <summary>The steps of one enabled skill.</summary>
    public const string SkillUse = "skill_use";

    /// <summary>Saves a skill.</summary>
    public const string SkillSave = "skill_save";

    /// <summary>Proposes a skill without saving it.</summary>
    public const string SkillPropose = "skill_propose";

    /// <summary>Turns a skill off or back on.</summary>
    public const string SkillSetEnabled = "skill_set_enabled";
}
