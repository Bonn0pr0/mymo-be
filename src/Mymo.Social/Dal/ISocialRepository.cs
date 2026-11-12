using Mymo.Social.Data;

namespace Mymo.Social.Dal;

public interface ISocialRepository
{
    Task Save();
    Task<List<FriendshipRow>> Accepted(Guid userId);
    Task<List<PostRow>> LatestPosts(int take);
    Task<List<MediaRow>> MediaForPosts(IEnumerable<Guid> postIds);
    Task<List<PostRow>> PostsByUser(Guid userId);
    Task<List<PostRow>> PostsByUserRaw(Guid userId);
    Task<List<MediaRow>> MediaByUser(Guid userId);
    Task<List<PostRow>> PublicPosts(Guid userId);
    Task<List<PostRow>> PublicWithLocation();
    Task<List<PostRow>> WithLocation();
    Task<PostRow?> ActivePost(Guid id);
    Task<List<MediaRow>> MediaOfPost(Guid postId);
    Task<bool> Liked(Guid postId, Guid userId);
    void AddPost(PostRow post);
    Task<MediaRow?> OwnMedia(Guid mediaId, Guid userId);
    Task<PostRow?> OwnPost(Guid postId, Guid? userId);
    Task<PostRow?> FindPost(Guid id);
    Task<bool> HasLike(Guid postId, Guid userId);
    void AddLike(LikeRow like);
    void AddNotification(NotificationRow note);
    Task<LikeRow?> FindLike(Guid postId, Guid? userId);
    void RemoveLike(LikeRow like);
    Task<bool> HasView(Guid postId, Guid userId);
    void AddView(PostViewRow view);
    Task<List<PostViewRow>> Views(Guid postId);
    Task<List<CommentRow>> CommentsOf(Guid postId);
    Task<CommentRow?> FindComment(Guid id);
    void AddComment(CommentRow comment);
    Task<CommentRow?> OwnComment(Guid id, Guid? userId);
    Task<List<FriendshipRow>> Incoming(Guid userId);
    Task<List<FriendshipRow>> Sent(Guid userId);
    Task<FriendshipRow?> Between(Guid a, Guid b);
    Task<bool> RelationExists(Guid a, Guid b);
    void AddFriendship(FriendshipRow row);
    void RemoveFriendship(FriendshipRow row);
    Task<FriendshipRow?> PendingToMe(Guid requesterId, Guid me);
    Task<FriendshipRow?> PendingFromMe(Guid me, Guid other);
    Task<FriendshipRow?> AcceptedPair(Guid a, Guid b);
    Task<FriendshipRow?> EitherWay(Guid a, Guid b);
    Task<FriendshipRow?> BlockFromMe(Guid me, Guid other);
    Task<List<FriendshipRow>> BlockedBy(Guid userId);
    Task<List<NotificationRow>> Notes(Guid userId);
    Task<int> UnreadCount(Guid userId);
    Task<NotificationRow?> OwnNote(Guid id, Guid userId);
    Task<List<NotificationRow>> Unread(Guid userId);
    Task<List<NotificationRow>> AllNotes(Guid userId);
    void RemoveNotes(IEnumerable<NotificationRow> notes);
    void RemoveNote(NotificationRow note);
    void AddMedia(MediaRow media);
    Task<int> PhotoCount(IEnumerable<Guid> postIds);
    Task<int> LiveCommentCount(IEnumerable<Guid> postIds);
}
