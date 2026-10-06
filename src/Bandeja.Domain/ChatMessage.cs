namespace Bandeja.Domain;

public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long ChatId { get; set; }
    public long? TelegramMessageId { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsOutgoing { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Conversation Conversation { get; set; } = null!;
}
