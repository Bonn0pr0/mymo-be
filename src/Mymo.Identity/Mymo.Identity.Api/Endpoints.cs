using System.Text.Json;
using Mymo.Identity.Bll;
using Mymo.Shared;

namespace Mymo.Identity;

public static class IdentityEndpoints
{
    public static void Map(WebApplication app)
    {
        var auth = app.MapGroup("/api/Auth");
        auth.MapPost("/register", async (AuthService auth, AuthBody body) => (await auth.Register(body)).ToHttp());
        auth.MapPost("/login", async (AuthService auth, AuthBody body) => (await auth.Login(body)).ToHttp());
        auth.MapPost("/send-register-otp", async (AuthService auth, OtpBody body) => (await auth.SendOtp(body, "register")).ToHttp());
        auth.MapPost("/send-reset-otp", async (AuthService auth, OtpBody body) => (await auth.SendOtp(body, "reset")).ToHttp());
        auth.MapPost("/reset-password", async (AuthService auth, AuthBody body) => (await auth.ResetPassword(body)).ToHttp());
        auth.MapPost("/refresh-token", async (AuthService auth, RefreshBody body) => (await auth.Refresh(body)).ToHttp());
        auth.MapPost("/logout", async (AuthService auth, RefreshBody body) => (await auth.Logout(body)).ToHttp());
        auth.MapGet("/me", async (ClaimsPrincipal principal, AuthService auth) => (await auth.Me(principal)).ToHttp()).RequireAuthorization();

        var users = app.MapGroup("/api/users").RequireAuthorization();
        users.MapGet("/me", async (ClaimsPrincipal principal, UserService users) => (await users.Me(principal)).ToHttp());
        users.MapPut("/me", async (ClaimsPrincipal principal, UserService users, JsonElement body) => (await users.UpdateMe(principal, body)).ToHttp());
        users.MapPut("/change-password", async (ClaimsPrincipal principal, UserService users, JsonElement body) => (await users.ChangePassword(principal, body)).ToHttp());
        users.MapPatch("/settings", async (ClaimsPrincipal principal, UserService users, JsonElement body) => (await users.Settings(principal, body)).ToHttp());
        users.MapPut("/location", async (ClaimsPrincipal principal, UserService users, JsonElement body) => (await users.Location(principal, body)).ToHttp());
        users.MapPut("/me/style", async (ClaimsPrincipal principal, UserService users, JsonElement body) => (await users.Style(principal, body)).ToHttp());
        users.MapPut("/me/favorite-places", async (ClaimsPrincipal principal, UserService users, JsonElement body) => (await users.Favorites(principal, body)).ToHttp());
        users.MapGet("/me/visited-places", async (ClaimsPrincipal principal, UserService users) => (await users.Visited(principal)).ToHttp());
        users.MapGet("/me/stats", async (ClaimsPrincipal principal, UserService users) => (await users.Stats(principal)).ToHttp());
        users.MapGet("/nearby/online", async (ClaimsPrincipal principal, UserService users, double? latitude, double? longitude, double? radiusKm) => (await users.NearbyOnline(principal, latitude, longitude, radiusKm)).ToHttp());
        users.MapPut("/avatar", async (ClaimsPrincipal principal, UserService users, HttpRequest request) => (await users.UploadAvatar(principal, request.Form.Files.FirstOrDefault())).ToHttp());
        users.MapPut("/cover", async (ClaimsPrincipal principal, UserService users, HttpRequest request) => (await users.UploadCover(principal, request.Form.Files.FirstOrDefault())).ToHttp());
        users.MapPut("/map-marker", async (ClaimsPrincipal principal, UserService users, JsonElement body) => (await users.MapMarker(principal, body)).ToHttp());
        users.MapPut("/map-marker/image", async (ClaimsPrincipal principal, UserService users, HttpRequest request) => (await users.UploadMarker(principal, request.Form.Files.FirstOrDefault())).ToHttp());
        users.MapGet("/{id:guid}", async (Guid id, UserService users) => (await users.PublicProfile(id)).ToHttp());
        users.MapGet("/{id:guid}/posts", async (Guid id, HttpContext ctx, UserService users) => (await users.ProxyUserPosts(id, ctx.Request.Headers.Authorization.ToString())).ToHttp());

        var admin = app.MapGroup("/api/admin").RequireAuthorization(p => p.RequireRole("Admin"));
        admin.MapGet("/dashboard", async (AdminService admin) => (await admin.Dashboard()).ToHttp());
        admin.MapGet("/users", async (AdminService admin, string? keyword, int page = 1, int pageSize = 20) => (await admin.Users(keyword, page, pageSize)).ToHttp());
        admin.MapPut("/users/{id:guid}/status", async (Guid id, AdminService admin, JsonElement body) => (await admin.SetStatus(id, body)).ToHttp());
        admin.MapGet("/reports", async (AdminService admin) => (await admin.Reports()).ToHttp());
        admin.MapPut("/reports/{id:guid}/status", async (Guid id, AdminService admin, JsonElement body) => (await admin.SetReportStatus(id, body)).ToHttp());
        admin.MapGet("/audit-logs", async (AdminService admin) => (await admin.AuditLogs()).ToHttp());
        admin.MapGet("/settings/overview", (AdminService admin) => admin.SettingsOverview().ToHttp());
        admin.MapPut("/settings/bank-transfer", (AdminService admin) => admin.SaveBank().ToHttp());

        var internalApi = app.MapGroup("/internal");
        internalApi.MapGet("/users/{id:guid}", async (Guid id, HttpRequest request, IConfiguration config, UserService users) => Gate(request, config, () => users.InternalUser(id)));
        internalApi.MapPost("/users/batch", async (HttpRequest request, IConfiguration config, UserService users, IdList body) => Gate(request, config, () => users.InternalBatch(body)));
        internalApi.MapGet("/users/search", async (HttpRequest request, IConfiguration config, UserService users, string? keyword) => Gate(request, config, () => users.InternalSearch(keyword)));
        internalApi.MapPost("/users/{id:guid}/presence", async (Guid id, HttpRequest request, IConfiguration config, UserService users, JsonElement body) => Gate(request, config, () => users.Presence(id, body)));
        MapUpload(app);
    }

    static async Task<IResult> Gate(HttpRequest request, IConfiguration config, Func<Task<ServiceResult>> action)
    {
        if (!Internal.Allowed(request, config)) return Results.Unauthorized();
        return (await action()).ToHttp();
    }

    static void MapUpload(WebApplication app)
    {
        app.MapPost("/api/upload", async (IFormFile file, CloudinaryService cloudinary) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(ApiResponse<object>.Fail("Không có file"));
            await using var stream = file.OpenReadStream();
            var result = await cloudinary.UploadAsync(stream, file.FileName, folder: "mymo/identity");
            return result.Success
                ? Results.Ok(ApiResponse<object>.Ok(new { url = result.Url, publicId = result.PublicId }))
                : Results.BadRequest(ApiResponse<object>.Fail(result.Error ?? "Upload thất bại"));
        }).RequireAuthorization().DisableAntiforgery();
    }
}
