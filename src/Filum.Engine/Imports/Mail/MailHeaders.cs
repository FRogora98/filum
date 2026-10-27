namespace Filum.Engine.Imports.Mail;

// The header names and parameters a mail is read by, as MimeParts.Split keeps them: lower case.
internal static class MailHeaders
{
    public const string From = "from";
    public const string Subject = "subject";
    public const string Date = "date";
    public const string MessageId = "message-id";
    public const string ContentType = "content-type";
    public const string ContentTransferEncoding = "content-transfer-encoding";
    public const string Boundary = "boundary";
    public const string Charset = "charset";
}
