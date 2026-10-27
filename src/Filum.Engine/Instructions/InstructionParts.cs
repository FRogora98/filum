using Filum.Engine.Abstractions.Constants;

namespace Filum.Engine.Instructions;

/// <summary>
/// The parts of the agent's instructions every host says the same way (spec 040): written once here, and composed into
/// <see cref="PlatformInstructions.Text"/> and into filum-mcp's instructions, so the policies cannot drift apart.
/// </summary>
public static class InstructionParts
{
    /// <summary>What goes in the facts and what in the core and documents.</summary>
    public const string FactPolicy = $"""A point fact that can change, about a person, a place or a thing (where someone lives, a job, how many of something there are, a status, a preference), is recorded with {ToolNames.FactRecord}, one per subject and attribute, every time it is told or changes: the value before is kept with the day it ended, as a revision of the old one. When the person says it changed by an amount ("one more", "two fewer"), work out the new value from the last one and record that. When the person says a value was wrong, record the right one with correction set; when something no longer holds and nothing takes its place, end it with {ToolNames.FactEnd}. Who the person is, and longer texts such as notes and plans, go in the core and in documents; never renumber facts or copy them into a file.""";

    /// <summary>
    /// Lasting rules and open items as facts (spec 046), so every brief shows them and consolidation can catch them.
    /// </summary>
    public const string RulesAndOpenItems = $"A lasting instruction (\"from now on…\", \"always…\") is a fact of the person with attribute rule, the instruction as the value and its reason as the note; something promised or still to do is a fact with attribute open, its due day as the note, ended with {ToolNames.FactEnd} and its value once done or dropped.";

    /// <summary>What is worth keeping (spec 049): what will still matter in a later session.</summary>
    public const string Durable = "Keep only what will still matter in a later session: not small talk, not the passing details of the task at hand, never a password or a key.";

    /// <summary>How far a rule of the person's reaches (spec 055): style and format, never safety or the host's rules.</summary>
    public const string RuleReach = "A rule of the person's outranks the host's defaults of style and format (length, layout, units, language) in every answer; never safety or the host's own rules.";

    /// <summary>What the memory does not hold is unknown (spec 055).</summary>
    public const string Unknown = "What the memory does not hold is unknown: never fill a person's relation, name or value in from something else; say it was never said.";

    /// <summary>What is saved without being asked (spec 055).</summary>
    public const string WithoutAsking = $"Save without being asked, with the tools that keep it: a promise or a plan with a day ({ToolNames.FactRecord}); something done that closes an open item ({ToolNames.FactEnd}, that event as source); a list the person asked you to keep (a collection or a document). memory_log keeps only their words.";

    /// <summary>A search shows excerpts; a conversation can be read whole.</summary>
    public const string ReadWhole = $"A match of {ToolNames.EventsSearch} shows only part of its message: when the answer may lie elsewhere in that conversation, read it whole with {ToolNames.EventsRead} and the match's number.";
}
