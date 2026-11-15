using Mymo.Shared;
using Mymo.Social.Dal;
using Mymo.Social.Data;

namespace Mymo.Social.Bll;

public class NotificationService
{
    private readonly ISocialRepository _data;

    public NotificationService(ISocialRepository data) => _data = data;

    public async Task<ServiceResult> List(ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        var notes = await _data.Notes(me);
        return ServiceResult.Ok(ApiResponse<object>.Ok(notes.Select(NoteDto)));
    }

    public async Task<ServiceResult> Unread(ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        return ServiceResult.Ok(ApiResponse<int>.Ok(await _data.UnreadCount(me)));
    }

    public async Task<ServiceResult> ReadOne(Guid id, ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        var note = await _data.OwnNote(id, me);
        if (note != null) note.IsRead = true;
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> ReadAll(ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        var notes = await _data.Unread(me);
        notes.ForEach(n => n.IsRead = true);
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> ClearAll(ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        _data.RemoveNotes(await _data.AllNotes(me));
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> Delete(Guid id, ClaimsPrincipal principal)
    {
        var me = HttpUser.Id(principal)!.Value;
        var note = await _data.OwnNote(id, me);
        if (note != null) _data.RemoveNote(note);
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    static object NoteDto(NotificationRow note) => new
    {
        note.NotificationId,
        note.SenderId,
        note.Type,
        note.Title,
        note.Content,
        note.ReferenceId,
        note.IsRead,
        note.CreatedAt,
        sender = new { userId = note.SenderId, displayName = note.SenderName, avatarUrl = note.SenderAvatar },
    };
}
