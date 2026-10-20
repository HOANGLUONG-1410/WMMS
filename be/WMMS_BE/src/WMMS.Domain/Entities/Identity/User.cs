using WMMS.Domain.Common;
using WMMS.Domain.Enums;

namespace WMMS.Domain.Entities.Identity;

/// <summary>
/// Thực thể Tài khoản người dùng (<c>users</c>) - Phân hệ AUT &amp; ADM (Report 3 &amp; Report 4).
/// Quản lý thông tin định danh, mật khẩu băm BCrypt (BR-SEC-01), trạng thái tài khoản,
/// cơ chế khóa tự động sau 5 lần đăng nhập sai liên tiếp trong 30 phút (UC-01, BR-SEC-02),
/// và liên kết đa vai trò (Multi-Role RBAC - GBR-04).
/// </summary>
public class User : BaseEntity, IAuditableEntity
{
    /// <summary>Khóa chính định danh người dùng (<c>user_id</c>).</summary>
    public long UserId { get; set; }

    /// <summary>Tên đăng nhập duy nhất (<c>username</c>) - tối đa 100 ký tự.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Địa chỉ email duy nhất (<c>email</c>) dùng cho đăng nhập và khôi phục mật khẩu (UC-04).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Chuỗi băm mật khẩu chuẩn BCrypt work factor &gt;= 12 (<c>password_hash</c>, BR-SEC-01).</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Họ và tên đầy đủ của nhân sự (<c>full_name</c>).</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Số điện thoại liên hệ (<c>phone</c>).</summary>
    public string? Phone { get; set; }

    /// <summary>Trạng thái tài khoản: ACTIVE, INACTIVE, BANNED, LOCKED (<c>status</c>).</summary>
    public UserStatusEnum Status { get; set; } = UserStatusEnum.ACTIVE;

    /// <summary>Số lần đăng nhập sai liên tiếp hiện tại (<c>failed_login_count</c>). Đạt 5 lần sẽ khóa 30 phút.</summary>
    public int FailedLoginCount { get; set; } = 0;

    /// <summary>Thời điểm hết hạn khóa tạm thời (<c>locked_until</c> - UTC). Null nếu không bị khóa.</summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>Thời điểm đăng nhập thành công gần nhất (<c>last_login_at</c> - UTC).</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Danh sách vai trò được gán cho người dùng (Multi-Role 1..* - GBR-04).</summary>
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    /// <summary>Danh sách các Refresh Token của người dùng (<c>refresh_tokens</c>).</summary>
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    /// <summary>Danh sách các yêu cầu khôi phục mật khẩu (<c>password_reset_tokens</c> - UC-04, UC-05).</summary>
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();

    /// <summary>Lịch sử nhật ký kiểm toán do người dùng này thực hiện (<c>audit_logs</c>).</summary>
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    /// <summary>
    /// Kiểm tra tài khoản có đang trong thời gian bị khóa tạm thời hay không (UC-01).
    /// </summary>
    public bool IsLockedOut() => LockedUntil.HasValue && LockedUntil.Value > DateTime.UtcNow;

    /// <summary>
    /// Tăng bộ đếm đăng nhập sai; nếu chạm ngưỡng 5 lần thì chuyển trạng thái sang LOCKED trong 30 phút (UC-01).
    /// </summary>
    public void RecordFailedLogin(int maxAttempts = 5, int lockMinutes = 30)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= maxAttempts)
        {
            Status = UserStatusEnum.LOCKED;
            LockedUntil = DateTime.UtcNow.AddMinutes(lockMinutes);
        }
    }

    /// <summary>
    /// Đặt lại bộ đếm đăng nhập sai và mở khóa tạm thời khi đăng nhập thành công hoặc sau khi hết hạn khóa (UC-01, JOB-01).
    /// </summary>
    public void ResetFailedCount()
    {
        FailedLoginCount = 0;
        LockedUntil = null;
        if (Status == UserStatusEnum.LOCKED)
        {
            Status = UserStatusEnum.ACTIVE;
        }
    }
}
