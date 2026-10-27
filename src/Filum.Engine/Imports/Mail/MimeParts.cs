using System.Net;
using System.Text.RegularExpressions;

namespace Filum.Engine.Imports.Mail;

internal static class MimeParts
{
    private const string PlainText = "text/plain";
    private const string Html = "text/html";
    private const string Multipart = "multipart/";
    private const string BoundaryDelimiter = "--";

    // Headers unfolded, names lower case, the first occurrence kept.
    public static (Dictionary<string, string> Headers, string Body) Split(string raw)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = raw.Split('\n');
        var i = 0;
        string? last = null;
        for (; i < lines.Length && lines[i].Length > 0; i++)
        {
            if ((lines[i].StartsWith(' ') || lines[i].StartsWith('\t')) && last is not null)
            {
                headers[last] += " " + lines[i].Trim();
                continue;
            }

            var colon = lines[i].IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            last = lines[i][..colon].Trim().ToLowerInvariant();
            if (!headers.ContainsKey(last))
            {
                headers[last] = lines[i][(colon + 1)..].Trim();
            }
            else
            {
                last = null;
            }
        }

        return (headers, string.Join('\n', lines.Skip(i + 1)));
    }

    // The plain part, else the HTML part without tags.
    public static string ReadableText(Dictionary<string, string> headers, string body)
    {
        var type = headers.GetValueOrDefault(MailHeaders.ContentType) ?? PlainText;
        if (type.StartsWith(Multipart, StringComparison.OrdinalIgnoreCase) && MimeDecoder.Parameter(type, MailHeaders.Boundary) is { } boundary)
        {
            return MultipartText(body, boundary);
        }

        var decoded = MimeDecoder.Decode(body, headers.GetValueOrDefault(MailHeaders.ContentTransferEncoding), MimeDecoder.Parameter(type, MailHeaders.Charset));
        return type.StartsWith(Html, StringComparison.OrdinalIgnoreCase) ? Unhtml(decoded) : decoded;
    }

    // The first plain part, else the first nested multipart, else the first HTML part.
    private static string MultipartText(string body, string boundary)
    {
        var parts = body.Split(BoundaryDelimiter + boundary)
            .Skip(1)
            .Where(p => !p.StartsWith(BoundaryDelimiter, StringComparison.Ordinal))
            .Select(p => Split(p.TrimStart('\n')))
            .ToList();
        var plain = parts.FirstOrDefault(p => (p.Headers.GetValueOrDefault(MailHeaders.ContentType) ?? PlainText).StartsWith(PlainText, StringComparison.OrdinalIgnoreCase));
        if (plain.Headers is not null)
        {
            return ReadableText(plain.Headers, plain.Body);
        }

        var nested = parts.FirstOrDefault(p => (p.Headers.GetValueOrDefault(MailHeaders.ContentType) ?? string.Empty).StartsWith(Multipart, StringComparison.OrdinalIgnoreCase));
        if (nested.Headers is not null)
        {
            return ReadableText(nested.Headers, nested.Body);
        }

        var html = parts.FirstOrDefault(p => (p.Headers.GetValueOrDefault(MailHeaders.ContentType) ?? string.Empty).StartsWith(Html, StringComparison.OrdinalIgnoreCase));
        return html.Headers is null ? string.Empty : ReadableText(html.Headers, html.Body);
    }

    private static string Unhtml(string html)
    {
        var text = Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<(br|/p|/div|/li|/h\d)[^>]*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", string.Empty);
        text = WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\n\s*\n+", "\n\n").Trim();
    }
}
