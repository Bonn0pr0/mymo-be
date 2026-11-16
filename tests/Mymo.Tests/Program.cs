using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mymo.Business.Bll;
using Mymo.Business.Dal;
using Mymo.Business.Data;
using Mymo.Identity.Bll;
using Mymo.Identity.Dal;
using Mymo.Identity.Data;
using Mymo.Map.Bll;
using Mymo.Map.Dal;
using Mymo.Map.Data;
using Mymo.Shared;
using Mymo.Shared.Messaging;

int passed = 0;
int failed = 0;

async Task RunTest(string name, Func<Task> test)
{
    try
    {
        await test();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  [PASS] {name}");
        Console.ResetColor();
        passed++;
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  [FAIL] {name}: {ex.Message}");
        Console.ResetColor();
        failed++;
    }
}

void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

Console.WriteLine("=== Running MyMo Unit & Business Logic Tests ===");

// 1. Geo Haversine Distance Test
await RunTest("Geo.Km calculates accurate distance between Nguyen Hue and Book Street", () =>
{
    var km = Geo.Km(10.7735, 106.7034, 10.7798, 106.6995);
    Assert(km > 0.5 && km < 1.2, $"Expected distance around 0.82km, got {km:F2}km");
    Assert(Geo.Km(10.7735, 106.7034, 10.7735, 106.7034) == 0, "Distance to same point should be 0");
    return Task.CompletedTask;
});

// 2. ApiResponse & ServiceResult Test
await RunTest("ApiResponse<T> and ServiceResult format standard payloads", () =>
{
    var ok = ApiResponse<string>.Ok("hello", "Done");
    Assert(ok.Success && ok.Data == "hello" && ok.Message == "Done", "ApiResponse.Ok mismatch");

    var fail = ApiResponse<string>.Fail("Bad input", "ERR_INPUT");
    Assert(!fail.Success && fail.ErrorCode == "ERR_INPUT", "ApiResponse.Fail mismatch");

    var res = ServiceResult.BadRequest(fail);
    Assert(res.Status == 400 && res.Body != null, "ServiceResult.BadRequest status mismatch");
    return Task.CompletedTask;
});

// 3. JWT Token Generation & Admin Claims Test
await RunTest("JwtTokens.Create embeds User role and Admin permissions", () =>
{
    var config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "mymo-dev-secret-key-change-me-32b!",
            ["Jwt:Issuer"] = "MYMO",
            ["Jwt:Audience"] = "MYMO",
        })
        .Build();

    var tokenString = JwtTokens.Create(config, Ids.Admin, "admin@mymo.app", "admin", "Admin");
    var handler = new JwtSecurityTokenHandler();
    var jwt = handler.ReadJwtToken(tokenString);

    Assert(jwt.Issuer == "MYMO", "JWT Issuer mismatch");
    Assert(jwt.Claims.Any(c => c.Type == "role" && c.Value == "Admin"), "Missing Admin role claim");
    Assert(jwt.Claims.Count(c => c.Type == "perm") == JwtTokens.AdminPermissions.Length, "Admin permissions count mismatch");
    return Task.CompletedTask;
});

// 4. Identity BLL (AuthService OTP + Register + Login) with SQLite In-Memory DB
await RunTest("AuthService handles OTP, Registration, and Login workflow", async () =>
{
    await using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
    await conn.OpenAsync();
    var options = new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(conn).Options;
    await using var db = new IdentityDbContext(options);
    await IdentitySeed.Run(db);

    var config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "mymo-dev-secret-key-change-me-32b!",
            ["Jwt:Issuer"] = "MYMO",
            ["Jwt:Audience"] = "MYMO",
        })
        .Build();

    var repo = new IdentityRepository(db);
    var auth = new AuthService(repo, config);

    var loginDemo = await auth.Login(new AuthBody("demo@mymo.app", "Demo123!", null, null, null, null, null, null, null));
    Assert(loginDemo.Status == 200, "Seeded demo user should login with status 200");

    var badLogin = await auth.Login(new AuthBody("demo@mymo.app", "WrongPass", null, null, null, null, null, null, null));
    Assert(badLogin.Status == 401, "Wrong password should return 401 Unauthorized");
});

// 5. Map BLL (Nearby Places & Vibe Filtering) with SQLite In-Memory DB
await RunTest("MapService filters nearby places by radius and vibe", async () =>
{
    await using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
    await conn.OpenAsync();
    var options = new DbContextOptionsBuilder<MapDbContext>().UseSqlite(conn).Options;
    await using var db = new MapDbContext(options);
    await MapSeed.Run(db);

    var config = new ConfigurationBuilder().Build();
    var repo = new MapRepository(db);
    var map = new MapService(repo, config, new DummyHttpFactory(), new DummyMapRealtime());

    var nearby = await map.Nearby(new System.Security.Claims.ClaimsPrincipal(), 10.7735, 106.7034, 5, "study");
    Assert(nearby.Status == 200, "MapService.Nearby should return 200 OK");
    var json = JsonSerializer.Serialize(nearby.Body, Internal.JsonOptions);
    Assert(json.Contains("Momo Book Street") && !json.Contains("Momo Nguyen Hue"), "Vibe filter 'study' should match Momo Book Street only");
});

// 6. Business BLL (Catalog & Campaign Creation) with SQLite In-Memory DB
await RunTest("BusinessService seeds B2C/B2B catalog packages and creates campaigns", async () =>
{
    await using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
    await conn.OpenAsync();
    var options = new DbContextOptionsBuilder<BusinessDbContext>().UseSqlite(conn).Options;
    await using var db = new BusinessDbContext(options);
    await BusinessSeed.Run(db);

    var config = new ConfigurationBuilder().Build();
    var repo = new BusinessRepository(db);
    var biz = new BusinessService(repo, config, new DummyHttpFactory(), new NoopCommandBus());

    var catalog = await biz.Catalog();
    Assert(catalog.Status == 200, "Catalog should return 200 OK");
    var json = JsonSerializer.Serialize(catalog.Body, Internal.JsonOptions);
    Assert(json.Contains("monthly") && json.Contains("featured_venue"), "Catalog should include B2C monthly and B2B featured_venue packages");
});

Console.WriteLine($"=== Summary: {passed} Passed, {failed} Failed ===");
return failed == 0 ? 0 : 1;

sealed class DummyHttpFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new();
}

sealed class DummyMapRealtime : IMapRealtime
{
    public Task MapPostCreated(object payload) => Task.CompletedTask;
}

sealed class NoopCommandBus : ICommandBus
{
    public Task PublishAsync<T>(string queue, T message, CancellationToken ct = default) => Task.CompletedTask;
}

