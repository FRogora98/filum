using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Events;

namespace Filum.Engine.Storage.LocalFolder.UnitTests;

public sealed class EventLineTests
{
    private static readonly MemoryEvent Stored = new(
        12, new DateTimeOffset(2024, 3, 13, 9, 16, 0, TimeSpan.FromHours(1)), new DateTimeOffset(2026, 10, 8, 9, 0, 0, TimeSpan.Zero),
        MemoryEventKind.Derived, MemoryEventSource.Agent, Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"), null,
        "Wrote \"1\" change\nto /notes/a.md", MemorySensitivity.Sensitive, [3, 4], [7]);

    [Fact]
    public void Serialize_WhenWritingAnEvent_ShouldGiveTheSameLineAsThePublicModel()
    {
        // The line on disk is the format of every version since 0.7: the disk record writes what the model used to.
        Assert.Equal(JsonLines.Serialize(Stored), JsonLines.Serialize(EventLine.From(Stored)));
    }

    [Fact]
    public void ToEvent_WhenALineIsReadBack_ShouldGiveTheSameEvent()
    {
        var line = JsonLines.Parse<EventLine>([JsonLines.Serialize(EventLine.From(Stored))]).Single();

        var read = line.ToEvent();

        Assert.Equal(JsonLines.Serialize(Stored), JsonLines.Serialize(read));
        Assert.Equal([3L, 4L], read.Sources);
    }
}
