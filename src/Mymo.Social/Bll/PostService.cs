using System.Text.Json;
using Mymo.Shared;
using Mymo.Social.Dal;
using Mymo.Social.Data;

namespace Mymo.Social.Bll;

public record PlaceLite(Guid PlaceId, string Name, string? Address, double Latitude, double Longitude);

public class PostService
{
    private readonly ISocialRepository _data;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;

    public PostService(ISocialRepository data, IConfiguration config, IHttpClientFactory http)
    {
        _data = data;
        _config = config;
        _http = http;
    }

    static bool Expired(PostRow post) => post.ExpiresAt != null && post.ExpiresAt < DateTime.UtcNow;

    static object FeedItem(PostRow post, List<MediaRow> media) => new
    {
        post.PostId,
        post.UserId,
        post.Caption,
        post.Visibility,
        post.AnonymousAlias,
        post.Latitude,
        post.Longitude,
        post.CreatedAt,
        post.LikeCount,
        post.CommentCount,
        isExpired = Expired(post),
        media = media.Where(m => m.PostId == post.PostId).Select(m => new
        {
            m.MediaId,
            m.Url,
            m.ThumbnailUrl,
            m.MediaType,
        }),
    };

    public async Task<ServiceResult> Feed(ClaimsPrincipal principal, bool friendsOnly)
    {
        var me = HttpUser.Id(principal);
        if (me == null) return ServiceResult.Unauthorized();
        var friends = await FriendIds(me.Value);
        var posts = await _data.LatestPosts(100);
        posts = posts.Where(p => !Expired(p)).Where(p =>
        {
            if (p.UserId == me) return true;
            if (p.Visibility == "Private") return false;
            if (friendsOnly || p.Visibility == "Friends") return friends.Contains(p.UserId);
            return p.Visibility == "Public";
        }).ToList();
        var media = await _data.MediaForPosts(posts.Select(p => p.PostId));
        return ServiceResult.Ok(ApiResponse<object>.Ok(posts.Select(p => FeedItem(p, media))));
    }

    public async Task<ServiceResult> Mine(ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal);
        if (me == null) return ServiceResult.Unauthorized();
        var posts = await _data.PostsByUser(me.Value);
        var media = await _data.MediaByUser(me.Value);
        return ServiceResult.Ok(ApiResponse<object>.Ok(posts.Where(p => !Expired(p)).Select(p => FeedItem(p, media))));
    }

    public async Task<ServiceResult> Archive(ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal);
        if (me == null) return ServiceResult.Unauthorized();
        var posts = await _data.PostsByUserRaw(me.Value);
        var media = await _data.MediaByUser(me.Value);
        return ServiceResult.Ok(ApiResponse<object>.Ok(posts.Where(Expired).Select(p => FeedItem(p, media))));
    }

    public async Task<ServiceResult> UserPosts(Guid id)
    {
        var posts = (await _data.PublicPosts(id)).Where(p => !Expired(p)).ToList();
        var media = await _data.MediaForPosts(posts.Select(p => p.PostId));
        return ServiceResult.Ok(ApiResponse<object>.Ok(posts.Select(p => FeedItem(p, media))));
    }

    public async Task<ServiceResult> Nearby(double? lat, double? lng, double? latitude, double? longitude, double radius)
    {
        var y = lat ?? latitude ?? 0;
        var x = lng ?? longitude ?? 0;
        var posts = await _data.PublicWithLocation();
        var picked = posts.Where(p => !Expired(p) && Geo.Km(y, x, p.Latitude!.Value, p.Longitude ?? 0) <= radius).ToList();
        var media = await _data.MediaForPosts(picked.Select(p => p.PostId));
        var data = picked.Select(p => new
        {
            p.PostId,
            p.UserId,
            displayName = p.IsAnonymous ? p.AnonymousAlias ?? "Ẩn danh" : p.DisplayName,
            p.AvatarUrl,
            p.AuthorIsPremium,
            p.IsAnonymous,
            p.AnonymousAlias,
            thumbnailUrl = media.FirstOrDefault(m => m.PostId == p.PostId)?.ThumbnailUrl ?? media.FirstOrDefault(m => m.PostId == p.PostId)?.Url,
            caption = p.Caption ?? "",
            latitude = p.Latitude,
            longitude = p.Longitude,
            p.CreatedAt,
            p.Visibility,
            p.LikeCount,
            p.CommentCount,
            isExpired = false,
        });
        return ServiceResult.Ok(ApiResponse<object>.Ok(data));
    }

    public async Task<ServiceResult> Heat(double lat, double lng, double radius)
    {
        var posts = await _data.WithLocation();
        var data = posts.Where(p => Geo.Km(lat, lng, p.Latitude!.Value, p.Longitude ?? 0) <= radius)
            .Select(p => new { p.UserId, latitude = p.Latitude, longitude = p.Longitude });
        return ServiceResult.Ok(ApiResponse<object>.Ok(data));
    }

    public async Task<ServiceResult> Detail(Guid id, ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal);
        var post = await _data.ActivePost(id);
        if (post == null) return ServiceResult.NotFound(ApiResponse<object>.Fail("Không thấy bài viết"));
        var media = await _data.MediaOfPost(id);
        var liked = me != null && await _data.Liked(id, me.Value);
        return ServiceResult.Ok(ApiResponse<object>.Ok(new
        {
            post.PostId,
            post.Caption,
            post.PostType,
            post.Visibility,
            post.AnonymousAlias,
            post.CreatedAt,
            updatedAt = (DateTime?)null,
            post.Latitude,
            post.Longitude,
            post.LikeCount,
            post.CommentCount,
            post.ViewCount,
            shareCount = 0,
            isLiked = liked,
            owner = new
            {
                userId = post.UserId,
                displayName = post.IsAnonymous ? post.AnonymousAlias : post.DisplayName,
                post.Username,
                avatarUrl = post.IsAnonymous ? null : post.AvatarUrl,
                isPremium = post.AuthorIsPremium,
            },
            place = post.PlaceId == null ? null : new
            {
                placeId = post.PlaceId,
                name = post.PlaceName ?? "",
                address = post.PlaceAddress,
                city = "Hồ Chí Minh",
                post.Latitude,
                post.Longitude,
                thumbnailUrl = (string?)null,
            },
            media = media.Select(m => new { m.MediaId, m.Url, m.ThumbnailUrl, m.MediaType, m.Width, m.Height, m.Duration }),
        }));
    }

    public async Task<ServiceResult> Create(ClaimsPrincipal principal, JsonElement body)
    {
        var me = HttpUser.Id(principal);
        if (me == null) return ServiceResult.Unauthorized();
        var card = await Internal.GetAsync<UserCard>(_http, $"{_config["Services:Identity"]}/internal/users/{me}");
        var post = new PostRow
        {
            PostId = Guid.NewGuid(),
            UserId = me.Value,
            DisplayName = card?.DisplayName ?? "User",
            Username = card?.Username ?? "user",
            AvatarUrl = card?.AvatarUrl,
            AuthorIsPremium = card?.IsPremium ?? false,
            Caption = JsonBody.Text(body, "caption"),
            PostType = JsonBody.Text(body, "postType") ?? "Photo",
            Visibility = JsonBody.Text(body, "visibility") ?? "Public",
            AnonymousAlias = JsonBody.Text(body, "anonymousAlias"),
            IsAnonymous = !string.IsNullOrWhiteSpace(JsonBody.Text(body, "anonymousAlias")),
            Latitude = JsonBody.Num(body, "latitude"),
            Longitude = JsonBody.Num(body, "longitude"),
            CreatedAt = DateTime.UtcNow,
        };
        if (Guid.TryParse(JsonBody.Text(body, "placeId"), out var placeId))
        {
            post.PlaceId = placeId;
            var place = await Internal.GetAsync<PlaceLite>(_http, $"{_config["Services:Map"]}/internal/places/{placeId}");
            post.PlaceName = place?.Name;
            post.PlaceAddress = place?.Address;
            post.Latitude ??= place?.Latitude;
            post.Longitude ??= place?.Longitude;
        }
        _data.AddPost(post);
        var linked = new List<Guid>();
        if (body.TryGetProperty("mediaIds", out var ids) && ids.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in ids.EnumerateArray())
            {
                if (!Guid.TryParse(item.GetString(), out var mediaId)) continue;
                var media = await _data.OwnMedia(mediaId, me.Value);
                if (media == null) continue;
                media.PostId = post.PostId;
                linked.Add(mediaId);
            }
        }
        await _data.Save();
        await Internal.PostAsync<object>(_http, $"{_config["Services:Map"]}/internal/events/post", new
        {
            post.PostId,
            post.UserId,
            post.DisplayName,
            post.AvatarUrl,
            post.Caption,
            post.Latitude,
            post.Longitude,
            post.CreatedAt,
        });
        return ServiceResult.Ok(ApiResponse<object>.Ok(new
        {
            id = post.PostId,
            post.PlaceId,
            post.Caption,
            post.PostType,
            post.Visibility,
            mediaIds = linked,
            post.CreatedAt,
        }));
    }

    public async Task<ServiceResult> DeletePost(Guid id, ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal);
        var post = await _data.OwnPost(id, me);
        if (post == null) return ServiceResult.NotFound(ApiResponse<bool>.Ok(false));
        post.IsDeleted = true;
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> Like(Guid id, ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal);
        if (me == null) return ServiceResult.Unauthorized();
        if (!await _data.HasLike(id, me.Value))
        {
            _data.AddLike(new LikeRow { LikeId = Guid.NewGuid(), PostId = id, UserId = me.Value });
            var post = await _data.FindPost(id);
            if (post != null)
            {
                post.LikeCount++;
                if (post.UserId != me)
                {
                    _data.AddNotification(new NotificationRow
                    {
                        NotificationId = Guid.NewGuid(),
                        ReceiverId = post.UserId,
                        SenderId = me.Value,
                        Type = "PostLiked",
                        Title = "Lượt thích mới",
                        Content = "đã thích bài viết của bạn",
                        ReferenceId = post.PostId,
                        SenderName = post.DisplayName,
                        CreatedAt = DateTime.UtcNow,
                    });
                }
            }
            await _data.Save();
        }
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> Unlike(Guid id, ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal);
        var like = await _data.FindLike(id, me);
        if (like != null)
        {
            _data.RemoveLike(like);
            var post = await _data.FindPost(id);
            if (post != null && post.LikeCount > 0) post.LikeCount--;
            await _data.Save();
        }
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> View(Guid id, ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal);
        if (me == null) return ServiceResult.Unauthorized();
        if (!await _data.HasView(id, me.Value))
        {
            _data.AddView(new PostViewRow { PostViewId = Guid.NewGuid(), PostId = id, UserId = me.Value, CreatedAt = DateTime.UtcNow });
            var post = await _data.FindPost(id);
            if (post != null) post.ViewCount++;
            await _data.Save();
        }
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> Viewers(Guid id)
    {
        var views = await _data.Views(id);
        var cards = await Internal.PostAsync<List<UserCard>>(_http, $"{_config["Services:Identity"]}/internal/users/batch", new { ids = views.Select(v => v.UserId) }) ?? [];
        var data = views.Select(v =>
        {
            var card = cards.FirstOrDefault(c => c.UserId == v.UserId);
            return new { userId = v.UserId, displayName = card?.DisplayName, avatarUrl = card?.AvatarUrl, v.CreatedAt };
        });
        return ServiceResult.Ok(ApiResponse<object>.Ok(data));
    }

    public async Task<ServiceResult> Report(Guid id, ClaimsPrincipal principal, JsonElement body)
    {
        var me = HttpUser.Id(principal);
        if (me == null) return ServiceResult.Unauthorized();
        var post = await _data.FindPost(id);
        await Internal.PostAsync<object>(_http, $"{_config["Services:Identity"]}/internal/reports", new
        {
            reporterId = me,
            targetType = "Post",
            targetId = id,
            reason = JsonBody.Text(body, "reason") ?? "reported",
            targetSummary = post?.Caption,
        });
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> Comments(Guid id)
    {
        var all = await _data.CommentsOf(id);
        return ServiceResult.Ok(ApiResponse<object>.Ok(all.Where(c => c.ParentCommentId == null).Select(c => CommentDto(c, all))));
    }

    public Task<ServiceResult> AddComment(Guid id, ClaimsPrincipal principal, JsonElement body) =>
        WriteComment(id, null, principal, JsonBody.Text(body, "content") ?? "");

    public async Task<ServiceResult> Reply(Guid id, ClaimsPrincipal principal, JsonElement body)
    {
        var parent = await _data.FindComment(id);
        if (parent == null) return ServiceResult.NotFound(ApiResponse<object>.Fail("Không thấy bình luận"));
        return await WriteComment(parent.PostId, parent.CommentId, principal, JsonBody.Text(body, "content") ?? "");
    }

    public async Task<ServiceResult> DeleteComment(Guid id, ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal);
        var comment = await _data.OwnComment(id, me);
        if (comment == null) return ServiceResult.NotFound();
        comment.IsDeleted = true;
        var post = await _data.FindPost(comment.PostId);
        if (post != null && post.CommentCount > 0) post.CommentCount--;
        await _data.Save();
        return ServiceResult.Empty();
    }

    public async Task<ServiceResult> Upload(ClaimsPrincipal principal, IFormFile? file)
    {
        var me = HttpUser.Id(principal);
        if (me == null) return ServiceResult.Unauthorized();
        if (file == null) return ServiceResult.BadRequest(ApiResponse<object>.Fail("Thiếu file"));
        var url = await Uploads.Save(_config, file);
        var media = new MediaRow { MediaId = Guid.NewGuid(), UserId = me.Value, Url = url, ThumbnailUrl = url, MediaType = "Image", CreatedAt = DateTime.UtcNow };
        _data.AddMedia(media);
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<object>.Ok(new { id = media.MediaId, media.MediaId, media.Url, fileName = file.FileName }));
    }

    public async Task<ServiceResult> UploadMany(ClaimsPrincipal principal, IFormFileCollection files)
    {
        var me = HttpUser.Id(principal);
        if (me == null) return ServiceResult.Unauthorized();
        var list = new List<object>();
        foreach (var file in files)
        {
            var url = await Uploads.Save(_config, file);
            var media = new MediaRow { MediaId = Guid.NewGuid(), UserId = me.Value, Url = url, ThumbnailUrl = url, MediaType = "Image", CreatedAt = DateTime.UtcNow };
            _data.AddMedia(media);
            list.Add(new { id = media.MediaId, media.MediaId, media.Url });
        }
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<object>.Ok(list));
    }

    public ServiceResult Hana(JsonElement body)
    {
        var message = JsonBody.Text(body, "message") ?? "";
        var reply = string.IsNullOrWhiteSpace(message)
            ? "Mình là Hana. Bạn muốn tìm quán hay xem bạn bè đang ở đâu?"
            : $"Mình là Hana. Mình nghe bạn nói: \"{message}\". Thử mở bản đồ quanh Nguyễn Huệ, có vài quán đã được ghim sẵn.";
        return ServiceResult.Ok(ApiResponse<object>.Ok(new { reply }));
    }

    public async Task<ServiceResult> Activity(Guid id)
    {
        var posts = await _data.PostsByUserRaw(id);
        var postIds = posts.Select(p => p.PostId).ToList();
        var photos = await _data.PhotoCount(postIds);
        var likes = posts.Sum(p => p.LikeCount);
        var comments = await _data.LiveCommentCount(postIds);
        var friends = (await FriendIds(id)).Count;
        return ServiceResult.Ok(new { postCount = posts.Count, photoPosts = photos, likesReceived = likes, commentsReceived = comments, friendCount = friends });
    }

    async Task<ServiceResult> WriteComment(Guid postId, Guid? parentId, ClaimsPrincipal principal, string content)
    {
        var me = HttpUser.Id(principal);
        if (me == null) return ServiceResult.Unauthorized();
        var card = await Internal.GetAsync<UserCard>(_http, $"{_config["Services:Identity"]}/internal/users/{me}");
        var comment = new CommentRow
        {
            CommentId = Guid.NewGuid(),
            PostId = postId,
            UserId = me.Value,
            ParentCommentId = parentId,
            Content = content,
            DisplayName = card?.DisplayName ?? "User",
            Username = card?.Username ?? "user",
            AvatarUrl = card?.AvatarUrl,
            CreatedAt = DateTime.UtcNow,
        };
        _data.AddComment(comment);
        var post = await _data.FindPost(postId);
        if (post != null) post.CommentCount++;
        await _data.Save();
        var all = await _data.CommentsOf(postId);
        return ServiceResult.Ok(ApiResponse<object>.Ok(CommentDto(comment, all)));
    }

    static object CommentDto(CommentRow comment, List<CommentRow> all) => new
    {
        comment.CommentId,
        comment.PostId,
        comment.UserId,
        comment.ParentCommentId,
        comment.Content,
        comment.LikeCount,
        comment.CreatedAt,
        updatedAt = (DateTime?)null,
        user = new { comment.UserId, displayName = comment.DisplayName, comment.Username, comment.AvatarUrl },
        replies = all.Where(c => c.ParentCommentId == comment.CommentId).Select(c => CommentDto(c, all)),
    };

    async Task<HashSet<Guid>> FriendIds(Guid me)
    {
        var rows = await _data.Accepted(me);
        return rows.Select(f => f.RequesterId == me ? f.ReceiverId : f.RequesterId).ToHashSet();
    }
}
