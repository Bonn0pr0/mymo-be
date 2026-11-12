using Microsoft.EntityFrameworkCore;
using Mymo.Social.Data;

namespace Mymo.Social.Dal;

public class SocialRepository : ISocialRepository
{
    private readonly SocialDbContext _db;

    public SocialRepository(SocialDbContext db) => _db = db;

    public Task Save() => _db.SaveChangesAsync();

    public Task<List<FriendshipRow>> Accepted(Guid userId) =>
        _db.Friendships.Where(f => f.Status == "Accepted" && (f.RequesterId == userId || f.ReceiverId == userId)).ToListAsync();

    public Task<List<PostRow>> LatestPosts(int take) =>
        _db.Posts.Where(p => !p.IsDeleted).OrderByDescending(p => p.CreatedAt).Take(take).ToListAsync();

    public Task<List<MediaRow>> MediaForPosts(IEnumerable<Guid> postIds)
    {
        var ids = postIds.ToList();
        return _db.Media.Where(m => m.PostId != null && ids.Contains(m.PostId.Value)).ToListAsync();
    }

    public Task<List<PostRow>> PostsByUser(Guid userId) =>
        _db.Posts.Where(p => p.UserId == userId && !p.IsDeleted).OrderByDescending(p => p.CreatedAt).ToListAsync();

    public Task<List<PostRow>> PostsByUserRaw(Guid userId) =>
        _db.Posts.Where(p => p.UserId == userId && !p.IsDeleted).ToListAsync();

    public Task<List<MediaRow>> MediaByUser(Guid userId) =>
        _db.Media.Where(m => m.UserId == userId).ToListAsync();

    public Task<List<PostRow>> PublicPosts(Guid userId) =>
        _db.Posts.Where(p => p.UserId == userId && !p.IsDeleted && p.Visibility == "Public").OrderByDescending(p => p.CreatedAt).ToListAsync();

    public Task<List<PostRow>> PublicWithLocation() =>
        _db.Posts.Where(p => !p.IsDeleted && p.Latitude != null && p.Visibility == "Public").ToListAsync();

    public Task<List<PostRow>> WithLocation() =>
        _db.Posts.Where(p => !p.IsDeleted && p.Latitude != null).ToListAsync();

    public Task<PostRow?> ActivePost(Guid id) =>
        _db.Posts.FirstOrDefaultAsync(p => p.PostId == id && !p.IsDeleted);

    public Task<List<MediaRow>> MediaOfPost(Guid postId) =>
        _db.Media.Where(m => m.PostId == postId).ToListAsync();

    public Task<bool> Liked(Guid postId, Guid userId) =>
        _db.Likes.AnyAsync(l => l.PostId == postId && l.UserId == userId);

    public void AddPost(PostRow post) => _db.Posts.Add(post);

    public Task<MediaRow?> OwnMedia(Guid mediaId, Guid userId) =>
        _db.Media.FirstOrDefaultAsync(m => m.MediaId == mediaId && m.UserId == userId);

    public Task<PostRow?> OwnPost(Guid postId, Guid? userId) =>
        _db.Posts.FirstOrDefaultAsync(p => p.PostId == postId && p.UserId == userId);

    public Task<PostRow?> FindPost(Guid id) => _db.Posts.FirstOrDefaultAsync(p => p.PostId == id);

    public Task<bool> HasLike(Guid postId, Guid userId) => Liked(postId, userId);

    public void AddLike(LikeRow like) => _db.Likes.Add(like);

    public void AddNotification(NotificationRow note) => _db.Notifications.Add(note);

    public Task<LikeRow?> FindLike(Guid postId, Guid? userId) =>
        _db.Likes.FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

    public void RemoveLike(LikeRow like) => _db.Likes.Remove(like);

    public Task<bool> HasView(Guid postId, Guid userId) =>
        _db.Views.AnyAsync(v => v.PostId == postId && v.UserId == userId);

    public void AddView(PostViewRow view) => _db.Views.Add(view);

    public Task<List<PostViewRow>> Views(Guid postId) =>
        _db.Views.Where(v => v.PostId == postId).OrderByDescending(v => v.CreatedAt).Take(50).ToListAsync();

    public Task<List<CommentRow>> CommentsOf(Guid postId) =>
        _db.Comments.Where(c => c.PostId == postId && !c.IsDeleted).OrderBy(c => c.CreatedAt).ToListAsync();

    public Task<CommentRow?> FindComment(Guid id) => _db.Comments.FirstOrDefaultAsync(c => c.CommentId == id);

    public void AddComment(CommentRow comment) => _db.Comments.Add(comment);

    public Task<CommentRow?> OwnComment(Guid id, Guid? userId) =>
        _db.Comments.FirstOrDefaultAsync(c => c.CommentId == id && c.UserId == userId);

    public Task<List<FriendshipRow>> Incoming(Guid userId) =>
        _db.Friendships.Where(f => f.ReceiverId == userId && f.Status == "Pending").ToListAsync();

    public Task<List<FriendshipRow>> Sent(Guid userId) =>
        _db.Friendships.Where(f => f.RequesterId == userId && f.Status == "Pending").ToListAsync();

    public Task<FriendshipRow?> Between(Guid a, Guid b) =>
        _db.Friendships.FirstOrDefaultAsync(f => (f.RequesterId == a && f.ReceiverId == b) || (f.RequesterId == b && f.ReceiverId == a));

    public Task<bool> RelationExists(Guid a, Guid b) =>
        _db.Friendships.AnyAsync(f => (f.RequesterId == a && f.ReceiverId == b) || (f.RequesterId == b && f.ReceiverId == a));

    public void AddFriendship(FriendshipRow row) => _db.Friendships.Add(row);
    public void RemoveFriendship(FriendshipRow row) => _db.Friendships.Remove(row);

    public Task<FriendshipRow?> PendingToMe(Guid requesterId, Guid me) =>
        _db.Friendships.FirstOrDefaultAsync(f => f.RequesterId == requesterId && f.ReceiverId == me && f.Status == "Pending");

    public Task<FriendshipRow?> PendingFromMe(Guid me, Guid other) =>
        _db.Friendships.FirstOrDefaultAsync(f => f.RequesterId == me && f.ReceiverId == other && f.Status == "Pending");

    public Task<FriendshipRow?> AcceptedPair(Guid a, Guid b) =>
        _db.Friendships.FirstOrDefaultAsync(f => f.Status == "Accepted" && ((f.RequesterId == a && f.ReceiverId == b) || (f.RequesterId == b && f.ReceiverId == a)));

    public Task<FriendshipRow?> EitherWay(Guid a, Guid b) => Between(a, b);

    public Task<FriendshipRow?> BlockFromMe(Guid me, Guid other) =>
        _db.Friendships.FirstOrDefaultAsync(f => f.Status == "Blocked" && f.RequesterId == me && f.ReceiverId == other);

    public Task<List<FriendshipRow>> BlockedBy(Guid userId) =>
        _db.Friendships.Where(f => f.Status == "Blocked" && f.RequesterId == userId).ToListAsync();

    public Task<List<NotificationRow>> Notes(Guid userId) =>
        _db.Notifications.Where(n => n.ReceiverId == userId).OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync();

    public Task<int> UnreadCount(Guid userId) =>
        _db.Notifications.CountAsync(n => n.ReceiverId == userId && !n.IsRead);

    public Task<NotificationRow?> OwnNote(Guid id, Guid userId) =>
        _db.Notifications.FirstOrDefaultAsync(n => n.NotificationId == id && n.ReceiverId == userId);

    public Task<List<NotificationRow>> Unread(Guid userId) =>
        _db.Notifications.Where(n => n.ReceiverId == userId && !n.IsRead).ToListAsync();

    public Task<List<NotificationRow>> AllNotes(Guid userId) =>
        _db.Notifications.Where(n => n.ReceiverId == userId).ToListAsync();

    public void RemoveNotes(IEnumerable<NotificationRow> notes) => _db.Notifications.RemoveRange(notes);
    public void RemoveNote(NotificationRow note) => _db.Notifications.Remove(note);
    public void AddMedia(MediaRow media) => _db.Media.Add(media);

    public Task<int> PhotoCount(IEnumerable<Guid> postIds)
    {
        var ids = postIds.ToList();
        return _db.Media.CountAsync(m => m.PostId != null && ids.Contains(m.PostId.Value));
    }

    public Task<int> LiveCommentCount(IEnumerable<Guid> postIds)
    {
        var ids = postIds.ToList();
        return _db.Comments.CountAsync(c => ids.Contains(c.PostId) && !c.IsDeleted);
    }
}
