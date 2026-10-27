using Filum.Engine.Abstractions.Imports;

namespace Filum.Engine.Imports.Calendar;

// Components nested in an event (an alarm) are skipped whole, so their properties never become the event's. Of a
// property set twice, the first counts.
internal sealed class IcsEventScan
{
    private const string BeginEvent = "BEGIN:VEVENT";
    private const string EndEvent = "END:VEVENT";
    private const string Begin = "BEGIN:";
    private const string End = "END:";
    private const string Summary = "SUMMARY";
    private const string Start = "DTSTART";
    private const string Location = "LOCATION";
    private const string Description = "DESCRIPTION";
    private const string Uid = "UID";
    private const string IdentityPrefix = "ics|";

    private Dictionary<string, (string Params, string Value)>? _event;
    private int _nested;

    public List<ImportItem> Items { get; } = [];

    public int Skipped { get; private set; }

    public void Read(string line)
    {
        if (SkipsNested(line))
        {
            return;
        }

        if (line.Equals(BeginEvent, StringComparison.OrdinalIgnoreCase))
        {
            _event = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        if (line.Equals(EndEvent, StringComparison.OrdinalIgnoreCase) && _event is not null)
        {
            Close(_event);
            _event = null;
            return;
        }

        if (_event is not null)
        {
            AddProperty(_event, line);
        }
    }

    private bool SkipsNested(string line)
    {
        if (_event is not null && line.StartsWith(Begin, StringComparison.OrdinalIgnoreCase) && !line.Equals(BeginEvent, StringComparison.OrdinalIgnoreCase))
        {
            _nested++;
            return true;
        }

        if (_nested > 0)
        {
            if (line.StartsWith(End, StringComparison.OrdinalIgnoreCase))
            {
                _nested--;
            }

            return true;
        }

        return false;
    }

    private void Close(Dictionary<string, (string Params, string Value)> ev)
    {
        var summary = IcsValues.Unescape(ev.GetValueOrDefault(Summary).Value ?? string.Empty);
        var start = ev.TryGetValue(Start, out var dt) ? IcsValues.Date(dt.Value, dt.Params) : null;
        if (summary.Length == 0 && start is null)
        {
            Skipped++;
            return;
        }

        var location = IcsValues.Unescape(ev.GetValueOrDefault(Location).Value ?? string.Empty);
        var description = IcsValues.Unescape(ev.GetValueOrDefault(Description).Value ?? string.Empty);
        var allDay = dt.Value is { Length: IcsValues.DateOnlyLength };
        var uid = ev.GetValueOrDefault(Uid).Value;
        Items.Add(new ImportItem(start, null, CalendarItemText.Format(summary, start, allDay, location, description),
            $"{IdentityPrefix}{(uid is { Length: > 0 } ? uid : summary)}|{dt.Value}"));
    }

    private static void AddProperty(Dictionary<string, (string Params, string Value)> ev, string line)
    {
        var colon = line.IndexOf(':');
        if (colon <= 0)
        {
            return;
        }

        var key = line[..colon];
        var semicolon = key.IndexOf(';');
        var name = semicolon < 0 ? key : key[..semicolon];
        ev.TryAdd(name, (semicolon < 0 ? string.Empty : key[(semicolon + 1)..], line[(colon + 1)..]));
    }
}
