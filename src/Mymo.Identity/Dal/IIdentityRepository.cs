using Mymo.Identity.Data;

namespace Mymo.Identity.Dal;

public interface IIdentityRepository
{
    Task Save();

    Task<AppUser?> FindUser(Guid id);
    Task<AppUser?> FindActiveByEmail(string email);
    Task<AppUser?> FindByEmail(string email);
    Task<bool> EmailOrUsernameTaken(string email, string username);
    void AddUser(AppUser user);
    Task<List<AppUser>> ActiveUsers();
    Task<(int Total, List<AppUser> Items)> PageUsers(string? keyword, int page, int pageSize);
    Task<List<AppUser>> LocationSharersExcept(Guid? userId);
    Task<List<AppUser>> UsersByIds(IEnumerable<Guid> ids);
    Task<List<AppUser>> SearchUsers(string keyword);

    void AddSession(UserSession session);
    Task<UserSession?> ValidSession(string? refreshToken);
    Task<UserSession?> SessionByToken(string? refreshToken);
    void RemoveSession(UserSession session);

    void AddOtp(OtpChallenge otp);
    Task<bool> HasValidOtp(string email, string purpose, string code);

    Task<int> CountPendingReports();
    Task<List<ReportRow>> RecentReports();
    Task<ReportRow?> FindReport(Guid id);
    void AddReport(ReportRow report);

    void AddAudit(AuditLogRow row);
    Task<List<AuditLogRow>> RecentAudits();
}
