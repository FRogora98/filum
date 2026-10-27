using System.Text.RegularExpressions;

namespace Filum.Engine.Memory;

// A key, a token or a password a person pastes never reaches the memory (spec 048): every event and every file is kept
// with it replaced. The patterns are the providers' own formats and labelled assignments, so ordinary text is left alone.
internal static partial class SecretFilter
{
    public const string Removed = "[secret removed by Filum]";

    public static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var redacted = PrivateKey().Replace(text, Removed);
        redacted = Token().Replace(redacted, Removed);
        return Labelled().Replace(redacted, m => $"{m.Groups["label"].Value}{m.Groups["between"].Value}{Removed}");
    }

    [GeneratedRegex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z ]*PRIVATE KEY-----")]
    private static partial Regex PrivateKey();

    // API keys and tokens by their prefixes, and JSON web tokens.
    [GeneratedRegex(@"\b(?:sk-(?:ant-|proj-)?[A-Za-z0-9_\-]{20,}|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|AKIA[0-9A-Z]{16}|xox[abprs]-[A-Za-z0-9\-]{10,}|AIza[0-9A-Za-z_\-]{35}|eyJ[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,})")]
    private static partial Regex Token();

    // "password: …", "api_key=…", "token = …": the label stays, the value goes.
    [GeneratedRegex(@"(?i)(?<label>\b(?:password|passwd|pwd|passphrase|secret|api[_\- ]?key|access[_\- ]?token|auth[_\- ]?token|client[_\- ]?secret)\b)(?<between>\s*[:=]\s*)(?<value>[""']?[^\s""',;]{6,}[""']?)")]
    private static partial Regex Labelled();
}
