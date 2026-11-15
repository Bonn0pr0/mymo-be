using System.Text.Json;
using Mymo.Identity.Dal;
using Mymo.Identity.Data;
using Mymo.Shared;

namespace Mymo.Identity.Bll;

public class UserService
{
    private readonly IIdentityRepository _data;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;

    public UserService(IIdentityRepository data, IConfiguration config, IHttpClientFactory http)
    {
        _data = data;
        _config = config;
        _http = http;
    }

    public async Task<ServiceResult> Me(ClaimsPrincipal principal)
    {
        var user = await Require(principal);
        if (user == null) return ServiceResult.Unauthorized();
        var (friends, posts, places) = await Counts(user.UserId);
        return ServiceResult.Ok(ApiResponse<object>.Ok(IdentityMaps.Profile(user, friends, posts, places)));
    }

    public async Task<ServiceResult> PublicProfile(Guid id)
    {
        var user = await _data.FindUser(id);
        if (user == null || user.IsDeleted) return ServiceResult.NotFound(ApiResponse<object>.Fail("User not found"));
        var (friends, posts, places) = await Counts(user.UserId);
        return ServiceResult.Ok(ApiResponse<object>.Ok(IdentityMaps.Profile(user, friends, posts, places)));
    }

    public async Task<ServiceResult> UpdateMe(ClaimsPrincipal principal, JsonElement body)
    {
        var user = await Require(principal);
        if (user == null) return ServiceResult.Unauthorized();
        user.DisplayName = JsonBody.Text(body, "displayName") ?? user.DisplayName;
        user.Bio = JsonBody.Text(body, "bio") ?? user.Bio;
        user.Gender = JsonBody.Text(body, "gender") ?? user.Gender;
        user.PhoneNumber = JsonBody.Text(body, "phoneNumber") ?? user.PhoneNumber;
        if (DateOnly.TryParse(JsonBody.Text(body, "dateOfBirth"), out var dob)) user.DateOfBirth = dob;
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<object>.Ok(IdentityMaps.Profile(user, 0, 0, 0)));
    }

    public async Task<ServiceResult> ChangePassword(ClaimsPrincipal principal, JsonElement body)
    {
        var user = await Require(principal);
        if (user == null) return ServiceResult.Unauthorized();
        var current = JsonBody.Text(body, "currentPassword") ?? JsonBody.Text(body, "oldPassword") ?? "";
        var next = JsonBody.Text(body, "newPassword") ?? "";
        if (!BCrypt.Net.BCrypt.Verify(current, user.PasswordHash))
            return ServiceResult.BadRequest(ApiResponse<object>.Fail("Mật khẩu hiện tại không đúng"));
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(next);
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<object?>.Ok(null));
    }

    public async Task<ServiceResult> Settings(ClaimsPrincipal principal, JsonElement body)
    {
        var user = await Require(principal);
        if (user == null) return ServiceResult.Unauthorized();
        if (body.TryGetProperty("isLocationSharing", out var share)) user.IsLocationSharing = share.GetBoolean();
        if (body.TryGetProperty("isAnonymous", out var anon)) user.IsAnonymous = anon.GetBoolean();
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> Location(ClaimsPrincipal principal, JsonElement body)
    {
        var user = await Require(principal);
        if (user == null) return ServiceResult.Unauthorized();
        if (body.TryGetProperty("latitude", out var lat) && lat.ValueKind != JsonValueKind.Null) user.Latitude = lat.GetDouble();
        if (body.TryGetProperty("longitude", out var lng) && lng.ValueKind != JsonValueKind.Null) user.Longitude = lng.GetDouble();
        user.LastActiveAt = DateTime.UtcNow;
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> Style(ClaimsPrincipal principal, JsonElement body)
    {
        var user = await Require(principal);
        if (user == null) return ServiceResult.Unauthorized();
        user.AvatarFrame = JsonBody.Text(body, "avatarFrame") ?? user.AvatarFrame;
        user.ProfileTheme = JsonBody.Text(body, "profileTheme") ?? user.ProfileTheme;
        user.ProfileTitle = JsonBody.Text(body, "profileTitle") ?? user.ProfileTitle;
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<object>.Ok(IdentityMaps.Profile(user, 0, 0, 0)));
    }

    public async Task<ServiceResult> Favorites(ClaimsPrincipal principal, JsonElement body)
    {
        var user = await Require(principal);
        if (user == null) return ServiceResult.Unauthorized();
        var ids = new List<string>();
        if (body.TryGetProperty("placeIds", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray()) ids.Add(item.GetString() ?? "");
        }
        var looked = await Internal.PostAsync<List<PlaceSnippet>>(_http, $"{_config["Services:Map"]}/internal/places/lookup", new { ids });
        user.FavoritePlacesJson = JsonSerializer.Serialize(looked ?? []);
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<object>.Ok(IdentityMaps.Profile(user, 0, 0, 0)));
    }

    public async Task<ServiceResult> Visited(ClaimsPrincipal principal)
    {
        var id = HttpUser.Id(principal);
        if (id == null) return ServiceResult.Unauthorized();
        var places = await Internal.GetAsync<List<object>>(_http, $"{_config["Services:Map"]}/internal/users/{id}/visited") ?? [];
        return ServiceResult.Ok(ApiResponse<object>.Ok(places));
    }

    public async Task<ServiceResult> Stats(ClaimsPrincipal principal)
    {
        var id = HttpUser.Id(principal);
        if (id == null) return ServiceResult.Unauthorized();
        var social = await Internal.GetAsync<SocialActivity>(_http, $"{_config["Services:Social"]}/internal/users/{id}/activity");
        var map = await Internal.GetAsync<MapActivity>(_http, $"{_config["Services:Map"]}/internal/users/{id}/activity");
        return ServiceResult.Ok(ApiResponse<object>.Ok(new
        {
            placesExplored = map?.PlacesExplored ?? 0,
            postsCreated = social?.PostCount ?? 0,
            photoPosts = social?.PhotoPosts ?? 0,
            checkIns = map?.CheckIns ?? 0,
            likesReceived = social?.LikesReceived ?? 0,
            commentsReceived = social?.CommentsReceived ?? 0,
            newFriends = social?.FriendCount ?? 0,
            streakDays = 0,
            months = Array.Empty<object>(),
            interests = Array.Empty<object>(),
            achievements = Array.Empty<object>(),
        }));
    }

    public async Task<ServiceResult> NearbyOnline(ClaimsPrincipal principal, double? latitude, double? longitude, double? radiusKm)
    {
        var me = HttpUser.Id(principal);
        var lat = latitude ?? 10.77;
        var lng = longitude ?? 106.70;
        var radius = radiusKm ?? 25;
        var since = DateTime.UtcNow.AddMinutes(-10);
        var users = await _data.LocationSharersExcept(me);
        var data = users
            .Select(u => new { u, km = Geo.Km(lat, lng, u.Latitude!.Value, u.Longitude ?? 0) })
            .Where(x => x.km <= radius && (x.u.IsOnline || x.u.LastActiveAt >= since))
            .Select(x => new
            {
                userId = x.u.UserId,
                displayName = x.u.DisplayName,
                avatarUrl = x.u.AvatarUrl,
                latitude = x.u.Latitude,
                longitude = x.u.Longitude,
                isOnline = x.u.IsOnline,
            });
        return ServiceResult.Ok(ApiResponse<object>.Ok(data));
    }

    public Task<ServiceResult> UploadAvatar(ClaimsPrincipal principal, IFormFile? file) =>
        SaveImage(principal, file, (u, url) => u.AvatarUrl = url);

    public Task<ServiceResult> UploadCover(ClaimsPrincipal principal, IFormFile? file) =>
        SaveImage(principal, file, (u, url) => u.CoverUrl = url);

    public Task<ServiceResult> UploadMarker(ClaimsPrincipal principal, IFormFile? file) =>
        SaveImage(principal, file, (u, url) => u.MapMarker = url);

    public async Task<ServiceResult> MapMarker(ClaimsPrincipal principal, JsonElement body)
    {
        var user = await Require(principal);
        if (user == null) return ServiceResult.Unauthorized();
        user.MapMarker = JsonBody.Text(body, "marker");
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<string?>.Ok(user.MapMarker));
    }

    public async Task<ServiceResult> ProxyUserPosts(Guid id, string? authorization)
    {
        var client = _http.CreateClient("internal");
        var url = $"{_config["Services:Social"]}/api/internal/users/{id}/posts";
        if (!string.IsNullOrEmpty(authorization))
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization);
        var response = await client.GetAsync(url);
        var json = await response.Content.ReadAsStringAsync();
        return ServiceResult.RawJson(json, (int)response.StatusCode);
    }

    public async Task<ServiceResult> InternalUser(Guid id)
    {
        var user = await _data.FindUser(id);
        return user == null ? ServiceResult.NotFound() : ServiceResult.Ok(IdentityMaps.Card(user));
    }

    public async Task<ServiceResult> InternalBatch(IdList body)
    {
        var users = await _data.UsersByIds(body.Ids);
        return ServiceResult.Ok(users.Select(IdentityMaps.Card));
    }

    public async Task<ServiceResult> InternalSearch(string? keyword)
    {
        var k = (keyword ?? "").Trim().ToLower();
        var users = await _data.SearchUsers(k);
        return ServiceResult.Ok(users.Select(IdentityMaps.Card));
    }

    public async Task<ServiceResult> Presence(Guid id, JsonElement body)
    {
        var user = await _data.FindUser(id);
        if (user == null) return ServiceResult.NotFound();
        user.IsOnline = !body.TryGetProperty("isOnline", out var online) || online.GetBoolean();
        user.LastSeen = DateTime.UtcNow;
        user.LastActiveAt = DateTime.UtcNow;
        if (body.TryGetProperty("latitude", out var lat) && lat.ValueKind == JsonValueKind.Number) user.Latitude = lat.GetDouble();
        if (body.TryGetProperty("longitude", out var lng) && lng.ValueKind == JsonValueKind.Number) user.Longitude = lng.GetDouble();
        await _data.Save();
        return ServiceResult.Empty();
    }

    public async Task ApplyPremium(Guid userId, bool isPremium)
    {
        var user = await _data.FindUser(userId);
        if (user == null) return;
        user.IsPremium = isPremium;
        await _data.Save();
    }

    public async Task ApplyRole(Guid userId, string role)
    {
        var user = await _data.FindUser(userId);
        if (user == null) return;
        user.Role = string.IsNullOrWhiteSpace(role) ? user.Role : role;
        await _data.Save();
    }

    async Task<AppUser?> Require(ClaimsPrincipal principal)
    {
        var id = HttpUser.Id(principal);
        return id == null ? null : await _data.FindUser(id.Value);
    }

    async Task<(int friends, int posts, int places)> Counts(Guid userId)
    {
        var social = await Internal.GetAsync<SocialActivity>(_http, $"{_config["Services:Social"]}/internal/users/{userId}/activity");
        var map = await Internal.GetAsync<MapActivity>(_http, $"{_config["Services:Map"]}/internal/users/{userId}/activity");
        return (social?.FriendCount ?? 0, social?.PostCount ?? 0, map?.PlacesExplored ?? 0);
    }

    async Task<ServiceResult> SaveImage(ClaimsPrincipal principal, IFormFile? file, Action<AppUser, string> apply)
    {
        var user = await Require(principal);
        if (user == null) return ServiceResult.Unauthorized();
        if (file == null) return ServiceResult.BadRequest(ApiResponse<object>.Fail("Thiếu file"));
        var url = await Uploads.Save(_config, file);
        apply(user, url);
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<string>.Ok(url));
    }
}
