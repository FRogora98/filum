using Filum.Engine.Abstractions.Skills;

namespace Filum.Engine.Skills;

/// <summary>The skills every new memory starts with. Generic: they name no domain.</summary>
public static class StarterSkills
{
    /// <summary>Every new person starts with these, created with the core; they are ordinary skills.</summary>
    public static IReadOnlyList<Skill> All { get; } =
    [
        new("create-a-skill",
            "Turn something the person asks for again and again into a skill",
            "the person asks to create a skill, or to make something they often ask for quicker",
            true,
            """
            1. Find out what the person wants done each time: what they will give you, what to keep or change in the memory, and what to answer.
            2. Pick a short name of lowercase words joined by '-', a one-line description, and when the skill applies.
            3. Write the steps plainly, using what you can already do: add rows to a collection, update a document, compute totals and counts.
            4. Save it with skill_save, then tell the person in a few words what it does and that they can use it by typing /name or just by asking.
            """),
        new("what-you-know-about-me",
            "A tidy summary of what Filum knows about the person",
            "the person asks what you know or have learned about them",
            true,
            """
            1. Start from the core: who the person is, the rules they gave you, and how they want to be answered.
            2. Go through the list of what the memory holds and say briefly what each part is about; for a collection, how many entries it has. Read a file only when its name is not enough to describe it.
            3. Leave out sensitive and private content unless the person asks for it.
            4. Keep it short and grouped by topic, then ask whether anything is wrong or missing.
            """),
        new("summary-of-a-period",
            "What was saved or changed in the memory over a period",
            "the person asks for a summary of a week, a month or another period",
            true,
            """
            1. Settle the period: the one the person names, or the last 7 days.
            2. From the list of what the memory holds, pick what changed in that period.
            3. For a collection with a date field, count and total the entries of the period with collection_aggregate (dateColumn, from, to). For a document, read the part that changed.
            4. Answer grouped by topic, short, with the exact numbers the tools gave.
            """)
    ];
}
