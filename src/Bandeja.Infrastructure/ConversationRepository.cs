using Bandeja.Application;
using Bandeja.Domain;
using Microsoft.EntityFrameworkCore;

namespace Bandeja.Infrastructure;

public sealed class ConversationRepository(InboxDbContext db) : IConversationRepository
{
    public async Task<IReadOnlyList<ConversationSummary>> GetConversationsAsync(CancellationToken cancellationToken) =>
        await db.Conversations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new ConversationSummary(
                x.ChatId,
                x.DisplayName,
                x.Username,
                x.UnreadCount,
                x.Messages.OrderByDescending(message => message.CreatedAt).Select(message => message.Text).FirstOrDefault(),
                x.Messages.OrderByDescending(message => message.CreatedAt).Select(message => (DateTimeOffset?)message.CreatedAt).FirstOrDefault()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MessageView>> GetMessagesAsync(long chatId, CancellationToken cancellationToken) =>
        await db.Messages
            .AsNoTracking()
            .Where(x => x.ChatId == chatId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new MessageView(x.Id, x.TelegramMessageId, x.Text, x.IsOutgoing, x.IsRead, x.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<bool> MarkReadAsync(long chatId, CancellationToken cancellationToken)
    {
        var conversation = await db.Conversations.FindAsync([chatId], cancellationToken);
        if (conversation is null)
            return false;

        conversation.UnreadCount = 0;
        await db.Messages
            .Where(x => x.ChatId == chatId && !x.IsOutgoing && !x.IsRead)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.IsRead, true), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<MessageView?> AddIncomingAsync(IncomingTelegramMessage message, CancellationToken cancellationToken)
    {
        var duplicate = await db.Messages.AnyAsync(
            x => x.ChatId == message.ChatId && x.TelegramMessageId == message.TelegramMessageId,
            cancellationToken);
        if (duplicate)
            return null;

        var conversation = await db.Conversations.FindAsync([message.ChatId], cancellationToken);
        if (conversation is null)
        {
            conversation = new Conversation { ChatId = message.ChatId };
            db.Conversations.Add(conversation);
        }

        conversation.DisplayName = string.IsNullOrWhiteSpace(message.DisplayName) ? message.ChatId.ToString() : message.DisplayName;
        conversation.Username = message.Username;
        conversation.UpdatedAt = message.SentAt;
        conversation.UnreadCount++;

        var chatMessage = new ChatMessage
        {
            ChatId = message.ChatId,
            TelegramMessageId = message.TelegramMessageId,
            Text = message.Text,
            CreatedAt = message.SentAt,
            IsOutgoing = false,
            IsRead = false
        };
        db.Messages.Add(chatMessage);
        await db.SaveChangesAsync(cancellationToken);
        return ToView(chatMessage);
    }

    public async Task<MessageView> AddOutgoingAsync(long chatId, long? telegramMessageId, string text, CancellationToken cancellationToken)
    {
        var conversation = await db.Conversations.FindAsync([chatId], cancellationToken)
            ?? throw new KeyNotFoundException("No existe la conversación solicitada.");

        var message = new ChatMessage
        {
            ChatId = chatId,
            TelegramMessageId = telegramMessageId,
            Text = text,
            CreatedAt = DateTimeOffset.UtcNow,
            IsOutgoing = true,
            IsRead = true
        };
        conversation.UpdatedAt = message.CreatedAt;
        db.Messages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        return ToView(message);
    }

    private static MessageView ToView(ChatMessage message) => new(
        message.Id,
        message.TelegramMessageId,
        message.Text,
        message.IsOutgoing,
        message.IsRead,
        message.CreatedAt);
}
