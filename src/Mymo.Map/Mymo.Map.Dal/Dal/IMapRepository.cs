using Mymo.Map.Data;

namespace Mymo.Map.Dal;

public interface IMapRepository
{
    Task Save();
    Task<List<PlaceRow>> ApprovedPlaces();
    Task<PlaceRow?> FindPlace(Guid id);
    Task<List<PlacePhotoRow>> Photos(Guid placeId);
    Task<List<ReviewRow>> Reviews(Guid placeId);
    Task<List<ReviewRow>> RecentReviews(Guid placeId, int take);
    void AddReview(ReviewRow row);
    Task<ReviewRow?> OwnReview(Guid reviewId, Guid? userId);
    void RemoveReview(ReviewRow row);
    Task<List<int>> Ratings(Guid placeId);
    void AddImpression(ImpressionRow row);
    void AddCheckIn(CheckInRow row);
    Task<List<CheckInRow>> CheckInsOf(Guid userId);
    Task<List<PlaceRow>> Places(IEnumerable<Guid> ids);
    Task<CheckInRow?> OwnCheckIn(Guid checkInId, Guid? userId);
    void RemoveCheckIn(CheckInRow row);
    Task<List<PlaceCategoryRow>> Categories();
    Task<List<PlaceRow>> PendingPlaces();
    Task<List<PlaceRow>> PlacesOfBusiness(Guid businessId);
    Task<PlaceCategoryRow?> FindCategory(Guid id);
    void AddPlace(PlaceRow row);
}
