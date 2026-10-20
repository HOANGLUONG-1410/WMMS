using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WMMS.Application.DTOs.Auth;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Enums;
using WMMS.Domain.Exceptions;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Security;

/// <summary>
/// Triển khai Dịch vụ Xác thực và Quản lý Phiên (Phân hệ AUT - UC-01 đến UC-05, JOB-01, Report 4 Class Diagram <c>AuthService</c>).
/// Bao gồm:
/// - UC-01: Đăng nhập, tự động khóa 30 phút sau 5 lần nhập sai liên tiếp (BR-SEC-02), cấp JWT + Refresh Token.
/// - Token Rotation: Làm mới JWT bằng Refresh Token (băm SHA-256), thu hồi token cũ.
/// - UC-02: Đăng xuất, thu hồi Refresh Token và ghi nhận Audit Log.
/// - UC-03: Đổi mật khẩu cá nhân, kiểm tra mật khẩu hiện tại, thu hồi toàn bộ phiên cũ.
/// - UC-04: Quên mật khẩu, sinh OTP 6 chữ số &amp; Reset Token (15 phút), giới hạn 3 lần / 10 phút, trả MSG05 chống dò quét email.
/// - UC-05: Xác nhận đặt lại mật khẩu bằng OTP hoặc Reset Token, mở khóa tài khoản nếu đang bị khóa tạm thời.
/// </summary>
public class AuthService : IAuthService
{
    private readonly WmmsDbContext _context;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    private const int MaxFailedLoginAttempts = 5;
    private const int LockoutDurationMinutes = 30;
    private const int PasswordResetExpiryMinutes = 15;
    private const int MaxResetRequestsPerWindow = 3;
    private const int ResetRateLimitWindowMinutes = 10;

    public AuthService(
        WmmsDbContext context,
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordHasher passwordHasher,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _context = context;
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// UC-01: Xác thực thông tin đăng nhập (Username hoặc Email + Password).
    /// Kiểm tra trạng thái BANNED/INACTIVE (MSG04), kiểm tra khóa tạm thời LOCKED (MSG03),
    /// xác minh mật khẩu BCrypt (BR-SEC-01), tăng bộ đếm nếu sai và khóa 30 phút ở lần thứ 5 (BR-SEC-02).
    /// </summary>
    public async Task<AuthResponseDto> AuthenticateAsync(
        LoginRequestDto request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var normalizedInput = request.UsernameOrEmail.Trim();

        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(
                u => u.Username == normalizedInput || u.Email == normalizedInput,
                cancellationToken);

        // Chống dò quét tài khoản: trả thông báo chuẩn MSG02 nếu không tìm thấy user
        if (user is null)
        {
            throw AppException.Unauthorized(
                "Invalid username/email or password.",
                MessageCodes.Msg02InvalidCredentials);
        }

        // Kiểm tra nếu tài khoản bị Admin khóa vĩnh viễn (BANNED) hoặc vô hiệu hóa (INACTIVE) -> MSG04
        if (user.Status == UserStatusEnum.BANNED || user.Status == UserStatusEnum.INACTIVE)
        {
            throw AppException.Forbidden(
                $"Your account is currently {user.Status}. Please contact the System Administrator.",
                MessageCodes.Msg04AccountBannedOrInactive);
        }

        // Kiểm tra nếu tài khoản đang trong thời gian khóa 30 phút (LOCKED) -> MSG03
        if (user.IsLockedOut())
        {
            var remainingMinutes = Math.Max(1, (int)Math.Ceiling((user.LockedUntil!.Value - DateTime.UtcNow).TotalMinutes));
            throw AppException.Locked(
                $"Account is temporarily locked due to 5 consecutive failed login attempts. Please try again in {remainingMinutes} minute(s).",
                MessageCodes.Msg03AccountLocked);
        }

        // Nếu thời gian khóa 30 phút đã trôi qua, tự động mở khóa trước khi kiểm tra mật khẩu (JOB-01 inline)
        if (user.Status == UserStatusEnum.LOCKED && !user.IsLockedOut())
        {
            user.ResetFailedCount();
        }

        // Xác minh mật khẩu bằng BCrypt
        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            user.RecordFailedLogin(MaxFailedLoginAttempts, LockoutDurationMinutes);

            var isNowLocked = user.Status == UserStatusEnum.LOCKED;
            await WriteAuditLogAsync(
                user.UserId,
                isNowLocked ? AuditActionConstants.AccountLocked : AuditActionConstants.LoginFailed,
                nameof(User),
                user.UserId.ToString(),
                beforeState: null,
                afterState: JsonSerializer.Serialize(new { user.FailedLoginCount, Status = user.Status.ToString(), user.LockedUntil }),
                reason: isNowLocked
                    ? "Account automatically locked for 30 minutes after 5 consecutive failed login attempts (BR-SEC-02)."
                    : $"Failed login attempt #{user.FailedLoginCount}.",
                ipAddress: ipAddress,
                cancellationToken: cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            if (isNowLocked)
            {
                throw AppException.Locked(
                    "Account has been temporarily locked for 30 minutes due to 5 consecutive failed login attempts.",
                    MessageCodes.Msg03AccountLocked);
            }

            throw AppException.Unauthorized(
                "Invalid username/email or password.",
                MessageCodes.Msg02InvalidCredentials);
        }

        // Đăng nhập thành công: Reset bộ đếm lỗi, cập nhật thời điểm đăng nhập
        user.ResetFailedCount();
        user.LastLoginAt = DateTime.UtcNow;

        var roleCodes = user.UserRoles
            .Select(ur => ur.Role.RoleCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Sinh JWT Access Token và Refresh Token (7 ngày hoặc 30 ngày nếu RememberMe = true)
        var (accessToken, accessExpiresAt) = _jwtTokenGenerator.GenerateAccessToken(user, roleCodes);
        var (rawRefreshToken, refreshTokenHash, refreshExpiresAt) = _jwtTokenGenerator.GenerateRefreshToken(request.RememberMe);

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = refreshTokenHash,
            ExpiresAt = refreshExpiresAt,
            IsRevoked = false,
            CreatedByIp = ipAddress,
            CreatedAt = DateTime.UtcNow
        };

        await _context.RefreshTokens.AddAsync(refreshTokenEntity, cancellationToken);

        await WriteAuditLogAsync(
            user.UserId,
            AuditActionConstants.LoginSuccess,
            nameof(User),
            user.UserId.ToString(),
            beforeState: null,
            afterState: JsonSerializer.Serialize(new { user.Username, Roles = roleCodes, request.RememberMe }),
            reason: "User logged in successfully (UC-01).",
            ipAddress: ipAddress,
            cancellationToken: cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            AccessTokenExpiresAt = accessExpiresAt,
            RefreshTokenExpiresAt = refreshExpiresAt,
            User = MapToCurrentUserProfile(user)
        };
    }

    /// <summary>
    /// Xoay vòng Refresh Token (Token Rotation - UC-01, BR-SEC-01):
    /// Kiểm tra bản băm SHA-256 của Refresh Token, thu hồi token hiện tại và cấp cặp JWT + Refresh Token mới.
    /// </summary>
    public async Task<AuthResponseDto> RefreshTokenAsync(
        string rawRefreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            throw AppException.Unauthorized("Refresh token is required.", MessageCodes.Msg12InvalidToken);
        }

        var tokenHash = _jwtTokenGenerator.ComputeSha256Hash(rawRefreshToken);

        var existingToken = await _context.RefreshTokens
            .Include(rt => rt.User)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (existingToken is null || !existingToken.IsActive)
        {
            throw AppException.Unauthorized("Refresh token is invalid, expired, or has been revoked.", MessageCodes.Msg12InvalidToken);
        }

        var user = existingToken.User;
        if (user.Status != UserStatusEnum.ACTIVE || user.IsLockedOut())
        {
            existingToken.Revoke();
            await _context.SaveChangesAsync(cancellationToken);
            throw AppException.Forbidden("User account is not active.", MessageCodes.Msg04AccountBannedOrInactive);
        }

        var roleCodes = user.UserRoles
            .Select(ur => ur.Role.RoleCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var (newAccessToken, accessExpiresAt) = _jwtTokenGenerator.GenerateAccessToken(user, roleCodes);
        var (newRawRefresh, newRefreshHash, refreshExpiresAt) = _jwtTokenGenerator.GenerateRefreshToken(false);

        // Thu hồi Refresh Token cũ và đánh dấu mã thay thế (Token Rotation)
        existingToken.Revoke(newRefreshHash);

        var newRefreshTokenEntity = new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = newRefreshHash,
            ExpiresAt = refreshExpiresAt,
            IsRevoked = false,
            CreatedByIp = ipAddress,
            CreatedAt = DateTime.UtcNow
        };

        await _context.RefreshTokens.AddAsync(newRefreshTokenEntity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new AuthResponseDto
        {
            AccessToken = newAccessToken,
            RefreshToken = newRawRefresh,
            AccessTokenExpiresAt = accessExpiresAt,
            RefreshTokenExpiresAt = refreshExpiresAt,
            User = MapToCurrentUserProfile(user)
        };
    }

    /// <summary>
    /// UC-02: Đăng xuất hệ thống - Thu hồi Refresh Token hiện tại và lưu vết vào <c>audit_logs</c>.
    /// </summary>
    public async Task RevokeTokenAsync(
        string? rawRefreshToken,
        long? currentUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        long? resolvedUserId = currentUserId;

        if (!string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            var tokenHash = _jwtTokenGenerator.ComputeSha256Hash(rawRefreshToken);
            var existingToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

            if (existingToken is not null && !existingToken.IsRevoked)
            {
                existingToken.Revoke();
                resolvedUserId ??= existingToken.UserId;
            }
        }
        else if (currentUserId.HasValue)
        {
            // Nếu client không gửi rawRefreshToken nhưng có JWT hợp lệ, thu hồi các token đang active của user
            var activeTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == currentUserId.Value && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow)
                .ToListAsync(cancellationToken);

            foreach (var token in activeTokens)
            {
                token.Revoke();
            }
        }

        if (resolvedUserId.HasValue)
        {
            await WriteAuditLogAsync(
                resolvedUserId.Value,
                AuditActionConstants.Logout,
                nameof(User),
                resolvedUserId.Value.ToString(),
                beforeState: null,
                afterState: null,
                reason: "User logged out and revoked active session token (UC-02).",
                ipAddress: ipAddress,
                cancellationToken: cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Lấy thông tin hồ sơ người dùng đang đăng nhập kèm danh sách quyền đa vai trò (Global Role Selector).
    /// </summary>
    public async Task<CurrentUserProfileDto> GetCurrentUserProfileAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            throw AppException.NotFound("Authenticated user profile not found.");
        }

        return MapToCurrentUserProfile(user);
    }

    /// <summary>
    /// UC-03: Đổi mật khẩu cá nhân.
    /// Xác minh mật khẩu hiện tại, băm mật khẩu mới bằng BCrypt (work factor 12),
    /// thu hồi toàn bộ các Refresh Token đang hoạt động để buộc đăng nhập lại trên thiết bị khác.
    /// </summary>
    public async Task ChangePasswordAsync(
        long userId,
        ChangePasswordRequestDto request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            throw AppException.NotFound("User account not found.");
        }

        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            throw AppException.Unauthorized("Current password is incorrect.", MessageCodes.Msg02InvalidCredentials);
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        // Thu hồi toàn bộ Refresh Token đang hoạt động của người dùng (BR-SEC-01)
        foreach (var token in user.RefreshTokens.Where(rt => !rt.IsRevoked))
        {
            token.Revoke();
        }

        await WriteAuditLogAsync(
            user.UserId,
            AuditActionConstants.PasswordChanged,
            nameof(User),
            user.UserId.ToString(),
            beforeState: null,
            afterState: JsonSerializer.Serialize(new { PasswordChangedAt = DateTime.UtcNow }),
            reason: "User changed personal password and revoked active sessions (UC-03).",
            ipAddress: ipAddress,
            cancellationToken: cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// UC-04: Yêu cầu khôi phục mật khẩu khi quên mật khẩu.
    /// - Áp dụng Rate-limit: Tối đa 3 yêu cầu trong vòng 10 phút cho mỗi tài khoản.
    /// - Sinh đồng thời chuỗi Reset Token (URL-safe) và mã OTP 6 chữ số có hạn 15 phút (BR-SEC-03).
    /// - Chỉ lưu bản băm SHA-256 vào bảng <c>password_reset_tokens</c>.
    /// - Luôn trả về thông báo chung MSG05 để chống dò quét email (Account Enumeration).
    /// </summary>
    public async Task<ForgotPasswordResponseDto> RequestPasswordResetAsync(
        ForgotPasswordRequestDto request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var genericMessage = "If an account with that email exists, a password reset link and 6-digit OTP have been dispatched (MSG05).";
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        // Nếu không tìm thấy email hoặc tài khoản bị BANNED/INACTIVE, vẫn trả thông báo thành công giả lập (MSG05) để chống dò quét
        if (user is null || user.Status == UserStatusEnum.BANNED || user.Status == UserStatusEnum.INACTIVE)
        {
            return new ForgotPasswordResponseDto
            {
                Message = genericMessage,
                ExpiresInMinutes = PasswordResetExpiryMinutes
            };
        }

        // Kiểm tra giới hạn tần suất (Rate Limiting): Tối đa 3 yêu cầu trong vòng 10 phút gần nhất (UC-04 Exception E2)
        var rateLimitCutoff = DateTime.UtcNow.AddMinutes(-ResetRateLimitWindowMinutes);
        var recentRequestsCount = await _context.PasswordResetTokens
            .CountAsync(prt => prt.UserId == user.UserId && prt.CreatedAt >= rateLimitCutoff, cancellationToken);

        if (recentRequestsCount >= MaxResetRequestsPerWindow)
        {
            throw AppException.TooManyRequests(
                "Too many password reset requests. Maximum 3 requests allowed within 10 minutes. Please try again later.",
                MessageCodes.Msg09RateLimitExceeded);
        }

        // Vô hiệu hóa các mã khôi phục cũ chưa dùng của người dùng này
        var previousUnusedTokens = await _context.PasswordResetTokens
            .Where(prt => prt.UserId == user.UserId && !prt.IsUsed && prt.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var oldToken in previousUnusedTokens)
        {
            oldToken.MarkAsUsed();
        }

        // Sinh Reset Token và mã OTP 6 chữ số mới (hiệu lực 15 phút - BR-SEC-03)
        var (rawResetToken, resetTokenHash, _) = _jwtTokenGenerator.GenerateRefreshToken(false);
        var otpCode = _jwtTokenGenerator.GenerateSixDigitOtp();
        var otpHash = _jwtTokenGenerator.ComputeSha256Hash(otpCode);
        var expiresAt = DateTime.UtcNow.AddMinutes(PasswordResetExpiryMinutes);

        var resetEntity = new PasswordResetToken
        {
            UserId = user.UserId,
            TokenHash = resetTokenHash,
            OtpHash = otpHash,
            ExpiresAt = expiresAt,
            IsUsed = false,
            RequestedByIp = ipAddress,
            CreatedAt = DateTime.UtcNow
        };

        await _context.PasswordResetTokens.AddAsync(resetEntity, cancellationToken);

        await WriteAuditLogAsync(
            user.UserId,
            AuditActionConstants.PasswordResetRequested,
            nameof(PasswordResetToken),
            user.UserId.ToString(),
            beforeState: null,
            afterState: JsonSerializer.Serialize(new { ExpiresAt = expiresAt, RequestedByIp = ipAddress }),
            reason: "Password reset OTP & token generated (15-min TTL, UC-04).",
            ipAddress: ipAddress,
            cancellationToken: cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        await _emailService.SendPasswordResetEmailAsync(
            user.Email,
            user.FullName,
            rawResetToken,
            otpCode,
            expiresAt,
            cancellationToken);

        var isDev = string.Equals(
            _configuration["ASPNETCORE_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
            "Development",
            StringComparison.OrdinalIgnoreCase);

        return new ForgotPasswordResponseDto
        {
            Message = genericMessage,
            ExpiresInMinutes = PasswordResetExpiryMinutes,
            DevResetToken = isDev ? rawResetToken : null,
            DevOtpCode = isDev ? otpCode : null
        };
    }

    /// <summary>
    /// UC-05: Xác nhận đặt lại mật khẩu bằng Reset Token hoặc mã OTP 6 chữ số.
    /// - Kiểm tra tính hợp lệ và thời hạn 15 phút của Token/OTP (MSG06 nếu sai hoặc hết hạn).
    /// - Cập nhật mật khẩu mới băm BCrypt, đặt lại <c>failed_login_count = 0</c>, mở khóa nếu đang LOCKED.
    /// - Đánh dấu mã khôi phục đã sử dụng (<c>is_used = true</c>) và thu hồi toàn bộ Refresh Token cũ.
    /// </summary>
    public async Task ConfirmPasswordResetAsync(
        ResetPasswordRequestDto request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var inputHash = _jwtTokenGenerator.ComputeSha256Hash(request.TokenOrOtp.Trim());

        var user = await _context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (user is null)
        {
            throw new AppException(
                "Invalid or expired password reset token / OTP code.",
                400,
                MessageCodes.Msg06ResetTokenInvalidOrExpired);
        }

        var matchingResetToken = await _context.PasswordResetTokens
            .Where(prt => prt.UserId == user.UserId && !prt.IsUsed && prt.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(prt => prt.CreatedAt)
            .FirstOrDefaultAsync(
                prt => prt.TokenHash == inputHash || prt.OtpHash == inputHash,
                cancellationToken);

        if (matchingResetToken is null)
        {
            throw new AppException(
                "Invalid or expired password reset token / OTP code (MSG06).",
                400,
                MessageCodes.Msg06ResetTokenInvalidOrExpired);
        }

        // Tiêu thụ token (One-time use - BR-SEC-03)
        matchingResetToken.MarkAsUsed();

        // Cập nhật mật khẩu mới và mở khóa tạm thời nếu đang LOCKED
        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.ResetFailedCount();
        user.UpdatedAt = DateTime.UtcNow;

        // Thu hồi toàn bộ Refresh Token cũ của tài khoản
        foreach (var refreshToken in user.RefreshTokens.Where(rt => !rt.IsRevoked))
        {
            refreshToken.Revoke();
        }

        await WriteAuditLogAsync(
            user.UserId,
            AuditActionConstants.PasswordResetConfirmed,
            nameof(User),
            user.UserId.ToString(),
            beforeState: null,
            afterState: JsonSerializer.Serialize(new { ResetTokenId = matchingResetToken.ResetTokenId, ResetCompletedAt = DateTime.UtcNow }),
            reason: "Password successfully reset via token/OTP and all prior sessions revoked (UC-05).",
            ipAddress: ipAddress,
            cancellationToken: cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// JOB-01: Tác vụ nền tự động mở khóa cho các tài khoản đang ở trạng thái LOCKED khi đã hết hạn 30 phút (<c>locked_until &lt;= UtcNow</c>).
    /// </summary>
    public async Task<int> UnlockExpiredAccountsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiredLockedUsers = await _context.Users
            .Where(u => u.Status == UserStatusEnum.LOCKED && u.LockedUntil != null && u.LockedUntil <= now)
            .ToListAsync(cancellationToken);

        if (expiredLockedUsers.Count == 0)
        {
            return 0;
        }

        foreach (var user in expiredLockedUsers)
        {
            user.ResetFailedCount();
            user.UpdatedAt = now;

            await WriteAuditLogAsync(
                user.UserId,
                AuditActionConstants.AccountAutoUnlocked,
                nameof(User),
                user.UserId.ToString(),
                beforeState: JsonSerializer.Serialize(new { Status = UserStatusEnum.LOCKED.ToString() }),
                afterState: JsonSerializer.Serialize(new { Status = UserStatusEnum.ACTIVE.ToString(), FailedLoginCount = 0 }),
                reason: "Account automatically unlocked by background scheduler after 30-minute lockout expiration (JOB-01).",
                ipAddress: "SYSTEM_SCHEDULER",
                cancellationToken: cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("[JOB-01] Auto-unlocked {Count} expired locked user account(s).", expiredLockedUsers.Count);
        return expiredLockedUsers.Count;
    }

    private static CurrentUserProfileDto MapToCurrentUserProfile(User user)
    {
        var roleScopes = user.UserRoles
            .Where(ur => ur.Role is not null)
            .Select(ur => new UserRoleScopeDto
            {
                RoleId = ur.RoleId,
                RoleCode = ur.Role.RoleCode,
                RoleName = ur.Role.RoleName,
                PermissionsJson = ur.Role.PermissionsJson,
                AssignedAt = ur.AssignedAt
            })
            .OrderBy(r => r.RoleId)
            .ToList();

        return new CurrentUserProfileDto
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            Status = user.Status.ToString(),
            LastLoginAt = user.LastLoginAt,
            Roles = roleScopes.Select(r => r.RoleCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            RoleScopes = roleScopes
        };
    }

    private async Task WriteAuditLogAsync(
        long? userId,
        string actionType,
        string entityName,
        string entityId,
        string? beforeState,
        string? afterState,
        string? reason,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var auditLog = new AuditLog
        {
            UserId = userId,
            ActionType = actionType,
            EntityName = entityName,
            EntityId = entityId,
            BeforeState = beforeState,
            AfterState = afterState,
            Reason = reason,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        };

        await _context.AuditLogs.AddAsync(auditLog, cancellationToken);
    }
}
