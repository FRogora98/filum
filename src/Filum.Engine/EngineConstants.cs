namespace Filum.Engine;

// What several areas of the engine must write and read alike, named once.
internal static class EngineConstants
{
    internal static class Dates
    {
        // A day as facts, collections, calendar items and event searches write it.
        public const string Day = "yyyy-MM-dd";
        public const string DayAndMinute = "yyyy-MM-dd HH:mm";
    }

    internal static class FileExtensions
    {
        public const string Document = ".md";
        public const string Collection = ".csv";
    }
}
