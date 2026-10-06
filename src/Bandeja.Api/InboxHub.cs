using Microsoft.AspNetCore.SignalR;

namespace Bandeja.Api;

public sealed class InboxHub : Hub;

public sealed class SignalRInboxNotifier(IHubContext<InboxHub> hub) : Bandeja.Application.IInboxNotifier
{
    public Task ConversationChangedAsync(long chatId, CancellationToken cancellationToken) =>
        hub.Clients.All.SendAsync("conversationChanged", chatId, cancellationToken);
}
