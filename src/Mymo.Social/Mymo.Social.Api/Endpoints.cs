using System.Text.Json;
using Mymo.Shared;
using Mymo.Social.Bll;

namespace Mymo.Social;

public static class SocialEndpoints
{
    public static void Map(WebApplication app)
    {
        var posts = app.MapGroup("/api/posts").RequireAuthorization();
        posts.MapGet("/feed", async (ClaimsPrincipal u, PostService posts) => (await posts.Feed(u, friendsOnly: false)).ToHttp());
        posts.MapGet("/feed/friends", async (ClaimsPrincipal u, PostService posts) => (await posts.Feed(u, friendsOnly: true)).ToHttp());
        posts.MapGet("/me", async (ClaimsPrincipal u, PostService posts) => (await posts.Mine(u)).ToHttp());
        posts.MapGet("/archive", async (ClaimsPrincipal u, PostService posts) => (await posts.Archive(u)).ToHttp());
        posts.MapGet("/nearby", async (PostService posts, double? lat, double? lng, double? latitude, double? longitude, double radius = 5) => (await posts.Nearby(lat, lng, latitude, longitude, radius)).ToHttp());
        posts.MapGet("/nearby/heatmap", async (PostService posts, double lat, double lng, double radius = 25) => (await posts.Heat(lat, lng, radius)).ToHttp());
        posts.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal u, PostService posts) => (await posts.Detail(id, u)).ToHttp());
        posts.MapPost("", async (ClaimsPrincipal u, PostService posts, JsonElement body) => (await posts.Create(u, body)).ToHttp());
        posts.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal u, PostService posts) => (await posts.DeletePost(id, u)).ToHttp());
        posts.MapPost("/{id:guid}/like", async (Guid id, ClaimsPrincipal u, PostService posts) => (await posts.Like(id, u)).ToHttp());
        posts.MapDelete("/{id:guid}/like", async (Guid id, ClaimsPrincipal u, PostService posts) => (await posts.Unlike(id, u)).ToHttp());
        posts.MapPost("/{id:guid}/view", async (Guid id, ClaimsPrincipal u, PostService posts) => (await posts.View(id, u)).ToHttp());
        posts.MapGet("/{id:guid}/viewers", async (Guid id, PostService posts) => (await posts.Viewers(id)).ToHttp());
        posts.MapPost("/{id:guid}/report", async (Guid id, ClaimsPrincipal u, PostService posts, JsonElement body) => (await posts.Report(id, u, body)).ToHttp());
        posts.MapGet("/{id:guid}/comments", async (Guid id, PostService posts) => (await posts.Comments(id)).ToHttp());
        posts.MapPost("/{id:guid}/comments", async (Guid id, ClaimsPrincipal u, PostService posts, JsonElement body) => (await posts.AddComment(id, u, body)).ToHttp());

        app.MapPost("/api/comments/{id:guid}/reply", async (Guid id, ClaimsPrincipal u, PostService posts, JsonElement body) => (await posts.Reply(id, u, body)).ToHttp()).RequireAuthorization();
        app.MapDelete("/api/comments/{id:guid}", async (Guid id, ClaimsPrincipal u, PostService posts) => (await posts.DeleteComment(id, u)).ToHttp()).RequireAuthorization();
        app.MapGet("/api/users/{id:guid}/posts", async (Guid id, PostService posts) => (await posts.UserPosts(id)).ToHttp()).RequireAuthorization();

        var friends = app.MapGroup("/api/friends").RequireAuthorization();
        friends.MapGet("", async (ClaimsPrincipal u, FriendService friends) => (await friends.List(u)).ToHttp());
        friends.MapGet("/requests", async (ClaimsPrincipal u, FriendService friends) => (await friends.Incoming(u)).ToHttp());
        friends.MapGet("/sent", async (ClaimsPrincipal u, FriendService friends) => (await friends.Sent(u)).ToHttp());
        friends.MapGet("/suggestions", async (ClaimsPrincipal u, FriendService friends) => (await friends.Suggestions(u)).ToHttp());
        friends.MapGet("/count", async (ClaimsPrincipal u, FriendService friends) => (await friends.Count(u)).ToHttp());
        friends.MapGet("/online", async (ClaimsPrincipal u, FriendService friends) => (await friends.Online(u)).ToHttp());
        friends.MapGet("/locations", async (ClaimsPrincipal u, FriendService friends) => (await friends.Locations(u)).ToHttp());
        friends.MapGet("/search", async (ClaimsPrincipal u, FriendService friends, string? keyword) => (await friends.Search(u, keyword)).ToHttp());
        friends.MapGet("/status/{id:guid}", async (Guid id, ClaimsPrincipal u, FriendService friends) => (await friends.Status(id, u)).ToHttp());
        friends.MapGet("/mutual/{id:guid}", async (Guid id, ClaimsPrincipal u, FriendService friends) => (await friends.Mutual(id, u)).ToHttp());
        friends.MapPost("/request", async (ClaimsPrincipal u, FriendService friends, JsonElement body) => (await friends.Request(u, body)).ToHttp());
        friends.MapPut("/{id:guid}/accept", async (Guid id, ClaimsPrincipal u, FriendService friends) => (await friends.Accept(id, u)).ToHttp());
        friends.MapPut("/{id:guid}/reject", async (Guid id, ClaimsPrincipal u, FriendService friends) => (await friends.Reject(id, u)).ToHttp());
        friends.MapDelete("/request/{id:guid}", async (Guid id, ClaimsPrincipal u, FriendService friends) => (await friends.Cancel(id, u)).ToHttp());
        friends.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal u, FriendService friends) => (await friends.Unfriend(id, u)).ToHttp());
        friends.MapPost("/block", async (ClaimsPrincipal u, FriendService friends, JsonElement body) => (await friends.Block(u, body)).ToHttp());
        friends.MapDelete("/block/{id:guid}", async (Guid id, ClaimsPrincipal u, FriendService friends) => (await friends.Unblock(id, u)).ToHttp());
        friends.MapGet("/block", async (ClaimsPrincipal u, FriendService friends) => (await friends.Blocked(u)).ToHttp());

        var notes = app.MapGroup("/api/notifications").RequireAuthorization();
        notes.MapGet("/", async (ClaimsPrincipal u, NotificationService notes) => (await notes.List(u)).ToHttp());
        notes.MapGet("/unread-count", async (ClaimsPrincipal u, NotificationService notes) => (await notes.Unread(u)).ToHttp());
        notes.MapPatch("/{id:guid}/read", async (Guid id, ClaimsPrincipal u, NotificationService notes) => (await notes.ReadOne(id, u)).ToHttp());
        notes.MapPatch("/read-all", async (ClaimsPrincipal u, NotificationService notes) => (await notes.ReadAll(u)).ToHttp());
        notes.MapPut("/read-all", async (ClaimsPrincipal u, NotificationService notes) => (await notes.ReadAll(u)).ToHttp());
        notes.MapDelete("/clear-all", async (ClaimsPrincipal u, NotificationService notes) => (await notes.ClearAll(u)).ToHttp());
        notes.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal u, NotificationService notes) => (await notes.Delete(id, u)).ToHttp());

        app.MapPost("/api/media/upload", async (ClaimsPrincipal u, PostService posts, HttpRequest request) => (await posts.Upload(u, request.Form.Files.FirstOrDefault())).ToHttp()).RequireAuthorization().DisableAntiforgery();
        app.MapPost("/api/media/upload-multiple", async (ClaimsPrincipal u, PostService posts, HttpRequest request) => (await posts.UploadMany(u, request.Form.Files)).ToHttp()).RequireAuthorization().DisableAntiforgery();
        app.MapPost("/api/mymy/chat", (PostService posts, JsonElement body) => posts.Hana(body).ToHttp()).RequireAuthorization();

        app.MapGet("/internal/users/{id:guid}/activity", async (Guid id, HttpRequest request, IConfiguration config, PostService posts) =>
        {
            if (!Internal.Allowed(request, config)) return Results.Unauthorized();
            return (await posts.Activity(id)).ToHttp();
        });
    }
}
