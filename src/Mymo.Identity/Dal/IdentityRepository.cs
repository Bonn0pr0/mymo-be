using Microsoft.EntityFrameworkCore;
using Mymo.Identity.Data;

namespace Mymo.Identity.Dal;

public class IdentityRepository : IIdentityRepository
{
    private readonly IdentityDbContext _db;

    public IdentityRepository(IdentityDbContext db) => _db = db;

    public Task Save() => _db.SaveChangesAsync();

    public Task<AppUser?> FindUser(Guid id) => _db.Users.FirstOrDefaultAsync(u => u.UserId == id);

    public Task<AppUser?> FindActiveByEmail(string email) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);

    public Task<AppUser?> FindByEmail(string email) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email == email);

    public Task<bool> EmailOrUsernameTaken(string email, string username) =>
        _db.Users.AnyAsync(u => u.Email == email || u.Username == username);

    public void AddUser(AppUser user) => _db.Users.Add(user);

    public Task<List<AppUser>> ActiveUsers() =>
        _db.Users.Where(u => !u.IsDeleted).ToListAsync();

    public async Task<(int Total, List<AppUser> Items)> PageUsers(string? keyword, int page, int pageSize)
    {
        var query = _db.Users.Where(u => !u.IsDeleted);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim().ToLower();
            query = query.Where(u => u.Email.Contains(k) || u.Username.Contains(k) || u.DisplayName.Contains(k));
        }
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(u => u.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (total, items);
    }

    public Task<List<AppUser>> LocationSharersExcept(Guid? userId) =>
        _db.Users.Where(u => !u.IsDeleted && u.UserId != userId && u.IsLocationSharing && u.Latitude != null).ToListAsync();

    public Task<List<AppUser>> UsersByIds(IEnumerable<Guid> ids)
    {
        var set = ids.ToList();
        return _db.Users.Where(u => set.Contains(u.UserId)).ToListAsync();
    }

    public Task<List<AppUser>> SearchUsers(string keyword) =>
        _db.Users.Where(u => !u.IsDeleted && (u.Username.ToLower().Contains(keyword) || u.DisplayName.ToLower().Contains(keyword) || u.Email.Contains(keyword)))
            .Take(20).ToListAsync();

    public void AddSession(UserSession session) => _db.Sessions.Add(session);

    public Task<UserSession?> ValidSession(string? refreshToken) =>
        _db.Sessions.FirstOrDefaultAsync(s => s.RefreshToken == refreshToken && s.ExpiresAt > DateTime.UtcNow);

    public Task<UserSession?> SessionByToken(string? refreshToken) =>
        _db.Sessions.FirstOrDefaultAsync(s => s.RefreshToken == refreshToken);

    public void RemoveSession(UserSession session) => _db.Sessions.Remove(session);

    public void AddOtp(OtpChallenge otp) => _db.Otps.Add(otp);

    public Task<bool> HasValidOtp(string email, string purpose, string code) =>
        _db.Otps.AnyAsync(o => o.Email == email && o.Purpose == purpose && o.Code == code && o.ExpiresAt > DateTime.UtcNow);

    public Task<int> CountPendingReports() => _db.Reports.CountAsync(r => r.Status == "Pending");

    public Task<List<ReportRow>> RecentReports() =>
        _db.Reports.OrderByDescending(r => r.CreatedAt).Take(100).ToListAsync();

    public Task<ReportRow?> FindReport(Guid id) => _db.Reports.FirstOrDefaultAsync(r => r.ReportId == id);

    public void AddReport(ReportRow report) => _db.Reports.Add(report);

    public void AddAudit(AuditLogRow row) => _db.AuditLogs.Add(row);

    public Task<List<AuditLogRow>> RecentAudits() =>
        _db.AuditLogs.OrderByDescending(a => a.CreatedAt).Take(100).ToListAsync();
}
