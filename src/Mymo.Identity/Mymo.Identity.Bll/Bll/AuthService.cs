using Mymo.Identity.Dal;
using Mymo.Identity.Data;
using Mymo.Shared;

namespace Mymo.Identity.Bll;

public class AuthService
{
    private readonly IIdentityRepository _data;
    private readonly IConfiguration _config;

    public AuthService(IIdentityRepository data, IConfiguration config)
    {
        _data = data;
        _config = config;
    }

    public async Task<ServiceResult> SendOtp(OtpBody body, string purpose)
    {
        var email = (body.Email ?? "").Trim().ToLowerInvariant();
        if (email.Length < 3) return ServiceResult.BadRequest(new { message = "Email không hợp lệ" });
        var code = Random.Shared.Next(100000, 999999).ToString();
        _data.AddOtp(new OtpChallenge
        {
            Id = Guid.NewGuid(),
            Email = email,
            Purpose = purpose,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
        });
        await _data.Save();
        Console.WriteLine($"[otp] {purpose} {email} = {code}");
        return ServiceResult.Ok(new { message = "Verification code sent.", devCode = code });
    }

    public async Task<ServiceResult> Register(AuthBody body)
    {
        var email = (body.Email ?? "").Trim().ToLowerInvariant();
        var username = (body.Username ?? "").Trim();
        if (email.Length == 0 || username.Length == 0 || string.IsNullOrWhiteSpace(body.Password))
            return ServiceResult.BadRequest(new { message = "Thiếu thông tin đăng ký" });
        if (!await ValidOtp(email, "register", body.OtpCode))
            return ServiceResult.BadRequest(new { message = "Mã OTP không đúng hoặc đã hết hạn" });
        if (await _data.EmailOrUsernameTaken(email, username))
            return ServiceResult.BadRequest(new { message = "Email hoặc username đã tồn tại" });

        var user = new AppUser
        {
            UserId = Guid.NewGuid(),
            Username = username,
            DisplayName = string.IsNullOrWhiteSpace(body.DisplayName) ? username : body.DisplayName.Trim(),
            Email = email,
            PhoneNumber = body.PhoneNumber,
            Gender = body.Gender,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(body.Password),
            Role = "User",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            IsLocationSharing = true,
        };
        if (DateOnly.TryParse(body.DateOfBirth, out var dob)) user.DateOfBirth = dob;
        _data.AddUser(user);
        var refresh = IssueSession(user.UserId);
        await _data.Save();
        return ServiceResult.Ok(IdentityMaps.Token(_config, user, refresh));
    }

    public async Task<ServiceResult> Login(AuthBody body)
    {
        var email = (body.Email ?? "").Trim().ToLowerInvariant();
        var user = await _data.FindActiveByEmail(email);
        if (user == null || string.IsNullOrWhiteSpace(body.Password) || !BCrypt.Net.BCrypt.Verify(body.Password, user.PasswordHash))
            return ServiceResult.Unauthorized(new { message = "Email hoặc mật khẩu không đúng" });
        if (user.Status == "Banned")
            return ServiceResult.Unauthorized(new { message = "Tài khoản đã bị khóa" });
        user.LastActiveAt = DateTime.UtcNow;
        var refresh = IssueSession(user.UserId);
        await _data.Save();
        return ServiceResult.Ok(IdentityMaps.Token(_config, user, refresh));
    }

    public async Task<ServiceResult> ResetPassword(AuthBody body)
    {
        var email = (body.Email ?? "").Trim().ToLowerInvariant();
        if (!await ValidOtp(email, "reset", body.OtpCode))
            return ServiceResult.BadRequest(new { message = "Mã OTP không đúng hoặc đã hết hạn" });
        var user = await _data.FindByEmail(email);
        if (user == null) return ServiceResult.BadRequest(new { message = "Không tìm thấy tài khoản" });
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(body.NewPassword ?? "");
        await _data.Save();
        return ServiceResult.Ok(new { message = "Password updated." });
    }

    public async Task<ServiceResult> Refresh(RefreshBody body)
    {
        var session = await _data.ValidSession(body.RefreshToken);
        if (session == null) return ServiceResult.Unauthorized();
        var user = await _data.FindUser(session.UserId);
        if (user == null) return ServiceResult.Unauthorized();
        return ServiceResult.Ok(IdentityMaps.Token(_config, user, session.RefreshToken));
    }

    public async Task<ServiceResult> Logout(RefreshBody body)
    {
        var session = await _data.SessionByToken(body.RefreshToken);
        if (session != null) _data.RemoveSession(session);
        await _data.Save();
        return ServiceResult.NoContent();
    }

    public async Task<ServiceResult> Me(ClaimsPrincipal principal)
    {
        var id = HttpUser.Id(principal);
        var user = id == null ? null : await _data.FindUser(id.Value);
        return user == null ? ServiceResult.NotFound("User not found") : ServiceResult.Ok(IdentityMaps.Profile(user, 0, 0, 0));
    }

    async Task<bool> ValidOtp(string email, string purpose, string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        return await _data.HasValidOtp(email, purpose, code.Trim());
    }

    string IssueSession(Guid userId)
    {
        var refresh = Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Convert.ToHexString(Guid.NewGuid().ToByteArray());
        _data.AddSession(new UserSession
        {
            SessionId = Guid.NewGuid(),
            UserId = userId,
            RefreshToken = refresh,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        });
        return refresh;
    }
}
