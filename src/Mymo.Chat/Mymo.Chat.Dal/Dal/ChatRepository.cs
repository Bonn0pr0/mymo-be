using Microsoft.EntityFrameworkCore;
using Mymo.Chat.Data;

namespace Mymo.Chat.Dal;

public class ChatRepository : IChatRepository
{
    private readonly ChatDbContext _db;

    public ChatRepository(ChatDbContext db) => _db = db;

    public Task Save() => _db.SaveChangesAsync();

    public Task<List<MemberRow>> Memberships(Guid userId) =>
        _db.Members.Where(m => m.UserId == userId).ToListAsync();

    public Task<List<ConversationRow>> Conversations(IEnumerable<Guid> ids)
    {
        var set = ids.ToList();
        return _db.Conversations.Where(c => set.Contains(c.ConversationId)).ToListAsync();
    }

    public Task<List<MemberRow>> MembersOf(IEnumerable<Guid> conversationIds)
    {
        var set = conversationIds.ToList();
        return _db.Members.Where(m => set.Contains(m.ConversationId)).ToListAsync();
    }

    public async Task<Guid?> SharedPrivate(Guid me, Guid other)
    {
        var mine = await _db.Members.Where(m => m.UserId == me).Select(m => m.ConversationId).ToListAsync();
        var shared = await _db.Members.Where(m => m.UserId == other && mine.Contains(m.ConversationId)).Select(m => m.ConversationId).FirstOrDefaultAsync();
        if (shared != Guid.Empty && await _db.Conversations.AnyAsync(c => c.ConversationId == shared && c.Type == "Private"))
            return shared;
        return null;
    }

    public void AddConversation(ConversationRow row) => _db.Conversations.Add(row);
    public void AddMember(MemberRow row) => _db.Members.Add(row);

    public Task<ConversationRow?> FindConversation(Guid id) =>
        _db.Conversations.FirstOrDefaultAsync(c => c.ConversationId == id);

    public Task<bool> IsMember(Guid conversationId, Guid? userId) =>
        _db.Members.AnyAsync(m => m.ConversationId == conversationId && m.UserId == userId);

    public Task<int> MessageCount(Guid conversationId) =>
        _db.Messages.CountAsync(m => m.ConversationId == conversationId);

    public async Task<List<MessageRow>> Messages(Guid conversationId, int page, int pageSize)
    {
        var items = await _db.Messages.Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        items.Reverse();
        return items;
    }

    public void AddMessage(MessageRow row) => _db.Messages.Add(row);

    public Task<MemberRow?> FindMember(Guid conversationId, Guid? userId) =>
        _db.Members.FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == userId);

    public Task<bool> MessageExists(Guid messageId) =>
        _db.Messages.AnyAsync(m => m.MessageId == messageId);

    public Task<MessageRow?> OwnMessage(Guid messageId, Guid? userId) =>
        _db.Messages.FirstOrDefaultAsync(m => m.MessageId == messageId && m.SenderId == userId);
}
