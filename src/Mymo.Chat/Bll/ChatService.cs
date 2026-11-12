using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Mymo.Chat.Dal;
using Mymo.Chat.Data;
using Mymo.Chat.Hubs;
using Mymo.Shared;

namespace Mymo.Chat.Bll;

public class ChatService
{
    private readonly IChatRepository _data;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;
    private readonly IHubContext<ChatHub> _hub;

    public ChatService(IChatRepository data, IConfiguration config, IHttpClientFactory http, IHubContext<ChatHub> hub)
    {
        _data = data;
        _config = config;
        _http = http;
        _hub = hub;
    }

    public async Task<ServiceResult> List(ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        var memberships = await _data.Memberships(me);
        var ids = memberships.Select(m => m.ConversationId).ToList();
        var conversations = await _data.Conversations(ids);
        var members = await _data.MembersOf(ids);
        var otherIds = members.Where(m => m.UserId != me).Select(m => m.UserId).Distinct();
        var cards = await Internal.PostAsync<List<UserCard>>(_http, $"{_config["Services:Identity"]}/internal/users/batch", new { ids = otherIds }) ?? [];
        var data = conversations.OrderByDescending(c => c.LastMessageTime).Select(c =>
        {
            var other = members.FirstOrDefault(m => m.ConversationId == c.ConversationId && m.UserId != me);
            var card = cards.FirstOrDefault(x => x.UserId == other?.UserId);
            var mine = memberships.First(m => m.ConversationId == c.ConversationId);
            return new
            {
                c.ConversationId,
                conversationName = card?.DisplayName ?? "Chat",
                conversationAvatar = card?.AvatarUrl,
                otherUserId = other?.UserId,
                conversationType = c.Type,
                c.LastMessage,
                c.LastMessageTime,
                unreadCount = 0,
                lastReadAt = mine.LastReadAt,
            };
        });
        return ServiceResult.Ok(ApiResponse<object>.Ok(data));
    }

    public async Task<ServiceResult> OpenPrivate(ClaimsPrincipal principal, JsonElement body)
    {
        var me = HttpUser.Id(principal)!.Value;
        if (!Guid.TryParse(JsonBody.Text(body, "userId"), out var other))
            return ServiceResult.BadRequest(ApiResponse<object>.Fail("Thiếu người nhận"));
        var shared = await _data.SharedPrivate(me, other);
        Guid conversationId;
        if (shared != null)
        {
            conversationId = shared.Value;
        }
        else
        {
            conversationId = Guid.NewGuid();
            _data.AddConversation(new ConversationRow { ConversationId = conversationId, Type = "Private", CreatedBy = me, CreatedAt = DateTime.UtcNow });
            _data.AddMember(new MemberRow { MemberId = Guid.NewGuid(), ConversationId = conversationId, UserId = me });
            _data.AddMember(new MemberRow { MemberId = Guid.NewGuid(), ConversationId = conversationId, UserId = other });
            await _data.Save();
        }
        var card = await Internal.GetAsync<UserCard>(_http, $"{_config["Services:Identity"]}/internal/users/{other}");
        var conversation = await _data.FindConversation(conversationId);
        return ServiceResult.Ok(ApiResponse<object>.Ok(new
        {
            conversationId,
            conversationName = card?.DisplayName ?? "Chat",
            conversationAvatar = card?.AvatarUrl,
            otherUserId = other,
            conversationType = "Private",
            conversation?.LastMessage,
            conversation?.LastMessageTime,
            unreadCount = 0,
        }));
    }

    public async Task<ServiceResult> Messages(Guid id, ClaimsPrincipal principal, int page, int pageSize)
    {
        var me = HttpUser.Id(principal);
        if (!await _data.IsMember(id, me)) return ServiceResult.Forbidden();
        var total = await _data.MessageCount(id);
        var items = await _data.Messages(id, page, pageSize);
        return ServiceResult.Ok(ApiResponse<object>.Ok(new { items = items.Select(MessageDto), page, pageSize, totalCount = total }));
    }

    public async Task<ServiceResult> Send(Guid id, ClaimsPrincipal principal, JsonElement body)
    {
        var me = HttpUser.Id(principal);
        if (me == null) return ServiceResult.Unauthorized();
        if (!await _data.IsMember(id, me)) return ServiceResult.Forbidden();
        var card = await Internal.GetAsync<UserCard>(_http, $"{_config["Services:Identity"]}/internal/users/{me}");
        var message = new MessageRow
        {
            MessageId = Guid.NewGuid(),
            ConversationId = id,
            SenderId = me.Value,
            SenderName = card?.DisplayName ?? "User",
            SenderAvatar = card?.AvatarUrl,
            MessageType = (int)(JsonBody.Num(body, "messageType") ?? 0),
            Content = JsonBody.Text(body, "content") ?? "",
            Latitude = JsonBody.Num(body, "latitude"),
            Longitude = JsonBody.Num(body, "longitude"),
            CreatedAt = DateTime.UtcNow,
        };
        if (Guid.TryParse(JsonBody.Text(body, "referenceId"), out var reference)) message.ReferenceId = reference;
        _data.AddMessage(message);
        var conversation = await _data.FindConversation(id);
        if (conversation != null)
        {
            conversation.LastMessage = message.Content;
            conversation.LastMessageTime = message.CreatedAt;
        }
        await _data.Save();
        var dto = MessageDto(message);
        await _hub.Clients.Group(id.ToString()).SendAsync("ReceiveMessage", dto);
        return ServiceResult.Ok(ApiResponse<object>.Ok(dto));
    }

    public async Task<ServiceResult> ReadAll(Guid id, ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal);
        var member = await _data.FindMember(id, me);
        if (member != null) member.LastReadAt = DateTime.UtcNow;
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> ReadOne(Guid id) =>
        ServiceResult.Ok(ApiResponse<bool>.Ok(await _data.MessageExists(id)));

    public async Task<ServiceResult> Delete(Guid id, ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal);
        var message = await _data.OwnMessage(id, me);
        if (message == null) return ServiceResult.NotFound(ApiResponse<bool>.Fail("Không thấy tin nhắn"));
        message.IsDeleted = true;
        await _data.Save();
        await _hub.Clients.Group(message.ConversationId.ToString()).SendAsync("MessageDeleted", new { message.MessageId });
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    static object MessageDto(MessageRow message) => new
    {
        message.MessageId,
        message.ConversationId,
        sender = new { userId = message.SenderId, displayName = message.SenderName, avatarUrl = message.SenderAvatar },
        message.MessageType,
        content = message.IsDeleted ? "This message has been deleted." : message.Content,
        message.MediaUrl,
        message.Latitude,
        message.Longitude,
        message.ReferenceId,
        message.CreatedAt,
        isRead = true,
        message.IsDeleted,
    };
}
