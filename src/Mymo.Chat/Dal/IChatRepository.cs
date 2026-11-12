using Mymo.Chat.Data;

namespace Mymo.Chat.Dal;

public interface IChatRepository
{
    Task Save();
    Task<List<MemberRow>> Memberships(Guid userId);
    Task<List<ConversationRow>> Conversations(IEnumerable<Guid> ids);
    Task<List<MemberRow>> MembersOf(IEnumerable<Guid> conversationIds);
    Task<Guid?> SharedPrivate(Guid me, Guid other);
    void AddConversation(ConversationRow row);
    void AddMember(MemberRow row);
    Task<ConversationRow?> FindConversation(Guid id);
    Task<bool> IsMember(Guid conversationId, Guid? userId);
    Task<int> MessageCount(Guid conversationId);
    Task<List<MessageRow>> Messages(Guid conversationId, int page, int pageSize);
    void AddMessage(MessageRow row);
    Task<MemberRow?> FindMember(Guid conversationId, Guid? userId);
    Task<bool> MessageExists(Guid messageId);
    Task<MessageRow?> OwnMessage(Guid messageId, Guid? userId);
}
