using System.Text.Json;
using Mymo.Map.Bll;
using Mymo.Shared;

namespace Mymo.Map;

public static class MapEndpoints
{
    public static void Map(WebApplication app)
    {
        var places = app.MapGroup("/api/places");
        places.MapGet("/nearby", async (MapService map, ClaimsPrincipal principal, double? latitude, double? longitude, double radiusKm = 5, string? vibe = null) => (await map.Nearby(principal, latitude, longitude, radiusKm, vibe)).ToHttp());
        places.MapGet("/{id:guid}/detail", async (Guid id, MapService map) => (await map.Detail(id)).ToHttp());
        places.MapGet("/{id:guid}/reviews", async (Guid id, MapService map) => (await map.Reviews(id)).ToHttp()).RequireAuthorization();
        places.MapPost("/{id:guid}/reviews", async (Guid id, ClaimsPrincipal principal, MapService map, JsonElement body) => (await map.AddReview(id, principal, body)).ToHttp()).RequireAuthorization();
        places.MapPost("/map-impressions", async (ClaimsPrincipal principal, MapService map, JsonElement body) => (await map.Impressions(principal, body)).ToHttp()).RequireAuthorization();

        app.MapPut("/api/reviews/{id:guid}", async (Guid id, ClaimsPrincipal principal, MapService map, JsonElement body) => (await map.EditReview(id, principal, body)).ToHttp()).RequireAuthorization();
        app.MapDelete("/api/reviews/{id:guid}", async (Guid id, ClaimsPrincipal principal, MapService map) => (await map.DeleteReview(id, principal)).ToHttp()).RequireAuthorization();

        var checkins = app.MapGroup("/api/checkins").RequireAuthorization();
        checkins.MapPost("", async (ClaimsPrincipal principal, MapService map, JsonElement body) => (await map.CheckIn(principal, body)).ToHttp());
        checkins.MapGet("/me", async (ClaimsPrincipal principal, MapService map) => (await map.MyCheckIns(principal)).ToHttp());
        checkins.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal, MapService map) => (await map.DeleteCheckIn(id, principal)).ToHttp());

        app.MapGet("/api/weather/current", async (MapService map, double lat, double lng) => (await map.Weather(lat, lng)).ToHttp()).RequireAuthorization();
        app.MapGet("/api/place-categories", async (MapService map) => (await map.Categories()).ToHttp());

        var admin = app.MapGroup("/api/admin/places").RequireAuthorization(p => p.RequireRole("Admin"));
        admin.MapGet("/pending", async (MapService map) => (await map.Pending()).ToHttp());
        admin.MapPut("/{id:guid}/approve", async (Guid id, MapService map) => (await map.SetStatus(id, "Approved")).ToHttp());
        admin.MapPut("/{id:guid}/reject", async (Guid id, MapService map) => (await map.SetStatus(id, "Rejected")).ToHttp());
        admin.MapPut("/{id:guid}/suspend", async (Guid id, MapService map) => (await map.SetStatus(id, "Suspended")).ToHttp());

        app.MapPost("/internal/places/lookup", async (HttpRequest request, IConfiguration config, MapService map, IdList body) => Gate(request, config, () => map.Lookup(body)));
        app.MapGet("/internal/places/{id:guid}", async (Guid id, HttpRequest request, IConfiguration config, MapService map) => Gate(request, config, () => map.One(id)));
        app.MapGet("/internal/places/by-business/{id:guid}", async (Guid id, HttpRequest request, IConfiguration config, MapService map) => Gate(request, config, () => map.ByBusiness(id)));
        app.MapPost("/internal/places", async (HttpRequest request, IConfiguration config, MapService map, JsonElement body) => Gate(request, config, () => map.CreatePlace(body)));
        app.MapPut("/internal/places/{id:guid}", async (Guid id, HttpRequest request, IConfiguration config, MapService map, JsonElement body) => Gate(request, config, () => map.UpdatePlace(id, body)));
        app.MapGet("/internal/users/{id:guid}/activity", async (Guid id, HttpRequest request, IConfiguration config, MapService map) => Gate(request, config, () => map.Activity(id)));
        app.MapGet("/internal/users/{id:guid}/visited", async (Guid id, HttpRequest request, IConfiguration config, MapService map) => Gate(request, config, () => map.Visited(id)));
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
            var result = await cloudinary.UploadAsync(stream, file.FileName, folder: "mymo/map");
            return result.Success
                ? Results.Ok(ApiResponse<object>.Ok(new { url = result.Url, publicId = result.PublicId }))
                : Results.BadRequest(ApiResponse<object>.Fail(result.Error ?? "Upload thất bại"));
        }).RequireAuthorization().DisableAntiforgery();
    }
}
