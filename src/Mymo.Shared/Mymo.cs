using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Mymo.Shared.Messaging;

namespace Mymo.Shared;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = "Success";
    public T? Data { get; set; }
    public string? ErrorCode { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "Success") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, string? errorCode = null) =>
        new() { Success = false, Message = message, ErrorCode = errorCode };
}

public record UserCard(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    bool IsOnline,
    bool IsPremium,
    string Role);

public static class Ids
{
    public static readonly Guid Admin = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Demo = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid BusinessUser = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid Business = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid CafeCategory = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid PlaceNguyenHue = Guid.Parse("66666666-6666-6666-6666-666666666666");
    public static readonly Guid PlaceBookStreet = Guid.Parse("77777777-7777-7777-7777-777777777777");
    public static readonly Guid DemoPost = Guid.Parse("88888888-8888-8888-8888-888888888888");
}

public static class Geo
{
    public static double Km(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}

public static class HttpUser
{
    public static Guid? Id(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    public static string? Name(ClaimsPrincipal user) =>
        user.FindFirstValue("unique_name") ?? user.FindFirstValue("name");
}

public static class JwtTokens
{
    public static readonly string[] AdminPermissions =
    [
        "admin.portal",
        "admin.users.manage",
        "admin.actors.manage",
        "admin.reports.manage",
        "admin.settings.manage",
        "admin.billing.manage",
    ];

    public static string Create(IConfiguration config, Guid userId, string email, string username, string role)
    {
        var secret = config["Jwt:Secret"] ?? throw new InvalidOperationException("Missing Jwt:Secret");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new("unique_name", username),
            new("role", role),
        };
        if (role == "Admin")
        {
            foreach (var permission in AdminPermissions)
                claims.Add(new Claim("perm", permission));
        }

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public static class Internal
{
    public static bool Allowed(HttpRequest request, IConfiguration config)
    {
        var expected = config["Internal:Key"] ?? "mymo-local";
        return request.Headers["X-Internal-Key"] == expected;
    }

    public static async Task<T?> GetAsync<T>(IHttpClientFactory factory, string url, CancellationToken ct = default)
    {
        try
        {
            var client = factory.CreateClient("internal");
            using var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return default;
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch
        {
            return default;
        }
    }

    public static async Task<T?> PostAsync<T>(IHttpClientFactory factory, string url, object body, CancellationToken ct = default)
    {
        try
        {
            var client = factory.CreateClient("internal");
            using var response = await client.PostAsJsonAsync(url, body, JsonOptions, ct);
            if (!response.IsSuccessStatusCode) return default;
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch
        {
            return default;
        }
    }

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

public static class MymoService
{
    public static void Add(IServiceCollection services, IConfiguration config, string serviceName = "Mymo API")
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        var secret = config["Jwt:Secret"] ?? "mymo-dev-secret-key-change-me-32b!";
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
        JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = config["Jwt:Issuer"],
                    ValidAudience = config["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    RoleClaimType = "role",
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };
            });
        services.AddAuthorization();
        services.AddHttpClient("internal", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(4);
            client.DefaultRequestHeaders.Add("X-Internal-Key", config["Internal:Key"] ?? "mymo-local");
        });
        services.AddMymoMessaging(config);

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = serviceName,
                Version = "v1",
                Description = $"Swagger API documentation for {serviceName}"
            });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Format: Bearer {token}",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT"
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });
    }

    public static void Use(WebApplication app, string serviceName = "Mymo API")
    {
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{serviceName} v1");
            c.RoutePrefix = "swagger";
        });
        app.MapGet("/health", () => Results.Ok(new { ok = true }));
    }
}

public static class JsonBody
{
    public static string? Text(JsonElement body, string name)
    {
        if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        return value.ToString();
    }

    public static double? Num(JsonElement body, string name)
    {
        if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number)
            return null;
        return value.GetDouble();
    }
}

public sealed class ServiceResult
{
    public int Status { get; init; } = StatusCodes.Status200OK;
    public object? Body { get; init; }
    public bool NoBody { get; init; }
    public string? Raw { get; init; }

    public IResult ToHttp()
    {
        if (Raw != null) return Results.Content(Raw, "application/json", statusCode: Status);
        if (Status == StatusCodes.Status204NoContent) return Results.NoContent();
        if (Status == StatusCodes.Status403Forbidden) return Results.Forbid();
        if (NoBody && Status == StatusCodes.Status200OK) return Results.Ok();
        if (Body == null && Status == StatusCodes.Status401Unauthorized) return Results.Unauthorized();
        if (Body == null && Status == StatusCodes.Status404NotFound) return Results.NotFound();
        return Status switch
        {
            StatusCodes.Status200OK => Results.Ok(Body),
            StatusCodes.Status400BadRequest => Results.BadRequest(Body),
            StatusCodes.Status401Unauthorized => Results.Json(Body, statusCode: StatusCodes.Status401Unauthorized),
            StatusCodes.Status404NotFound => Results.NotFound(Body),
            _ => Results.Json(Body, statusCode: Status),
        };
    }

    public static ServiceResult Ok(object? body) => new() { Body = body };
    public static ServiceResult Empty() => new() { NoBody = true };
    public static ServiceResult NoContent() => new() { Status = StatusCodes.Status204NoContent };
    public static ServiceResult BadRequest(object body) => new() { Status = StatusCodes.Status400BadRequest, Body = body };
    public static ServiceResult Unauthorized(object? body = null) => new() { Status = StatusCodes.Status401Unauthorized, Body = body };
    public static ServiceResult Forbidden() => new() { Status = StatusCodes.Status403Forbidden };
    public static ServiceResult NotFound(object? body = null) => new() { Status = StatusCodes.Status404NotFound, Body = body };
    public static ServiceResult RawJson(string json, int status) => new() { Status = status, Raw = json };
}
