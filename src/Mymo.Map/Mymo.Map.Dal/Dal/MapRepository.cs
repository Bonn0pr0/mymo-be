using Microsoft.EntityFrameworkCore;
using Mymo.Map.Data;

namespace Mymo.Map.Dal;

public class MapRepository : IMapRepository
{
    private readonly MapDbContext _db;

    public MapRepository(MapDbContext db) => _db = db;

    public Task Save() => _db.SaveChangesAsync();

    public Task<List<PlaceRow>> ApprovedPlaces() =>
        _db.Places.Where(p => p.Status == "Approved").ToListAsync();

    public Task<PlaceRow?> FindPlace(Guid id) => _db.Places.FirstOrDefaultAsync(p => p.PlaceId == id);

    public Task<List<PlacePhotoRow>> Photos(Guid placeId) =>
        _db.Photos.Where(p => p.PlaceId == placeId).OrderBy(p => p.DisplayOrder).ToListAsync();

    public Task<List<ReviewRow>> Reviews(Guid placeId) =>
        _db.Reviews.Where(r => r.PlaceId == placeId).OrderByDescending(r => r.CreatedAt).ToListAsync();

    public Task<List<ReviewRow>> RecentReviews(Guid placeId, int take) =>
        _db.Reviews.Where(r => r.PlaceId == placeId).OrderByDescending(r => r.CreatedAt).Take(take).ToListAsync();

    public void AddReview(ReviewRow row) => _db.Reviews.Add(row);

    public Task<ReviewRow?> OwnReview(Guid reviewId, Guid? userId) =>
        _db.Reviews.FirstOrDefaultAsync(r => r.ReviewId == reviewId && r.UserId == userId);

    public void RemoveReview(ReviewRow row) => _db.Reviews.Remove(row);

    public Task<List<int>> Ratings(Guid placeId) =>
        _db.Reviews.Where(r => r.PlaceId == placeId).Select(r => r.Rating).ToListAsync();

    public void AddImpression(ImpressionRow row) => _db.Impressions.Add(row);

    public void AddCheckIn(CheckInRow row) => _db.CheckIns.Add(row);

    public Task<List<CheckInRow>> CheckInsOf(Guid userId) =>
        _db.CheckIns.Where(c => c.UserId == userId).OrderByDescending(c => c.CreatedAt).ToListAsync();

    public Task<List<PlaceRow>> Places(IEnumerable<Guid> ids)
    {
        var set = ids.ToList();
        return _db.Places.Where(p => set.Contains(p.PlaceId)).ToListAsync();
    }

    public Task<CheckInRow?> OwnCheckIn(Guid checkInId, Guid? userId) =>
        _db.CheckIns.FirstOrDefaultAsync(c => c.CheckInId == checkInId && c.UserId == userId);

    public void RemoveCheckIn(CheckInRow row) => _db.CheckIns.Remove(row);

    public Task<List<PlaceCategoryRow>> Categories() => _db.Categories.ToListAsync();

    public Task<List<PlaceRow>> PendingPlaces() =>
        _db.Places.Where(p => p.Status == "Pending").ToListAsync();

    public Task<List<PlaceRow>> PlacesOfBusiness(Guid businessId) =>
        _db.Places.Where(p => p.BusinessId == businessId).ToListAsync();

    public Task<PlaceCategoryRow?> FindCategory(Guid id) =>
        _db.Categories.FirstOrDefaultAsync(c => c.PlaceCategoryId == id);

    public void AddPlace(PlaceRow row) => _db.Places.Add(row);
}
