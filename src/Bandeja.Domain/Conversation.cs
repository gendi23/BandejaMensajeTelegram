namespace Bandeja.Domain;

public sealed class Conversation
{
    public long ChatId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Username { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int UnreadCount { get; set; }
    public List<ChatMessage> Messages { get; set; } = [];
}
