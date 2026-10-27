using System.Security.Cryptography;
using System.Text;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Collections;
using Filum.Engine.Memory;

namespace Filum.Engine.Storage.LocalFolder;

internal static class FileMeasures
{
    public static string Hash(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    public static (string? Header, int? Rows) CollectionShape(string path, string content)
    {
        if (MemoryPaths.KindOf(path) != MemoryFileKind.Collection)
        {
            return (null, null);
        }

        var table = Csv.Check(content);
        return table.IsValid ? (string.Join(",", table.Header), table.Rows.Count) : (null, null);
    }
}
