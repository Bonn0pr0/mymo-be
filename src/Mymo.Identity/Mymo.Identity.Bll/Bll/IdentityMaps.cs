using System.Text.Json;
using Mymo.Identity.Data;
using Mymo.Shared;

namespace Mymo.Identity.Bll;

public record AuthBody(string? Email, string? Password, string? Username, string? DisplayName, string? Gender, string? PhoneNumber, string? DateOfBirth, string? OtpCode, string? NewPassword);
public record RefreshBody(string? RefreshToken);
public record OtpBody(string? Email);
public record IdList(List<Guid> Ids);
public record PlaceSnippet(Guid PlaceId, string Name, string? ThumbnailUrl);
public record SocialActivity(int PostCount, int PhotoPosts, int LikesReceived, int CommentsReceived, int FriendCount);
public record MapActivity(int CheckIns, int PlacesExplored);

static class IdentityMaps
{
    public static object Token(IConfiguration config, AppUser user, string refresh) => new
    {
        user.UserId,
        user.Username,
        user.Email,
        role = user.Role,
        accessToken = JwtTokens.Create(config, user.UserId, user.Email, user.Username, user.Role),
        refreshToken = refresh,
        permissions = user.Role == "Admin" ? JwtTokens.AdminPermissions : Array.Empty<string>(),
    };

    public static object Profile(AppUser user, int friends, int posts, int places) => new
    {
        id = user.UserId,
        user.UserId,
        user.Username,
        user.DisplayName,
        user.AvatarUrl,
        user.CoverUrl,
        user.Bio,
        gender = user.Gender ?? "",
        dateOfBirth = user.DateOfBirth?.ToString("yyyy-MM-dd") ?? "",
        user.Email,
        phoneNumber = user.PhoneNumber ?? "",
        friendCount = friends,
        postCount = posts,
        placeCount = places,
        user.CreatedAt,
        user.MapMarker,
        user.IsPremium,
        user.AvatarFrame,
        user.ProfileTheme,
        user.ProfileTitle,
        favoritePlaces = JsonSerializer.Deserialize<List<PlaceSnippet>>(string.IsNullOrWhiteSpace(user.FavoritePlacesJson) ? "[]" : user.FavoritePlacesJson, Internal.JsonOptions) ?? [],
        user.IsLocationSharing,
        user.IsAnonymous,
        user.Role,
    };

    public static UserCard Card(AppUser user) =>
        new(user.UserId, user.Username, user.DisplayName, user.AvatarUrl, user.IsOnline, user.IsPremium, user.Role);
}
