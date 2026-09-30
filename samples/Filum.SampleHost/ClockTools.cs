using Microsoft.Extensions.AI;

namespace Filum.SampleHost;

/// <summary>
/// The sample host's own tool: the current date and time in the person's time zone. Its result is surfaced, so the
/// app gets it as data on the turn's step (a card, for example) while the model reads a sentence.
/// </summary>
public sealed class ClockTools : ITurnToolSource
{
    public IReadOnlyList<AIFunction> Tools(TurnContext turn) =>
    [
        HostTools.Create(() =>
        {
            var local = TimeZoneInfo.ConvertTime(turn.Now, turn.TimeZone);
            return new SurfacedResult(
                $"It is {local:dddd d MMMM yyyy, HH:mm} ({turn.TimeZone.Id}).",
                new { now = local.ToString("O"), timeZone = turn.TimeZone.Id });
        }, "sample_now", "Tell the current date and time in the person's time zone. Use it when the person asks what day or time it is.")
    ];
}
