using Microsoft.EntityFrameworkCore;

namespace Mymo.Chat.Data;

public class ConversationRow
{
    public Guid ConversationId { get; set; }
    public string Type { get; set; } = "Private";
    public Guid CreatedBy { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? LastMessageTime { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MemberRow
{
    public Guid MemberId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public DateTime? LastReadAt { get; set; }
}

public class MessageRow
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = "";
    public string? SenderAvatar { get; set; }
    public int MessageType { get; set; }
    public string Content { get; set; } = "";
    public string? MediaUrl { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public Guid? ReferenceId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ChatDbContext : DbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options) { }
    public DbSet<ConversationRow> Conversations => Set<ConversationRow>();
    public DbSet<MemberRow> Members => Set<MemberRow>();
    public DbSet<MessageRow> Messages => Set<MessageRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConversationRow>().HasKey(c => c.ConversationId);
        modelBuilder.Entity<MemberRow>().HasKey(m => m.MemberId);
        modelBuilder.Entity<MessageRow>().HasKey(m => m.MessageId);
    }
}

public static class ChatSeed
{
    public static async Task Run(ChatDbContext db) => await db.Database.EnsureCreatedAsync();
}
