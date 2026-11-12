using System.Text.Json;
using Mymo.Identity.Dal;
using Mymo.Identity.Data;
using Mymo.Shared;

namespace Mymo.Identity.Bll;

public class AdminService
{
    private readonly IIdentityRepository _data;

    public AdminService(IIdentityRepository data) => _data = data;

    public async Task<ServiceResult> Dashboard()
    {
        var users = await _data.ActiveUsers();
        return ServiceResult.Ok(ApiResponse<object>.Ok(new
        {
            users = new
            {
                total = users.Count,
                active = users.Count(u => u.Status == "Active"),
                inactive = users.Count(u => u.Status == "Inactive"),
                banned = users.Count(u => u.Status == "Banned"),
                admins = users.Count(u => u.Role == "Admin"),
                businessOwners = users.Count(u => u.Role == "Business"),
                customers = users.Count(u => u.Role == "User"),
            },
            queues = new
            {
                pendingBusinesses = 0,
                pendingPlaces = 0,
                pendingReports = await _data.CountPendingReports(),
            },
            payments = new { pendingCount = 0, pendingAmountVnd = 0, successCount = 0, successAmountVnd = 0 },
        }));
    }

    public async Task<ServiceResult> Users(string? keyword, int page, int pageSize)
    {
        var (total, items) = await _data.PageUsers(keyword, page, pageSize);
        return ServiceResult.Ok(ApiResponse<object>.Ok(new
        {
            totalCount = total,
            items = items.Select(u => new
            {
                u.UserId,
                u.Username,
                u.Email,
                u.DisplayName,
                u.PhoneNumber,
                roleName = u.Role,
                u.Status,
                u.CreatedAt,
                u.LastActiveAt,
            }),
        }));
    }

    public async Task<ServiceResult> SetStatus(Guid id, JsonElement body)
    {
        var user = await _data.FindUser(id);
        if (user == null) return ServiceResult.NotFound(ApiResponse<object>.Fail("User not found"));
        user.Status = JsonBody.Text(body, "status") ?? user.Status;
        _data.AddAudit(new AuditLogRow
        {
            AuditLogId = Guid.NewGuid(),
            UserId = user.UserId,
            Action = "Update",
            EntityName = "User",
            EntityId = user.UserId,
            NewValue = user.Status,
            CreatedAt = DateTime.UtcNow,
        });
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<bool>.Ok(true));
    }

    public async Task<ServiceResult> Reports()
    {
        var reports = await _data.RecentReports();
        var users = await _data.UsersByIds(reports.Select(r => r.ReporterId));
        var data = reports.Select(r => new
        {
            r.ReportId,
            r.ReporterId,
            reporterDisplayName = users.FirstOrDefault(u => u.UserId == r.ReporterId)?.DisplayName,
            r.TargetType,
            r.TargetId,
            r.TargetSummary,
            r.Reason,
            r.Status,
            r.CreatedAt,
        });
        return ServiceResult.Ok(ApiResponse<object>.Ok(data));
    }

    public async Task<ServiceResult> SetReportStatus(Guid id, JsonElement body)
    {
        var report = await _data.FindReport(id);
        if (report == null) return ServiceResult.NotFound(ApiResponse<object>.Fail("Report not found"));
        report.Status = JsonBody.Text(body, "status") ?? report.Status;
        await _data.Save();
        return ServiceResult.Ok(ApiResponse<object>.Ok(report));
    }

    public async Task<ServiceResult> AuditLogs()
    {
        var items = await _data.RecentAudits();
        var since = DateTime.UtcNow.AddHours(-24);
        return ServiceResult.Ok(ApiResponse<object>.Ok(new
        {
            totalCount = items.Count,
            summary = new
            {
                last24Hours = items.Count(a => a.CreatedAt >= since),
                createCount = items.Count(a => a.Action == "Create"),
                updateCount = items.Count(a => a.Action == "Update"),
                deleteCount = items.Count(a => a.Action == "Delete"),
            },
            items = items.Select(a => new
            {
                a.AuditLogId,
                a.UserId,
                actorDisplayName = (string?)null,
                actorEmail = (string?)null,
                a.Action,
                a.EntityName,
                a.EntityId,
                a.OldValue,
                a.NewValue,
                a.CreatedAt,
            }),
        }));
    }

    public ServiceResult SettingsOverview() => ServiceResult.Ok(ApiResponse<object>.Ok(new
    {
        bankTransfer = new
        {
            bankName = "VietinBank",
            bankBin = "970415",
            accountNumberMasked = "****",
            accountName = "MYMO LOCAL",
            sepayConfigured = false,
            allowManualConfirm = true,
        },
        jwt = new { issuer = "MYMO", audience = "MYMO", accessTokenExpirationMinutes = 10080, refreshTokenExpirationDays = 7 },
        integrations = new { cloudinaryConfigured = false, geminiConfigured = false, weatherConfigured = true },
    }));

    public ServiceResult SaveBank() => ServiceResult.Ok(ApiResponse<bool>.Ok(true, "Đã lưu trên máy local"));

    public async Task<ServiceResult> CreateReport(JsonElement body)
    {
        _data.AddReport(new ReportRow
        {
            ReportId = Guid.NewGuid(),
            ReporterId = Guid.Parse(JsonBody.Text(body, "reporterId") ?? Guid.Empty.ToString()),
            TargetType = JsonBody.Text(body, "targetType") ?? "Post",
            TargetId = Guid.Parse(JsonBody.Text(body, "targetId") ?? Guid.Empty.ToString()),
            Reason = JsonBody.Text(body, "reason") ?? "",
            TargetSummary = JsonBody.Text(body, "targetSummary"),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
        });
        await _data.Save();
        return ServiceResult.Empty();
    }
}
