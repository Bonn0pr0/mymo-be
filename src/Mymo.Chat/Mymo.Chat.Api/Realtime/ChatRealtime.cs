using Microsoft.AspNetCore.SignalR;
using Mymo.Chat.Bll;
using Mymo.Chat.Hubs;

namespace Mymo.Chat;

public class ChatRealtime : IChatRealtime
{
    private readonly IHubContext<ChatHub> _hub;

    public ChatRealtime(IHubContext<ChatHub> hub) => _hub = hub;

    public Task ReceiveMessage(Guid conversationId, object dto) =>
        _hub.Clients.Group(conversationId.ToString()).SendAsync("ReceiveMessage", dto);

    public Task MessageDeleted(Guid conversationId, object dto) =>
        _hub.Clients.Group(conversationId.ToString()).SendAsync("MessageDeleted", dto);
}
