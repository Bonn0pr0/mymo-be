using Microsoft.EntityFrameworkCore;
using Mymo.Shared;

namespace Mymo.Social.Data;

public class PostRow
{
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = "";
    public string Username { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public bool AuthorIsPremium { get; set; }
    public string? Caption { get; set; }
    public string PostType { get; set; } = "Photo";
    public string Visibility { get; set; } = "Public";
    public bool IsAnonymous { get; set; }
    public string? AnonymousAlias { get; set; }
    public Guid? PlaceId { get; set; }
    public string? PlaceName { get; set; }
    public string? PlaceAddress { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public int ViewCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsDeleted { get; set; }
}

public class MediaRow
{
    public Guid MediaId { get; set; }
    public Guid UserId { get; set; }
    public Guid? PostId { get; set; }
    public string Url { get; set; } = "";
    public string? ThumbnailUrl { get; set; }
    public string MediaType { get; set; } = "Image";
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Duration { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CommentRow
{
    public Guid CommentId { get; set; }
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ParentCommentId { get; set; }
    public string Content { get; set; } = "";
    public int LikeCount { get; set; }
    public string DisplayName { get; set; } = "";
    public string Username { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public class LikeRow
{
    public Guid LikeId { get; set; }
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
}

public class PostViewRow
{
    public Guid PostViewId { get; set; }
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FriendshipRow
{
    public Guid FriendshipId { get; set; }
    public Guid RequesterId { get; set; }
    public Guid ReceiverId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
}

public class NotificationRow
{
    public Guid NotificationId { get; set; }
    public Guid ReceiverId { get; set; }
    public Guid SenderId { get; set; }
    public string Type { get; set; } = "info";
    public string? Title { get; set; }
    public string? Content { get; set; }
    public Guid? ReferenceId { get; set; }
    public bool IsRead { get; set; }
    public string? SenderName { get; set; }
    public string? SenderAvatar { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SocialDbContext : DbContext
{
    public SocialDbContext(DbContextOptions<SocialDbContext> options) : base(options) { }
    public DbSet<PostRow> Posts => Set<PostRow>();
    public DbSet<MediaRow> Media => Set<MediaRow>();
    public DbSet<CommentRow> Comments => Set<CommentRow>();
    public DbSet<LikeRow> Likes => Set<LikeRow>();
    public DbSet<PostViewRow> Views => Set<PostViewRow>();
    public DbSet<FriendshipRow> Friendships => Set<FriendshipRow>();
    public DbSet<NotificationRow> Notifications => Set<NotificationRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PostRow>().HasKey(p => p.PostId);
        modelBuilder.Entity<MediaRow>().HasKey(m => m.MediaId);
        modelBuilder.Entity<CommentRow>().HasKey(c => c.CommentId);
        modelBuilder.Entity<LikeRow>().HasKey(l => l.LikeId);
        modelBuilder.Entity<PostViewRow>().HasKey(v => v.PostViewId);
        modelBuilder.Entity<FriendshipRow>().HasKey(f => f.FriendshipId);
        modelBuilder.Entity<NotificationRow>().HasKey(n => n.NotificationId);
        modelBuilder.Entity<LikeRow>().HasIndex(l => new { l.PostId, l.UserId }).IsUnique();
        modelBuilder.Entity<FriendshipRow>().HasIndex(f => new { f.RequesterId, f.ReceiverId });
    }
}

public static class SocialSeed
{
    public static async Task Run(SocialDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Posts.AnyAsync()) return;
        db.Posts.Add(new PostRow
        {
            PostId = Ids.DemoPost,
            UserId = Ids.Demo,
            DisplayName = "Demo",
            Username = "demo",
            Caption = "Cà phê buổi chiều ở Nguyễn Huệ.",
            PostType = "Photo",
            Visibility = "Public",
            PlaceId = Ids.PlaceNguyenHue,
            PlaceName = "Momo Nguyen Hue",
            PlaceAddress = "Nguyễn Huệ, Quận 1",
            Latitude = 10.7735,
            Longitude = 106.7034,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
        });
        await db.SaveChangesAsync();
    }
}
