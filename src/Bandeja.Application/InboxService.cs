namespace Bandeja.Application;

public sealed class InboxService(
    IConversationRepository repository,
    ITelegramGateway telegram,
    IInboxNotifier notifier)
{
    public Task<IReadOnlyList<ConversationSummary>> GetConversationsAsync(CancellationToken cancellationToken) =>
        repository.GetConversationsAsync(cancellationToken);

    public Task<IReadOnlyList<MessageView>> GetMessagesAsync(long chatId, CancellationToken cancellationToken) =>
        repository.GetMessagesAsync(chatId, cancellationToken);

    public async Task MarkReadAsync(long chatId, CancellationToken cancellationToken)
    {
        if (await repository.MarkReadAsync(chatId, cancellationToken))
            await notifier.ConversationChangedAsync(chatId, cancellationToken);
    }

    public async Task ReceiveAsync(IncomingTelegramMessage message, CancellationToken cancellationToken)
    {
        var saved = await repository.AddIncomingAsync(message, cancellationToken);
        if (saved is not null)
            await notifier.ConversationChangedAsync(message.ChatId, cancellationToken);
    }

    public async Task<MessageView> ReplyAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("El mensaje no puede estar vacío.", nameof(text));

        var telegramMessageId = await telegram.SendTextAsync(chatId, text.Trim(), cancellationToken);
        var saved = await repository.AddOutgoingAsync(chatId, telegramMessageId, text.Trim(), cancellationToken);
        await notifier.ConversationChangedAsync(chatId, cancellationToken);
        return saved;
    }
}
