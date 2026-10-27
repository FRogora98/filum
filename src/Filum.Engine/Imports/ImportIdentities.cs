using System.Security.Cryptography;
using System.Text;
using Filum.Engine.Abstractions.Imports;

namespace Filum.Engine.Imports;

// Identities are the dedupe key of every imported event: a change of one byte makes the next import of the same file
// record everything again.
internal static class ImportIdentities
{
    private const string IdSalt = "filum-import:";
    private const int GuidBytes = 16;

    // Two identical items of one file are two items: the n-th repeat is told apart by its rank, which stays the same
    // when the file is exported again.
    public static ImportParse RankRepeats(ImportParse parsed)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        return parsed with
        {
            Items = parsed.Items.Select(i =>
            {
                var n = seen[i.Identity] = seen.GetValueOrDefault(i.Identity) + 1;
                return n == 1 ? i : i with { Identity = $"{i.Identity}|#{n}" };
            }).ToList()
        };
    }

    public static Guid IdOf(string identity)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(IdSalt + identity));
        return new Guid(hash.AsSpan(0, GuidBytes));
    }
}
