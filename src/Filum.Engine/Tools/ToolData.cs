using System.Text.Json;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Facts;

namespace Filum.Engine.Tools;

// The structured form of a result, beside its text, for a host that parses it (MCP structuredContent, spec 040). The
// shapes are the output schemas filum-mcp declares.
internal static class ToolData
{
    public static JsonElement Facts(IEnumerable<Fact> facts) =>
        JsonSerializer.SerializeToElement(new { facts = facts.Select(f => new { subject = f.Subject, attribute = f.Attribute, value = f.Value, from = f.ValidFrom, to = f.ValidTo, ended = f.Ended, recordedAt = f.RecordedAt, author = f.Author, quote = f.Quote }) });

    public static JsonElement Matches(IEnumerable<MemoryEventHit> hits) =>
        JsonSerializer.SerializeToElement(new
        {
            matches = hits.Select(h => new { id = h.Event.Id, date = h.Event.OccurredAt.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), who = ToolText.Who(h.Event), text = h.Event.Text }),
        });
}
