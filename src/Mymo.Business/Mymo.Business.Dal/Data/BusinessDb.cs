using Microsoft.EntityFrameworkCore;
using Mymo.Shared;

namespace Mymo.Business.Data;

public class BusinessRow
{
    public Guid BusinessId { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Vibes { get; set; } = "chill";
    public bool Verified { get; set; }
    public string Status { get; set; } = "Approved";
    public string? RejectReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MenuRow
{
    public Guid MenuId { get; set; }
    public Guid PlaceId { get; set; }
    public string Name { get; set; } = "Menu";
    public string? Description { get; set; }
}

public class MenuItemRow
{
    public Guid MenuItemId { get; set; }
    public Guid MenuId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? CategoryName { get; set; }
    public int DisplayOrder { get; set; }
}

public class PromotionRow
{
    public Guid PromotionId { get; set; }
    public Guid PlaceId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
}

public class PackageRow
{
    public Guid ProductPackageId { get; set; }
    public string Segment { get; set; } = "B2C";
    public string Code { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public decimal PriceVnd { get; set; }
    public int? DurationDays { get; set; }
    public string? BillingUnit { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class PremiumOrderRow
{
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public string PlanId { get; set; } = "monthly";
    public decimal AmountVnd { get; set; }
    public string PaymentStatus { get; set; } = "Pending";
    public string TransferReferenceCode { get; set; } = "";
    public DateTime? PaymentSubmittedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CampaignRow
{
    public Guid CampaignId { get; set; }
    public Guid BusinessId { get; set; }
    public Guid PlaceId { get; set; }
    public string PackageId { get; set; } = "";
    public decimal AmountVnd { get; set; }
    public string PaymentStatus { get; set; } = "Pending";
    public string TransferReferenceCode { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BusinessDbContext : DbContext
{
    public BusinessDbContext(DbContextOptions<BusinessDbContext> options) : base(options) { }
    public DbSet<BusinessRow> Businesses => Set<BusinessRow>();
    public DbSet<MenuRow> Menus => Set<MenuRow>();
    public DbSet<MenuItemRow> MenuItems => Set<MenuItemRow>();
    public DbSet<PromotionRow> Promotions => Set<PromotionRow>();
    public DbSet<PackageRow> Packages => Set<PackageRow>();
    public DbSet<PremiumOrderRow> PremiumOrders => Set<PremiumOrderRow>();
    public DbSet<CampaignRow> Campaigns => Set<CampaignRow>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BusinessRow>().HasKey(b => b.BusinessId);
        modelBuilder.Entity<MenuRow>().HasKey(m => m.MenuId);
        modelBuilder.Entity<MenuItemRow>().HasKey(m => m.MenuItemId);
        modelBuilder.Entity<PromotionRow>().HasKey(p => p.PromotionId);
        modelBuilder.Entity<PackageRow>().HasKey(p => p.ProductPackageId);
        modelBuilder.Entity<PremiumOrderRow>().HasKey(o => o.OrderId);
        modelBuilder.Entity<CampaignRow>().HasKey(c => c.CampaignId);

        // Foreign keys within Business service
        modelBuilder.Entity<MenuItemRow>()
            .HasOne<MenuRow>()
            .WithMany()
            .HasForeignKey(m => m.MenuId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CampaignRow>()
            .HasOne<BusinessRow>()
            .WithMany()
            .HasForeignKey(c => c.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public static class BusinessSeed
{
    public static async Task Run(BusinessDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (!await db.Packages.AnyAsync())
        {
            db.Packages.AddRange(
                new PackageRow { ProductPackageId = Guid.NewGuid(), Segment = "B2C", Code = "monthly", DisplayName = "Tháng", PriceVnd = 49000, DurationDays = 30, SortOrder = 1 },
                new PackageRow { ProductPackageId = Guid.NewGuid(), Segment = "B2C", Code = "yearly", DisplayName = "Năm", PriceVnd = 399000, DurationDays = 365, SortOrder = 2 },
                new PackageRow { ProductPackageId = Guid.NewGuid(), Segment = "B2B", Code = "starter_spot", DisplayName = "Starter", PriceVnd = 1500000, BillingUnit = "campaign", SortOrder = 1 },
                new PackageRow { ProductPackageId = Guid.NewGuid(), Segment = "B2B", Code = "featured_venue", DisplayName = "Featured", PriceVnd = 3500000, BillingUnit = "campaign", SortOrder = 2 },
                new PackageRow { ProductPackageId = Guid.NewGuid(), Segment = "B2B", Code = "event_boost", DisplayName = "Event", PriceVnd = 2000000, BillingUnit = "campaign", SortOrder = 3 });
        }
        if (!await db.Businesses.AnyAsync())
        {
            db.Businesses.Add(new BusinessRow
            {
                BusinessId = Ids.Business,
                OwnerId = Ids.BusinessUser,
                Name = "Cafe Momo",
                Description = "Quán cà phê mẫu cho bản local.",
                Email = "business@mymo.app",
                Phone = "0900000000",
                Address = "Nguyễn Huệ, Quận 1",
                Latitude = 10.7735,
                Longitude = 106.7034,
                Vibes = "chill,date",
                Verified = true,
                Status = "Approved",
                CreatedAt = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync();
    }
}
