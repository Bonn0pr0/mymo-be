using Microsoft.EntityFrameworkCore;
using Mymo.Shared;

namespace Mymo.Identity.Data;

public class AppUser
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string PasswordHash { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public string? CoverUrl { get; set; }
    public string? Bio { get; set; }
    public string? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeen { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public string Role { get; set; } = "User";
    public string Status { get; set; } = "Active";
    public bool IsLocationSharing { get; set; } = true;
    public bool IsAnonymous { get; set; }
    public string? MapMarker { get; set; }
    public string? AvatarFrame { get; set; }
    public string? ProfileTheme { get; set; }
    public string? ProfileTitle { get; set; }
    public bool IsPremium { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public string FavoritePlacesJson { get; set; } = "[]";
}

public class UserSession
{
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public string RefreshToken { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

public class OtpChallenge
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string Purpose { get; set; } = "register";
    public string Code { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

public class ReportRow
{
    public Guid ReportId { get; set; }
    public Guid ReporterId { get; set; }
    public string TargetType { get; set; } = "Post";
    public Guid TargetId { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string? TargetSummary { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AuditLogRow
{
    public Guid AuditLogId { get; set; }
    public Guid UserId { get; set; }
    public string Action { get; set; } = "Update";
    public string EntityName { get; set; } = "";
    public Guid EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<UserSession> Sessions => Set<UserSession>();
    public DbSet<OtpChallenge> Otps => Set<OtpChallenge>();
    public DbSet<ReportRow> Reports => Set<ReportRow>();
    public DbSet<AuditLogRow> AuditLogs => Set<AuditLogRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().HasKey(u => u.UserId);
        modelBuilder.Entity<UserSession>().HasKey(s => s.SessionId);
        modelBuilder.Entity<ReportRow>().HasKey(r => r.ReportId);
        modelBuilder.Entity<AuditLogRow>().HasKey(a => a.AuditLogId);
        modelBuilder.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<UserSession>().HasIndex(s => s.RefreshToken);
        modelBuilder.Entity<OtpChallenge>().HasIndex(o => new { o.Email, o.Purpose });

        // Foreign keys within Identity service
        modelBuilder.Entity<UserSession>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AuditLogRow>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReportRow>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(r => r.ReporterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public static class IdentitySeed
{
    public static async Task Run(IdentityDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Users.AnyAsync()) return;

        db.Users.AddRange(
            User(Ids.Admin, "admin", "Admin", "admin@mymo.app", "admin123", "Admin"),
            User(Ids.Demo, "demo", "Demo", "demo@mymo.app", "Demo123!", "User"),
            User(Ids.BusinessUser, "cafe.momo", "Cafe Momo", "business@mymo.app", "business123", "Business"));
        await db.SaveChangesAsync();
    }

    private static AppUser User(Guid id, string username, string name, string email, string password, string role) => new()
    {
        UserId = id,
        Username = username,
        DisplayName = name,
        Email = email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        Role = role,
        Status = "Active",
        CreatedAt = DateTime.UtcNow,
        IsLocationSharing = true,
        Latitude = 10.7769,
        Longitude = 106.7009,
    };
}
