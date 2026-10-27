using System.Security.Cryptography;

namespace Filum.Embeddings;

// A model's files, downloaded once: each is kept only when its hash matches, and a partial download never takes a
// file's name.
internal static class Downloads
{
    public static async Task<bool> EnsureAsync(string folder, IReadOnlyDictionary<string, (string Url, string Sha256)> files, HttpClient http, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(folder);
        foreach (var (file, (url, sha)) in files)
        {
            var path = Path.Combine(folder, file);
            if (File.Exists(path))
            {
                continue;
            }

            var partial = path + ".part";
            using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                await using var target = File.Create(partial);
                await response.Content.CopyToAsync(target, cancellationToken);
            }

            await using (var check = File.OpenRead(partial))
            {
                if (!Convert.ToHexString(await SHA256.HashDataAsync(check, cancellationToken)).Equals(sha, StringComparison.OrdinalIgnoreCase))
                {
                    check.Close();
                    File.Delete(partial);
                    return false;
                }
            }

            File.Move(partial, path, overwrite: true);
        }

        return files.Keys.All(f => File.Exists(Path.Combine(folder, f)));
    }
}
