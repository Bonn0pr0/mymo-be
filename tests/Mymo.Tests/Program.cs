using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mymo.Business.Bll;
using Mymo.Business.Dal;
using Mymo.Business.Data;
using Mymo.Chat.Bll;
using Mymo.Chat.Dal;
using Mymo.Chat.Data;
using Mymo.Identity.Bll;
using Mymo.Identity.Dal;
using Mymo.Identity.Data;
using Mymo.Map.Bll;
using Mymo.Map.Dal;
using Mymo.Map.Data;
using Mymo.Shared;
using Mymo.Shared.Messaging;
using Mymo.Social.Bll;
using Mymo.Social.Dal;
using Mymo.Social.Data;

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

static ClaimsPrincipal FakeUser(Guid userId, string username = "demo", string role = "User") =>
    new(new ClaimsIdentity(
    [
        new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
        new Claim("unique_name", username),
        new Claim("role", role),
    ], "TestAuth"));

static JsonElement ParseJson(object obj) =>
    JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(obj, Internal.JsonOptions));

Console.WriteLine("=== Running MyMo Complete Unit & 5-Microservice BLL Test Suite (12 Tests) ===");

// 1. [Shared] Geo Haversine Distance Test
await RunTest("01. [Shared] Geo.Km calculates accurate distance between Nguyen Hue and Book Street", () =>
{
    var km = Geo.Km(10.7735, 106.7034, 10.7798, 106.6995);
    Assert(km > 0.5 && km < 1.2, $"Expected distance around 0.82km, got {km:F2}km");
    Assert(Geo.Km(10.7735, 106.7034, 10.7735, 106.7034) == 0, "Distance to same point should be 0");
    return Task.CompletedTask;
});

// 2. [Shared] ApiResponse & ServiceResult Test
await RunTest("02. [Shared] ApiResponse<T> and ServiceResult format standard HTTP payloads", () =>
{
    var ok = ApiResponse<string>.Ok("hello", "Done");
    Assert(ok.Success && ok.Data == "hello" && ok.Message == "Done", "ApiResponse.Ok mismatch");

    var fail = ApiResponse<string>.Fail("Bad input", "ERR_INPUT");
    Assert(!fail.Success && fail.ErrorCode == "ERR_INPUT", "ApiResponse.Fail mismatch");

    var res = ServiceResult.BadRequest(fail);
    Assert(res.Status == 400 && res.Body != null, "ServiceResult.BadRequest status mismatch");
    return Task.CompletedTask;
});

// 3. [Shared] JWT Token Generation & Admin Claims Test
await RunTest("03. [Shared] JwtTokens.Create embeds User role and Admin permissions", () =>
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

// 4. [Identity] AuthService OTP, Registration, and Login Workflow
await RunTest("04. [Identity] AuthService handles OTP challenge, Registration, and Login", async () =>
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

    var otpRes = await auth.SendOtp(new OtpBody("newuser@mymo.app"), "register");
    Assert(otpRes.Status == 200, "SendOtp should succeed");
    var otpJson = ParseJson(otpRes.Body!);
    var devCode = otpJson.GetProperty("devCode").GetString()!;

    var regRes = await auth.Register(new AuthBody("newuser@mymo.app", "NewPass123!", "newuser", "New User", "Male", "0909123456", "2003-05-10", devCode, null));
    Assert(regRes.Status == 200, "Register with valid OTP should return 200 OK");
});

// 5. [Identity] AdminService Dashboard, Reports, and Audit Logs
await RunTest("05. [Identity] AdminService processes reports, user status updates, and audit logs", async () =>
{
    await using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
    await conn.OpenAsync();
    var options = new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(conn).Options;
    await using var db = new IdentityDbContext(options);
    await IdentitySeed.Run(db);

    var repo = new IdentityRepository(db);
    var admin = new AdminService(repo);

    await admin.CreateReport(new CreateReportCommand(Ids.Demo, "Post", Ids.DemoPost, "Spam content", "Caption summary"));
    var reports = await admin.Reports();
    Assert(reports.Status == 200, "AdminService.Reports should return 200 OK");
    var reportJson = JsonSerializer.Serialize(reports.Body, Internal.JsonOptions);
    Assert(reportJson.Contains("Spam content"), "Created report should appear in Admin reports list");

    var setStatus = await admin.SetStatus(Ids.Demo, ParseJson(new { status = "Banned" }));
    Assert(setStatus.Status == 200, "AdminService.SetStatus should succeed");

    var audits = await admin.AuditLogs();
    var auditJson = JsonSerializer.Serialize(audits.Body, Internal.JsonOptions);
    Assert(auditJson.Contains("Banned"), "Audit log should record user status change to Banned");
});

// 6. [Social] PostService Create, Like, View, and Kafka Event Publishing
await RunTest("06. [Social] PostService creates posts, records likes/views, and emits PostCreatedEvent", async () =>
{
    await using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
    await conn.OpenAsync();
    var options = new DbContextOptionsBuilder<SocialDbContext>().UseSqlite(conn).Options;
    await using var db = new SocialDbContext(options);
    await SocialSeed.Run(db);

    var config = new ConfigurationBuilder().Build();
    var repo = new SocialRepository(db);
    var cmdBus = new FakeCommandBus();
    var evtBus = new FakeEventBus();
    var postService = new PostService(repo, config, new DummyHttpFactory(), cmdBus, evtBus);

    var createRes = await postService.Create(FakeUser(Ids.Demo), ParseJson(new
    {
        caption = "Check-in buổi tối ở Sài Gòn",
        postType = "Photo",
        visibility = "Public",
        latitude = 10.775,
        longitude = 106.702,
    }));
    Assert(createRes.Status == 200, "PostService.Create should return 200 OK");
    Assert(evtBus.PublishedTopics.Contains(Topics.PostCreated), "PostService.Create should publish PostCreatedEvent to Kafka");

    var likeRes = await postService.Like(Ids.DemoPost, FakeUser(Ids.BusinessUser));
    Assert(likeRes.Status == 200, "PostService.Like should succeed");
    var detailRes = await postService.Detail(Ids.DemoPost, FakeUser(Ids.BusinessUser));
    var detailJson = JsonSerializer.Serialize(detailRes.Body, Internal.JsonOptions);
    Assert(detailJson.Contains("\"likeCount\":1") && detailJson.Contains("\"isLiked\":true"), "Post detail should reflect updated likeCount and isLiked");
});

// 7. [Social] PostService Nested Comments, Report Command, and Hana AI
await RunTest("07. [Social] PostService supports nested comment replies, post reporting, and Hana AI", async () =>
{
    await using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
    await conn.OpenAsync();
    var options = new DbContextOptionsBuilder<SocialDbContext>().UseSqlite(conn).Options;
    await using var db = new SocialDbContext(options);
    await SocialSeed.Run(db);

    var config = new ConfigurationBuilder().Build();
    var repo = new SocialRepository(db);
    var cmdBus = new FakeCommandBus();
    var evtBus = new FakeEventBus();
    var postService = new PostService(repo, config, new DummyHttpFactory(), cmdBus, evtBus);

    var commentRes = await postService.AddComment(Ids.DemoPost, FakeUser(Ids.Demo), ParseJson(new { content = "Quán đẹp quá!" }));
    Assert(commentRes.Status == 200, "AddComment should succeed");
    var commentId = ParseJson(commentRes.Body!).GetProperty("data").GetProperty("commentId").GetGuid();

    var replyRes = await postService.Reply(commentId, FakeUser(Ids.BusinessUser), ParseJson(new { content = "Cảm ơn bạn nhé!" }));
    Assert(replyRes.Status == 200, "Reply to comment should succeed");

    var reportRes = await postService.Report(Ids.DemoPost, FakeUser(Ids.BusinessUser), ParseJson(new { reason = "Inappropriate" }));
    Assert(reportRes.Status == 200 && cmdBus.PublishedQueues.Contains(Queues.CreateReport), "Report should publish CreateReportCommand to RabbitMQ");

    var hanaRes = postService.Hana(ParseJson(new { message = "Gợi ý quán học bài" }));
    Assert(hanaRes.Status == 200, "Hana AI should respond with status 200");
});

// 8. [Social] FriendService & NotificationService Workflow
await RunTest("08. [Social] FriendService and NotificationService manage friendships and notifications", async () =>
{
    await using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
    await conn.OpenAsync();
    var options = new DbContextOptionsBuilder<SocialDbContext>().UseSqlite(conn).Options;
    await using var db = new SocialDbContext(options);
    await SocialSeed.Run(db);

    var config = new ConfigurationBuilder().Build();
    var repo = new SocialRepository(db);
    var friends = new FriendService(repo, config, new DummyHttpFactory());
    var notes = new NotificationService(repo);

    var reqRes = await friends.Request(FakeUser(Ids.Demo), ParseJson(new { userId = Ids.BusinessUser.ToString() }));
    Assert(reqRes.Status == 200, "Friend request should succeed");

    var unreadBefore = await notes.Unread(FakeUser(Ids.BusinessUser));
    Assert(ParseJson(unreadBefore.Body!).GetProperty("data").GetInt32() == 1, "Receiver should have 1 unread FriendRequest notification");

    var acceptRes = await friends.Accept(Ids.Demo, FakeUser(Ids.BusinessUser));
    Assert(acceptRes.Status == 200, "Accepting friend request should succeed");

    var countRes = await friends.Count(FakeUser(Ids.Demo));
    Assert(ParseJson(countRes.Body!).GetProperty("data").GetInt32() == 1, "Friend count should be 1 after acceptance");

    await notes.ReadAll(FakeUser(Ids.BusinessUser));
    var unreadAfter = await notes.Unread(FakeUser(Ids.BusinessUser));
    Assert(ParseJson(unreadAfter.Body!).GetProperty("data").GetInt32() == 0, "Unread count should be 0 after ReadAll");
});

// 9. [Map] MapService Nearby Places & Vibe Filtering
await RunTest("09. [Map] MapService filters nearby places by radius and vibe", async () =>
{
    await using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
    await conn.OpenAsync();
    var options = new DbContextOptionsBuilder<MapDbContext>().UseSqlite(conn).Options;
    await using var db = new MapDbContext(options);
    await MapSeed.Run(db);

    var config = new ConfigurationBuilder().Build();
    var repo = new MapRepository(db);
    var map = new MapService(repo, config, new DummyHttpFactory(), new DummyMapRealtime());

    var nearby = await map.Nearby(FakeUser(Ids.Demo), 10.7735, 106.7034, 5, "study");
    Assert(nearby.Status == 200, "MapService.Nearby should return 200 OK");
    var json = JsonSerializer.Serialize(nearby.Body, Internal.JsonOptions);
    Assert(json.Contains("Momo Book Street") && !json.Contains("Momo Nguyen Hue"), "Vibe filter 'study' should match Momo Book Street only");
});

// 10. [Map] MapService Check-In, Reviews, and Ad Package Command
await RunTest("10. [Map] MapService records check-ins, recalculates review ratings, and applies Ad packages", async () =>
{
    await using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
    await conn.OpenAsync();
    var options = new DbContextOptionsBuilder<MapDbContext>().UseSqlite(conn).Options;
    await using var db = new MapDbContext(options);
    await MapSeed.Run(db);

    var config = new ConfigurationBuilder().Build();
    var repo = new MapRepository(db);
    var map = new MapService(repo, config, new DummyHttpFactory(), new DummyMapRealtime());

    var checkInRes = await map.CheckIn(FakeUser(Ids.Demo), ParseJson(new
    {
        placeId = Ids.PlaceNguyenHue.ToString(),
        mood = "Happy",
        weather = "Sunny",
    }));
    Assert(checkInRes.Status == 200, "CheckIn should return 200 OK");

    await map.AddReview(Ids.PlaceNguyenHue, FakeUser(Ids.Demo), ParseJson(new { rating = 5, title = "Tuyệt vời", content = "Cà phê ngon" }));
    await map.AddReview(Ids.PlaceNguyenHue, FakeUser(Ids.BusinessUser), ParseJson(new { rating = 3, title = "Ổn", content = "Hơi đông" }));

    await map.ApplyPlaceAd(new UpdatePlaceAdCommand(Ids.PlaceNguyenHue, "featured_venue", "Approved"));

    var detail = await map.Detail(Ids.PlaceNguyenHue);
    var detailJson = JsonSerializer.Serialize(detail.Body, Internal.JsonOptions);
    Assert(detailJson.Contains("\"averageRating\":5") && detailJson.Contains("\"checkInCount\":1"), "Place detail should have averageRating=5 and checkInCount=1");
});

// 11. [Chat] ChatService Private Conversation, Send Message, and Delete Message
await RunTest("11. [Chat] ChatService opens private chats, sends messages, and broadcasts via IChatRealtime", async () =>
{
    await using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
    await conn.OpenAsync();
    var options = new DbContextOptionsBuilder<ChatDbContext>().UseSqlite(conn).Options;
    await using var db = new ChatDbContext(options);
    await ChatSeed.Run(db);

    var config = new ConfigurationBuilder().Build();
    var repo = new ChatRepository(db);
    var realtime = new FakeChatRealtime();
    var chat = new ChatService(repo, config, new DummyHttpFactory(), realtime);

    var openRes = await chat.OpenPrivate(FakeUser(Ids.Demo), ParseJson(new { userId = Ids.BusinessUser.ToString() }));
    Assert(openRes.Status == 200, "OpenPrivate should return 200 OK");
    var convId = ParseJson(openRes.Body!).GetProperty("data").GetProperty("conversationId").GetGuid();

    var sendRes = await chat.Send(convId, FakeUser(Ids.Demo), ParseJson(new { content = "Chào quán Cafe Momo!", messageType = 0 }));
    Assert(sendRes.Status == 200 && realtime.ReceivedCount == 1, "Send message should persist and broadcast ReceiveMessage via SignalR");
    var msgId = ParseJson(sendRes.Body!).GetProperty("data").GetProperty("messageId").GetGuid();

    var delRes = await chat.Delete(msgId, FakeUser(Ids.Demo));
    Assert(delRes.Status == 200 && realtime.DeletedCount == 1, "Delete message should soft-delete and broadcast MessageDeleted");
});

// 12. [Business] BusinessService Catalog, Premium Order, Ad Campaign & RabbitMQ Commands
await RunTest("12. [Business] BusinessService manages Menu, Premium orders, Ad campaigns, and RabbitMQ commands", async () =>
{
    await using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
    await conn.OpenAsync();
    var options = new DbContextOptionsBuilder<BusinessDbContext>().UseSqlite(conn).Options;
    await using var db = new BusinessDbContext(options);
    await BusinessSeed.Run(db);

    var config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bank:BankName"] = "VietinBank",
            ["Bank:BankBin"] = "970415",
            ["Bank:AccountNumber"] = "0000000000",
            ["Bank:AccountName"] = "MYMO LOCAL",
        })
        .Build();
    var repo = new BusinessRepository(db);
    var cmdBus = new FakeCommandBus();
    var biz = new BusinessService(repo, config, new DummyHttpFactory(), cmdBus);

    var orderRes = await biz.CreatePremium(FakeUser(Ids.Demo), ParseJson(new { planId = "monthly" }));
    Assert(orderRes.Status == 200, "CreatePremium should return 200 OK");
    var orderId = ParseJson(orderRes.Body!).GetProperty("data").GetProperty("orderId").GetGuid();

    var confirmRes = await biz.ConfirmPremium(orderId);
    Assert(confirmRes.Status == 200 && cmdBus.PublishedQueues.Contains(Queues.SetUserPremium), "ConfirmPremium should publish SetUserPremiumCommand");

    var campRes = await biz.CreateCampaign(FakeUser(Ids.BusinessUser), ParseJson(new
    {
        placeId = Ids.PlaceNguyenHue.ToString(),
        packageId = "featured_venue",
    }));
    Assert(campRes.Status == 200, "CreateCampaign should return 200 OK");
    var campaignId = ParseJson(campRes.Body!).GetProperty("data").GetProperty("campaignId").GetGuid();

    var payCampRes = await biz.PayCampaign(campaignId);
    Assert(payCampRes.Status == 200 && cmdBus.PublishedQueues.Contains(Queues.UpdatePlaceAd), "PayCampaign should publish UpdatePlaceAdCommand");
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

sealed class FakeChatRealtime : IChatRealtime
{
    public int ReceivedCount { get; private set; }
    public int DeletedCount { get; private set; }

    public Task ReceiveMessage(Guid conversationId, object dto)
    {
        ReceivedCount++;
        return Task.CompletedTask;
    }

    public Task MessageDeleted(Guid conversationId, object dto)
    {
        DeletedCount++;
        return Task.CompletedTask;
    }
}

sealed class FakeCommandBus : ICommandBus
{
    public List<string> PublishedQueues { get; } = [];

    public Task PublishAsync<T>(string queue, T message, CancellationToken ct = default)
    {
        PublishedQueues.Add(queue);
        return Task.CompletedTask;
    }
}

sealed class FakeEventBus : IEventBus
{
    public List<string> PublishedTopics { get; } = [];

    public Task PublishAsync<T>(string topic, T message, CancellationToken ct = default)
    {
        PublishedTopics.Add(topic);
        return Task.CompletedTask;
    }
}
