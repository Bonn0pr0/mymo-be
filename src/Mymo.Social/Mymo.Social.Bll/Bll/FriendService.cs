using System.Text.Json;
using Mymo.Shared;
using Mymo.Social.Dal;
using Mymo.Social.Data;

namespace Mymo.Social.Bll;

public class FriendService
{
    private readonly ISocialRepository _data;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;

    public FriendService(ISocialRepository data, IConfiguration config, IHttpClientFactory http)
    {
        _data = data;
        _config = config;
        _http = http;
    }

    public async Task<ServiceResult> List(ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        var cards = await Cards(await FriendIds(me));
        return ServiceResult.Ok(ApiResponse<object>.Ok(cards.Select(c => FriendDto(c))));
    }

    public async Task<ServiceResult> Incoming(ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        var rows = await _data.Incoming(me);
        var cards = await Cards(rows.Select(r => r.RequesterId));
        var data = rows.Select(r =>
        {
            var card = cards.FirstOrDefault(c => c.UserId == r.RequesterId);
            return new
            {
                requesterId = r.RequesterId,
                requesterUsername = card?.Username,
                requesterDisplayName = card?.DisplayName,
                requesterAvatarUrl = card?.AvatarUrl,
                requestedAt = r.CreatedAt,
            };
        });
        return ServiceResult.Ok(ApiResponse<object>.Ok(data));
    }

    public async Task<ServiceResult> Sent(ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        var rows = await _data.Sent(me);
        var cards = await Cards(rows.Select(r => r.ReceiverId));
        var data = rows.Select(r =>
        {
            var card = cards.FirstOrDefault(c => c.UserId == r.ReceiverId);
            return new { receiverId = r.ReceiverId, receiverUsername = card?.Username, receiverDisplayName = card?.DisplayName, receiverAvatarUrl = card?.AvatarUrl, requestedAt = r.CreatedAt };
        });
        return ServiceResult.Ok(ApiResponse<object>.Ok(data));
    }

    public async Task<ServiceResult> Suggestions(ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        var known = await FriendIds(me);
        known.Add(me);
        var cards = await Internal.GetAsync<List<UserCard>>(_http, $"{_config["Services:Identity"]}/internal/users/search?keyword=m") ?? [];
        return ServiceResult.Ok(ApiResponse<object>.Ok(cards.Where(c => !known.Contains(c.UserId)).Take(10).Select(c => FriendDto(c))));
    }

    public async Task<ServiceResult> Count(ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        return ServiceResult.Ok(ApiResponse<int>.Ok((await FriendIds(me)).Count));
    }

    public async Task<ServiceResult> Online(ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        var cards = await Cards(await FriendIds(me));
        return ServiceResult.Ok(ApiResponse<object>.Ok(cards.Where(c => c.IsOnline).Select(c => FriendDto(c))));
    }

    public async Task<ServiceResult> Locations(ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        var cards = await Cards(await FriendIds(me));
        return ServiceResult.Ok(ApiResponse<object>.Ok(cards.Select(c => new
        {
            c.UserId,
            c.DisplayName,
            c.AvatarUrl,
            c.IsOnline,
            latitude = 10.776,
            longitude = 106.700,
        })));
    }

    public async Task<ServiceResult> Search(ClaimsPrincipal principal, string? keyword)
    {
        var cards = await Internal.GetAsync<List<UserCard>>(_http, $"{_config["Services:Identity"]}/internal/users/search?keyword={Uri.EscapeDataString(keyword ?? "")}") ?? [];
        var me = HttpUser.Id(principal);
        return ServiceResult.Ok(ApiResponse<object>.Ok(cards.Where(c => c.UserId != me).Select(c => FriendDto(c))));
    }

    public async Task<ServiceResult> Status(Guid id, ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        var row = await _data.Between(me, id);
        var status = row?.Status ?? "None";
        if (status == "Accepted") status = "Friends";
        return ServiceResult.Ok(ApiResponse<object>.Ok(new { userId = id, status, isIncoming = row?.ReceiverId == me && row.Status == "Pending" }));
    }

    public async Task<ServiceResult> Mutual(Guid id, ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        var mine = await FriendIds(me);
        var theirs = await FriendIds(id);
        var cards = await Cards(mine.Intersect(theirs));
        return ServiceResult.Ok(ApiResponse<object>.Ok(cards.Select(c => FriendDto(c))));
    }

    public async Task<ServiceResult> Request(ClaimsPrincipal principal, JsonElement body)
    {
        var me = HttpUser.Id(principal)!.Value;
        if (!Guid.TryParse(JsonBody.Text(body, "userId"), out var other) || other == me)
            return ServiceResult.BadRequest(ApiResponse<bool>.Fail("Người dùng không hợp lệ"));
        if (!await _data.RelationExists(me, other))
        {
            var card = await Internal.GetAsync<UserCard>(_http, $"{_config["Services:Identity"]}/internal/users/{me}");
            _data.AddFriendship(new FriendshipRow { FriendshipId = Guid.NewGuid(), RequesterId = me, ReceiverId = other, Status = "Pending", CreatedAt = DateTime.UtcNow });
            _data.AddNotification(new NotificationRow
            {
                NotificationId = Guid.NewGuid(),
                ReceiverId = other,
                SenderId = me,
                Type = "FriendRequest",
                Title = "Lời mời kết bạn",
                Content = "đã gửi lời mời kết bạn",
                SenderName = card?.DisplayName,
                SenderAvatar = card?.AvatarUrl,
                CreatedAt = DateTime.UtcNow,
            });
            await _data.Save();
        }
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> Accept(Guid id, ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        var row = await _data.PendingToMe(id, me);
        if (row == null) return ServiceResult.BadRequest(ApiResponse<bool>.Fail("Không thấy lời mời"));
        row.Status = "Accepted";
        _data.AddNotification(new NotificationRow
        {
            NotificationId = Guid.NewGuid(),
            ReceiverId = id,
            SenderId = me,
            Type = "FriendAccepted",
            Content = "đã đồng ý kết bạn",
            CreatedAt = DateTime.UtcNow,
        });
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> Reject(Guid id, ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        var row = await _data.PendingToMe(id, me);
        if (row != null) row.Status = "Rejected";
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> Cancel(Guid id, ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        var row = await _data.PendingFromMe(me, id);
        if (row != null) _data.RemoveFriendship(row);
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> Unfriend(Guid id, ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        var row = await _data.AcceptedPair(me, id);
        if (row != null) _data.RemoveFriendship(row);
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> Block(ClaimsPrincipal principal, JsonElement body)
    {
        var me = HttpUser.Id(principal)!.Value;
        if (!Guid.TryParse(JsonBody.Text(body, "userId"), out var other)) return ServiceResult.BadRequest(ApiResponse<bool>.Fail("Thiếu user"));
        var row = await _data.EitherWay(me, other);
        if (row == null) _data.AddFriendship(new FriendshipRow { FriendshipId = Guid.NewGuid(), RequesterId = me, ReceiverId = other, Status = "Blocked", CreatedAt = DateTime.UtcNow });
        else row.Status = "Blocked";
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> Unblock(Guid id, ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        var row = await _data.BlockFromMe(me, id);
        if (row != null) _data.RemoveFriendship(row);
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> Blocked(ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        var rows = await _data.BlockedBy(me);
        var cards = await Cards(rows.Select(r => r.ReceiverId));
        return ServiceResult.Ok(ApiResponse<object>.Ok(cards.Select(c => FriendDto(c))));
    }

    async Task<HashSet<Guid>> FriendIds(Guid me)
    {
        var rows = await _data.Accepted(me);
        return rows.Select(f => f.RequesterId == me ? f.ReceiverId : f.RequesterId).ToHashSet();
    }

    async Task<List<UserCard>> Cards(IEnumerable<Guid> ids) =>
        await Internal.PostAsync<List<UserCard>>(_http, $"{_config["Services:Identity"]}/internal/users/batch", new { ids = ids.Distinct() }) ?? [];

    static object FriendDto(UserCard card, DateTime? requestedAt = null) => new
    {
        card.UserId,
        card.Username,
        card.DisplayName,
        card.AvatarUrl,
        requestedAt,
        card.IsOnline,
    };
}
