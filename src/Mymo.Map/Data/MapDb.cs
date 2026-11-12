using Microsoft.EntityFrameworkCore;
using Mymo.Shared;

namespace Mymo.Map.Data;

public class PlaceCategoryRow
{
    public Guid PlaceCategoryId { get; set; }
    public string Name { get; set; } = "";
    public string? Icon { get; set; }
}

public class PlaceRow
{
    public Guid PlaceId { get; set; }
    public Guid? BusinessId { get; set; }
    public string BusinessName { get; set; } = "";
    public string? LogoUrl { get; set; }
    public bool Verified { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? OpeningHours { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string Vibes { get; set; } = "";
    public string Status { get; set; } = "Approved";
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int CheckInCount { get; set; }
    public string? AdPackageId { get; set; }
    public Guid OwnerId { get; set; }
}

public class PlacePhotoRow
{
    public Guid PlacePhotoId { get; set; }
    public Guid PlaceId { get; set; }
    public string ImageUrl { get; set; } = "";
    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
}

public class CheckInRow
{
    public Guid CheckInId { get; set; }
    public Guid UserId { get; set; }
    public Guid PlaceId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Mood { get; set; }
    public string? Weather { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReviewRow
{
    public Guid ReviewId { get; set; }
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public Guid PlaceId { get; set; }
    public int Rating { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ImpressionRow
{
    public Guid Id { get; set; }
    public Guid PlaceId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MapDbContext : DbContext
{
    public MapDbContext(DbContextOptions<MapDbContext> options) : base(options) { }
    public DbSet<PlaceCategoryRow> Categories => Set<PlaceCategoryRow>();
    public DbSet<PlaceRow> Places => Set<PlaceRow>();
    public DbSet<PlacePhotoRow> Photos => Set<PlacePhotoRow>();
    public DbSet<CheckInRow> CheckIns => Set<CheckInRow>();
    public DbSet<ReviewRow> Reviews => Set<ReviewRow>();
    public DbSet<ImpressionRow> Impressions => Set<ImpressionRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlaceCategoryRow>().HasKey(c => c.PlaceCategoryId);
        modelBuilder.Entity<PlaceRow>().HasKey(p => p.PlaceId);
        modelBuilder.Entity<PlacePhotoRow>().HasKey(p => p.PlacePhotoId);
        modelBuilder.Entity<CheckInRow>().HasKey(c => c.CheckInId);
        modelBuilder.Entity<ReviewRow>().HasKey(r => r.ReviewId);
    }
}

public static class MapSeed
{
    public static async Task Run(MapDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Places.AnyAsync()) return;
        db.Categories.Add(new PlaceCategoryRow { PlaceCategoryId = Ids.CafeCategory, Name = "Cafe", Icon = "cafe" });
        db.Places.AddRange(
            Place(Ids.PlaceNguyenHue, "Momo Nguyen Hue", "Quán cà phê nhìn ra phố đi bộ.", "Nguyễn Huệ, Quận 1", 10.7735, 106.7034, "chill,date"),
            Place(Ids.PlaceBookStreet, "Momo Book Street", "Chỗ ngồi học yên.", "Đường sách Nguyễn Văn Bình", 10.7798, 106.6995, "study,chill"));
        await db.SaveChangesAsync();
    }

    static PlaceRow Place(Guid id, string name, string description, string address, double lat, double lng, string vibes) => new()
    {
        PlaceId = id,
        BusinessId = Ids.Business,
        BusinessName = "Cafe Momo",
        Verified = true,
        CategoryId = Ids.CafeCategory,
        CategoryName = "Cafe",
        Name = name,
        Description = description,
        Address = address,
        City = "Hồ Chí Minh",
        Latitude = lat,
        Longitude = lng,
        Vibes = vibes,
        Status = "Approved",
        OwnerId = Ids.BusinessUser,
        OpeningHours = "07:00-22:00",
    };
}
