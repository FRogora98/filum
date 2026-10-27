using System.Text;
using System.Text.RegularExpressions;

namespace Filum.Engine.Imports.Mail;

internal static partial class MimeDecoder
{
    private const string QuotedPrintable = "quoted-printable";
    private const string Base64 = "base64";
    private const string Base64Word = "B";
    private const string ValueGroup = "v";
    private const int HexDigits = 2;
    private const int HexBase = 16;

    public static string Decode(string body, string? transfer, string? charset)
    {
        var encoding = EncodingOf(charset);
        switch (transfer?.Trim().ToLowerInvariant())
        {
            case Base64:
                try
                {
                    return encoding.GetString(Convert.FromBase64String(string.Concat(body.Where(c => !char.IsWhiteSpace(c)))));
                }
                catch (FormatException)
                {
                    return body;
                }

            case QuotedPrintable:
                return encoding.GetString(QuotedPrintableBytes(body));
            default:
                return body;
        }
    }

    public static string? Parameter(string header, string name)
    {
        var m = Regex.Match(header, name + @"\s*=\s*(""(?<v>[^""]*)""|(?<v>[^;\s]+))", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[ValueGroup].Value : null;
    }

    // Encoded words in headers: =?charset?B|Q?…?=.
    public static string Words(string header) =>
        EncodedWord().Replace(header, m =>
        {
            var encoding = EncodingOf(m.Groups[1].Value);
            var data = m.Groups[3].Value;
            try
            {
                return m.Groups[2].Value.Equals(Base64Word, StringComparison.OrdinalIgnoreCase)
                    ? encoding.GetString(Convert.FromBase64String(data))
                    : Decode(data.Replace('_', ' '), QuotedPrintable, m.Groups[1].Value);
            }
            catch (FormatException)
            {
                return m.Value;
            }
        }).Trim();

    private static byte[] QuotedPrintableBytes(string body)
    {
        var bytes = new List<byte>();
        var text = body.Replace("=\n", string.Empty);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '=' && i + HexDigits < text.Length && Uri.IsHexDigit(text[i + 1]) && Uri.IsHexDigit(text[i + 2]))
            {
                bytes.Add(Convert.ToByte(text.Substring(i + 1, HexDigits), HexBase));
                i += HexDigits;
            }
            else
            {
                bytes.AddRange(Encoding.UTF8.GetBytes(text[i].ToString()));
            }
        }

        return bytes.ToArray();
    }

    private static Encoding EncodingOf(string? charset)
    {
        try
        {
            return string.IsNullOrWhiteSpace(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset.Trim('"'));
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    [GeneratedRegex(@"=\?([^?]+)\?([BbQq])\?([^?]*)\?=")]
    private static partial Regex EncodedWord();
}
