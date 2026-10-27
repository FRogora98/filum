using System.Text;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Collections;
using Filum.Engine.Skills;
using Microsoft.Extensions.Options;

namespace Filum.Engine.Memory.Files;

internal sealed class MemoryContentValidator(IOptions<MemoryOptions> options)
{
    private const int BytesPerKilobyte = 1024;
    private const string HeaderSeparator = ",";

    private MemoryOptions Limits => options.Value;

    public ContentCheck Check(string path, string content)
    {
        var size = Encoding.UTF8.GetByteCount(content);
        var lines = MemoryText.LinesOf(content).Count;
        var collection = MemoryPaths.KindOf(path) == MemoryFileKind.Collection;
        var limit = collection ? Limits.MaxCollectionBytes : Limits.MaxFileBytes;
        if (size > limit)
        {
            return new ContentCheck(TooLarge(path, size, limit, collection), size, lines, null, null);
        }

        if (MemoryPaths.IsCore(path) && content.Length > Limits.MaxCoreChars)
        {
            return new ContentCheck(
                $"The core would be {content.Length} characters, over the limit of {Limits.MaxCoreChars}; condense it, keeping the essentials and moving details into other files.",
                size, lines, null, null);
        }

        if (SkillFormat.IsSkillPath(path) && SkillFormat.CheckFile(path, content, Limits.MaxSkillChars) is { } skillError)
        {
            return new ContentCheck(skillError, size, lines, null, null);
        }

        if (MemoryPaths.KindOf(path) == MemoryFileKind.Collection)
        {
            var table = Csv.Check(content);
            return table.IsValid
                ? new ContentCheck(null, size, lines, string.Join(HeaderSeparator, table.Header), table.Rows.Count)
                : new ContentCheck($"{path} is not a valid collection: {table.Error}", size, lines, null, null);
        }

        return new ContentCheck(null, size, lines, null, null);
    }

    // Said so the agent stops: a model told only "too large" kept trying, and every try failed the same way.
    private static string TooLarge(string path, int size, int limit, bool collection) =>
        $"{path} would be {size / BytesPerKilobyte} KB, over the limit of {limit / BytesPerKilobyte} KB: nothing was saved, and every later change that makes it " +
        $"larger will be refused the same way, so stop adding to it and tell the person. " +
        (collection ? "Older rows can be moved into another collection." : "Split it into smaller files.");
}
