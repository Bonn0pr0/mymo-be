using System.Text.Json;
using Mymo.Chat.Bll;

namespace Mymo.Chat;

public static class ChatEndpoints
{
    public static void Map(WebApplication app)
    {
        var conversations = app.MapGroup("/api/conversations").RequireAuthorization();
        conversations.MapGet("/", async (ClaimsPrincipal principal, ChatService chat) => (await chat.List(principal)).ToHttp());
        conversations.MapPost("/private", async (ClaimsPrincipal principal, ChatService chat, JsonElement body) => (await chat.OpenPrivate(principal, body)).ToHttp());
        conversations.MapGet("/{id:guid}/messages", async (Guid id, ClaimsPrincipal principal, ChatService chat, int page = 1, int pageSize = 50) => (await chat.Messages(id, principal, page, pageSize)).ToHttp());
        conversations.MapPost("/{id:guid}/messages", async (Guid id, ClaimsPrincipal principal, ChatService chat, JsonElement body) => (await chat.Send(id, principal, body)).ToHttp());
        conversations.MapPatch("/{id:guid}/read-all", async (Guid id, ClaimsPrincipal principal, ChatService chat) => (await chat.ReadAll(id, principal)).ToHttp());

        app.MapPatch("/api/messages/{id:guid}/read", async (Guid id, ChatService chat) => (await chat.ReadOne(id)).ToHttp()).RequireAuthorization();
        app.MapDelete("/api/messages/{id:guid}", async (Guid id, ClaimsPrincipal principal, ChatService chat) => (await chat.Delete(id, principal)).ToHttp()).RequireAuthorization();
    }
}
