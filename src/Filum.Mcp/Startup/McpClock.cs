using System.Globalization;
using Filum.Engine.Storage.LocalFolder;
using Microsoft.Extensions.DependencyInjection;

namespace Filum.Mcp.Startup;

// The engine's clock (spec 055): the system's, or with FILUM_TODAY=yyyy-MM-dd that day at the system's time of day, for a
// replay such as a benchmark. Today is never inferred from the memory. An invalid value is ignored, with a line on
// stderr: a hook must never stop its host.
internal static class McpClock
{
    private const string DayFormat = "yyyy-MM-dd";

    public static TimeProvider Time { get; } = Create(Environment.GetEnvironmentVariable(McpConstants.Variables.Today), Console.Error);

    public static TimeProvider Create(string? today, TextWriter errors)
    {
        if (string.IsNullOrWhiteSpace(today))
        {
            return TimeProvider.System;
        }

        if (DateOnly.TryParseExact(today.Trim(), DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return new OnDay(day);
        }

        errors.WriteLine($"{McpConstants.ProgramName}: {McpConstants.Variables.Today} must be a day, {DayFormat}; the system's clock is used.");
        return TimeProvider.System;
    }

    /// <summary>A local folder store and an engine on this clock: what the hooks open.</summary>
    public static (LocalFolderStore Store, FilumEngine Engine) Open(string home)
    {
        var store = new LocalFolderStore(home, null, Time);
        return (store, FilumEngine.Create(store, configure: engine => engine.Services.AddSingleton(Time)));
    }

    private sealed class OnDay(DateOnly day) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(day.ToDateTime(TimeOnly.FromTimeSpan(System.GetUtcNow().TimeOfDay)), TimeSpan.Zero);
    }
}
