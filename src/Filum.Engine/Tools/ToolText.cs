using System.Globalization;
using Filum.Engine.Abstractions.Events;
namespace Filum.Engine.Tools;

internal static class ToolText
{
    public static string Plural(int count, string one, string? many = null) =>
        count == 1 ? $"1 {one}" : $"{count.ToString(CultureInfo.InvariantCulture)} {many ?? one + "s"}";

    public static string Number(double value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);

    // 'you' is the agent's own answer.
    public static string Who(MemoryEvent e) => e.Kind switch
    {
        MemoryEventKind.Answered => "you",
        MemoryEventKind.Imported => "imported",
        _ => "the person"
    };
}
