namespace Filum.Agent;

public sealed class Conversation
{
    public const int TitleLength = 60;

    public const int PreviewLength = 80;

    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static string TitleFrom(string firstMessage) => Cut(firstMessage, TitleLength);

    /// <summary>The beginning of the last message, as the chat list shows it.</summary>
    public static string PreviewFrom(string? lastMessage) => lastMessage is null ? string.Empty : Cut(lastMessage, PreviewLength);

    private static string Cut(string text, int length)
    {
        var trimmed = text.Trim();
        return trimmed.Length > length ? trimmed[..length] + "…" : trimmed;
    }
}
