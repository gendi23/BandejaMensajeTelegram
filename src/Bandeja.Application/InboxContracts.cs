namespace Bandeja.Application;

public sealed record IncomingTelegramMessage(
    long ChatId,
    long TelegramMessageId,
    string DisplayName,
    string? Username,
    string Text,
    DateTimeOffset SentAt);

public sealed record ConversationSummary(
    long ChatId,
    string DisplayName,
    string? Username,
    int UnreadCount,
    string? LastMessage,
    DateTimeOffset? LastMessageAt);

public sealed record MessageView(
    Guid Id,
    long? TelegramMessageId,
    string Text,
    bool IsOutgoing,
    bool IsRead,
    DateTimeOffset CreatedAt);

public interface IConversationRepository
{
    Task<IReadOnlyList<ConversationSummary>> GetConversationsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<MessageView>> GetMessagesAsync(long chatId, CancellationToken cancellationToken);
    Task<bool> MarkReadAsync(long chatId, CancellationToken cancellationToken);
    Task<MessageView?> AddIncomingAsync(IncomingTelegramMessage message, CancellationToken cancellationToken);
    Task<MessageView> AddOutgoingAsync(long chatId, long? telegramMessageId, string text, CancellationToken cancellationToken);
}

public interface IMessagePublisher
{
    Task PublishAsync(IncomingTelegramMessage message, CancellationToken cancellationToken);
}

public interface ITelegramGateway
{
    Task<long?> SendTextAsync(long chatId, string text, CancellationToken cancellationToken);
}

public interface IInboxNotifier
{
    Task ConversationChangedAsync(long chatId, CancellationToken cancellationToken);
}
