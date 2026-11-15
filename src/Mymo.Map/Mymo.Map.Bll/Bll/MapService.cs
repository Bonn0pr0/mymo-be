using System.Net.Http.Json;
using System.Text.Json;
using Mymo.Map.Dal;
using Mymo.Map.Data;
using Mymo.Shared;
using Mymo.Shared.Messaging;

namespace Mymo.Map.Bll;

public record IdList(List<Guid> Ids);

public class MapService
{
    private readonly IMapRepository _data;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;
    private readonly IMapRealtime _realtime;

    public MapService(IMapRepository data, IConfiguration config, IHttpClientFactory http, IMapRealtime realtime)
    {
        _data = data;
        _config = config;
        _http = http;
        _realtime = realtime;
    }

    static string[] Vibes(PlaceRow place) =>
        string.IsNullOrWhiteSpace(place.Vibes) ? [] : place.Vibes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    static object Summary(PlaceRow place, double distance, Guid? me) => new
    {
        place.PlaceId,
        id = place.PlaceId,
        place.Name,
        place.Latitude,
        place.Longitude,
        place.ThumbnailUrl,
        model3DKey = (string?)null,
        model3DUrl = (string?)null,
        place.AverageRating,
        place.ReviewCount,
        place.CheckInCount,
        distanceKm = Math.Round(distance, 2),
        place.Address,
        place.City,
        vibes = Vibes(place),
        isOwnedByCurrentUser = me != null && place.OwnerId == me,
        place.AdPackageId,
    };

    public async Task<ServiceResult> Nearby(ClaimsPrincipal principal, double? latitude, double? longitude, double radiusKm, string? vibe)
    {
        var lat = latitude ?? 10.77;
        var lng = longitude ?? 106.70;
        var me = HttpUser.Id(principal);
        var places = await _data.ApprovedPlaces();
        var data = places
            .Select(p => new { p, km = Geo.Km(lat, lng, p.Latitude, p.Longitude) })
            .Where(x => x.km <= radiusKm)
            .Where(x => string.IsNullOrWhiteSpace(vibe) || Vibes(x.p).Contains(vibe, StringComparer.OrdinalIgnoreCase))
            .OrderBy(x => x.km)
            .Select(x => Summary(x.p, x.km, me));
        return ServiceResult.Ok(ApiResponse<object>.Ok(data));
    }

    public async Task<ServiceResult> Detail(Guid id)
    {
        var place = await _data.FindPlace(id);
        if (place == null) return ServiceResult.NotFound(ApiResponse<object>.Fail("Không thấy địa điểm"));
        var photos = await _data.Photos(id);
        var reviews = await _data.RecentReviews(id, 5);
        return ServiceResult.Ok(ApiResponse<object>.Ok(new
        {
            place.PlaceId,
            place.Name,
            place.Description,
            place.Address,
            place.City,
            district = (string?)null,
            ward = (string?)null,
            country = "Việt Nam",
            place.Latitude,
            place.Longitude,
            place.OpeningHours,
            place.ThumbnailUrl,
            place.AverageRating,
            place.ReviewCount,
            place.CheckInCount,
            postCount = 0,
            vibes = Vibes(place),
            category = new { categoryId = place.CategoryId, name = place.CategoryName, icon = "cafe" },
            business = place.BusinessId == null ? null : new
            {
                businessId = place.BusinessId,
                name = place.BusinessName,
                place.LogoUrl,
                place.Verified,
            },
            recentPosts = Array.Empty<object>(),
            recentReviews = reviews.Select(r => new
            {
                r.ReviewId,
                r.Rating,
                r.Title,
                r.Content,
                r.CreatedAt,
                user = new { r.UserId, displayName = r.DisplayName, r.AvatarUrl },
            }),
            photos = photos.Select(p => new { id = p.PlacePhotoId, p.ImageUrl, p.IsPrimary, p.DisplayOrder }),
        }));
    }

    public async Task<ServiceResult> Reviews(Guid id)
    {
        var reviews = await _data.Reviews(id);
        return ServiceResult.Ok(ApiResponse<object>.Ok(reviews.Select(r => new
        {
            r.ReviewId,
            r.PlaceId,
            r.Rating,
            r.Title,
            r.Content,
            r.CreatedAt,
            updatedAt = (DateTime?)null,
            user = new { r.UserId, displayName = r.DisplayName, r.AvatarUrl },
        })));
    }

    public async Task<ServiceResult> AddReview(Guid id, ClaimsPrincipal principal, JsonElement body)
    {
        var me = HttpUser.Id(principal);
        if (me == null) return ServiceResult.Unauthorized();
        var card = await Internal.GetAsync<UserCard>(_http, $"{_config["Services:Identity"]}/internal/users/{me}");
        var review = new ReviewRow
        {
            ReviewId = Guid.NewGuid(),
            PlaceId = id,
            UserId = me.Value,
            DisplayName = card?.DisplayName ?? "User",
            AvatarUrl = card?.AvatarUrl,
            Rating = (int)(JsonBody.Num(body, "rating") ?? 5),
            Title = JsonBody.Text(body, "title"),
            Content = JsonBody.Text(body, "content"),
            CreatedAt = DateTime.UtcNow,
        };
        _data.AddReview(review);
        await RefreshRating(id);
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<object>.Ok(review));
    }

    public async Task<ServiceResult> EditReview(Guid id, ClaimsPrincipal principal, JsonElement body)
    {
        var me = HttpUser.Id(principal);
        var review = await _data.OwnReview(id, me);
        if (review == null) return ServiceResult.NotFound(ApiResponse<object>.Fail("Không thấy đánh giá"));
        if (JsonBody.Num(body, "rating") is double rating) review.Rating = (int)rating;
        review.Content = JsonBody.Text(body, "content") ?? review.Content;
        review.Title = JsonBody.Text(body, "title") ?? review.Title;
        await RefreshRating(review.PlaceId);
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<object>.Ok(review));
    }

    public async Task<ServiceResult> DeleteReview(Guid id, ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal);
        var review = await _data.OwnReview(id, me);
        if (review != null)
        {
            _data.RemoveReview(review);
            await _data.Save();
            await RefreshRating(review.PlaceId);
            await _data.Save();
        }
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    async Task RefreshRating(Guid placeId)
    {
        var place = await _data.FindPlace(placeId);
        if (place == null) return;
        var ratings = await _data.Ratings(placeId);
        place.ReviewCount = ratings.Count;
        place.AverageRating = ratings.Count == 0 ? 0 : ratings.Average();
    }

    public async Task<ServiceResult> Impressions(ClaimsPrincipal principal, JsonElement body)
    {
        var me = HttpUser.Id(principal);
        if (me == null) return ServiceResult.Unauthorized();
        if (body.TryGetProperty("placeIds", out var ids) && ids.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in ids.EnumerateArray())
            {
                if (!Guid.TryParse(item.GetString(), out var placeId)) continue;
                _data.AddImpression(new ImpressionRow { Id = Guid.NewGuid(), PlaceId = placeId, UserId = me.Value, CreatedAt = DateTime.UtcNow });
            }
            await _data.Save();
        }
        return ServiceResult.Empty();
    }

    public async Task<ServiceResult> CheckIn(ClaimsPrincipal principal, JsonElement body)
    {
        var me = HttpUser.Id(principal);
        if (me == null) return ServiceResult.Unauthorized();
        if (!Guid.TryParse(JsonBody.Text(body, "placeId"), out var placeId))
            return ServiceResult.BadRequest(ApiResponse<object>.Fail("Thiếu địa điểm"));
        var place = await _data.FindPlace(placeId);
        if (place == null) return ServiceResult.NotFound(ApiResponse<object>.Fail("Không thấy địa điểm"));
        var row = new CheckInRow
        {
            CheckInId = Guid.NewGuid(),
            UserId = me.Value,
            PlaceId = placeId,
            Latitude = JsonBody.Num(body, "latitude") ?? place.Latitude,
            Longitude = JsonBody.Num(body, "longitude") ?? place.Longitude,
            Mood = JsonBody.Text(body, "mood"),
            Weather = JsonBody.Text(body, "weather"),
            CreatedAt = DateTime.UtcNow,
        };
        _data.AddCheckIn(row);
        place.CheckInCount++;
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<object>.Ok(CheckDto(row, place)));
    }

    public async Task<ServiceResult> MyCheckIns(ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        var rows = await _data.CheckInsOf(me);
        var places = await _data.Places(rows.Select(r => r.PlaceId));
        return ServiceResult.Ok(ApiResponse<object>.Ok(rows.Select(r => CheckDto(r, places.FirstOrDefault(p => p.PlaceId == r.PlaceId)))));
    }

    public async Task<ServiceResult> DeleteCheckIn(Guid id, ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal);
        var row = await _data.OwnCheckIn(id, me);
        if (row != null)
        {
            _data.RemoveCheckIn(row);
            var place = await _data.FindPlace(row.PlaceId);
            if (place != null && place.CheckInCount > 0) place.CheckInCount--;
            await _data.Save();
        }
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    static object CheckDto(CheckInRow row, PlaceRow? place) => new
    {
        row.CheckInId,
        row.PlaceId,
        placeName = place?.Name,
        row.Latitude,
        row.Longitude,
        row.Mood,
        row.Weather,
        row.CreatedAt,
    };

    public async Task<ServiceResult> Weather(double lat, double lng)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lng}&current=temperature_2m,weather_code,is_day";
            var json = await client.GetFromJsonAsync<JsonElement>(url);
            var current = json.GetProperty("current");
            var code = current.GetProperty("weather_code").GetInt32();
            var kind = code switch
            {
                0 => "sunny",
                1 or 2 or 3 => "cloudy",
                45 or 48 => "fog",
                95 or 96 or 99 => "thunder",
                71 or 73 or 75 or 77 or 85 or 86 => "snow",
                _ => "rain",
            };
            return ServiceResult.Ok(ApiResponse<object>.Ok(new
            {
                tempC = current.GetProperty("temperature_2m").GetDouble(),
                isDay = current.GetProperty("is_day").GetInt32() == 1,
                kind,
            }));
        }
        catch
        {
            return ServiceResult.Ok(ApiResponse<object>.Ok(new { tempC = 30d, isDay = true, kind = "sunny" }));
        }
    }

    public async Task<ServiceResult> Categories()
    {
        var rows = await _data.Categories();
        return ServiceResult.Ok(ApiResponse<object>.Ok(rows.Select(c => new { c.PlaceCategoryId, categoryId = c.PlaceCategoryId, c.Name, c.Icon })));
    }

    public async Task<ServiceResult> Pending() =>
        ServiceResult.Ok(ApiResponse<object>.Ok(await _data.PendingPlaces()));

    public async Task<ServiceResult> SetStatus(Guid id, string status)
    {
        var place = await _data.FindPlace(id);
        if (place == null) return ServiceResult.NotFound();
        place.Status = status;
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> Lookup(IdList body)
    {
        var places = await _data.Places(body.Ids);
        return ServiceResult.Ok(places.Select(p => new { p.PlaceId, p.Name, p.ThumbnailUrl }));
    }

    public async Task<ServiceResult> One(Guid id)
    {
        var place = await _data.FindPlace(id);
        return place == null
            ? ServiceResult.NotFound()
            : ServiceResult.Ok(new { place.PlaceId, place.Name, place.Address, place.Latitude, place.Longitude, place.BusinessId, place.Status });
    }

    public async Task<ServiceResult> ByBusiness(Guid id) =>
        ServiceResult.Ok(await _data.PlacesOfBusiness(id));

    public async Task<ServiceResult> CreatePlace(JsonElement body)
    {
        var categoryId = Guid.TryParse(JsonBody.Text(body, "categoryId"), out var cat) ? cat : Ids.CafeCategory;
        var category = await _data.FindCategory(categoryId);
        var place = new PlaceRow
        {
            PlaceId = Guid.NewGuid(),
            BusinessId = Guid.TryParse(JsonBody.Text(body, "businessId"), out var businessId) ? businessId : null,
            BusinessName = JsonBody.Text(body, "businessName") ?? "",
            OwnerId = Guid.TryParse(JsonBody.Text(body, "ownerId"), out var owner) ? owner : Guid.Empty,
            CategoryId = categoryId,
            CategoryName = category?.Name ?? "Cafe",
            Name = JsonBody.Text(body, "name") ?? "Place",
            Description = JsonBody.Text(body, "description"),
            Address = JsonBody.Text(body, "address"),
            City = "Hồ Chí Minh",
            Phone = JsonBody.Text(body, "phone"),
            OpeningHours = JsonBody.Text(body, "openingHours"),
            Latitude = JsonBody.Num(body, "latitude") ?? 0,
            Longitude = JsonBody.Num(body, "longitude") ?? 0,
            ThumbnailUrl = JsonBody.Text(body, "thumbnailUrl"),
            Vibes = JsonBody.Text(body, "vibes") ?? "",
            Status = JsonBody.Text(body, "status") ?? "Approved",
        };
        _data.AddPlace(place);
        await _data.Save();
        return ServiceResult.Ok(place);
    }

    public async Task<ServiceResult> UpdatePlace(Guid id, JsonElement body)
    {
        var place = await _data.FindPlace(id);
        if (place == null) return ServiceResult.NotFound();
        place.Name = JsonBody.Text(body, "name") ?? place.Name;
        place.Description = JsonBody.Text(body, "description") ?? place.Description;
        place.Address = JsonBody.Text(body, "address") ?? place.Address;
        place.Phone = JsonBody.Text(body, "phone") ?? place.Phone;
        place.OpeningHours = JsonBody.Text(body, "openingHours") ?? place.OpeningHours;
        if (JsonBody.Num(body, "latitude") is double lat) place.Latitude = lat;
        if (JsonBody.Num(body, "longitude") is double lng) place.Longitude = lng;
        if (JsonBody.Text(body, "vibes") is string vibes) place.Vibes = vibes;
        if (JsonBody.Text(body, "status") is string status) place.Status = status;
        if (JsonBody.Text(body, "adPackageId") is string package) place.AdPackageId = package;
        await _data.Save();
        return ServiceResult.Ok(place);
    }

    public async Task<ServiceResult> Activity(Guid id)
    {
        var checkIns = await _data.CheckInsOf(id);
        return ServiceResult.Ok(new { checkIns = checkIns.Count, placesExplored = checkIns.Select(c => c.PlaceId).Distinct().Count() });
    }

    public async Task<ServiceResult> Visited(Guid id)
    {
        var rows = await _data.CheckInsOf(id);
        var places = await _data.Places(rows.Select(r => r.PlaceId));
        return ServiceResult.Ok(rows.Select(r =>
        {
            var place = places.FirstOrDefault(p => p.PlaceId == r.PlaceId);
            return new { r.PlaceId, name = place?.Name, thumbnailUrl = place?.ThumbnailUrl, visitedAt = r.CreatedAt };
        }));
    }

    public async Task ApplyPlaceAd(UpdatePlaceAdCommand cmd)
    {
        var place = await _data.FindPlace(cmd.PlaceId);
        if (place == null) return;
        place.AdPackageId = cmd.AdPackageId;
        place.Status = string.IsNullOrWhiteSpace(cmd.Status) ? place.Status : cmd.Status;
        await _data.Save();
    }

    public Task BroadcastPost(PostCreatedEvent evt) =>
        _realtime.MapPostCreated(evt);
}
