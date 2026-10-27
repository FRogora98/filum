using Filum.Engine.Abstractions.Events;

namespace Filum.Engine.Storage.LocalFolder;

// Appended in order; rewritten only to forget events.
internal sealed class EventLog(FolderLayout layout)
{
    private List<MemoryEvent>? _events;

    // Read once per call.
    public List<MemoryEvent> All() => _events ??= JsonLines.Read<EventLine>(layout.EventsFile).Select(l => l.ToEvent()).ToList();

    public long LastId() => All().Select(e => e.Id).DefaultIfEmpty(0).Max();

    public void Append(MemoryEvent stored)
    {
        JsonLines.Append(layout.EventsFile, Line(stored));
        All().Add(stored);
    }

    public void Remove(IReadOnlyCollection<long> eventIds)
    {
        All().RemoveAll(e => eventIds.Contains(e.Id));
        JsonLines.Rewrite<EventLine>(layout, layout.EventsFile, All().Select(Line));
    }

    private static string Line(MemoryEvent stored) => JsonLines.Serialize(EventLine.From(stored));
}
