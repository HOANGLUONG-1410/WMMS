namespace WMMS.Application.DTOs.Auth;

/// <summary>
/// DTO Yêu cầu đăng nhập hệ thống (UC-01: Log In System).
/// Hỗ trợ đăng nhập bằng cả Username hoặc Email kèm tùy chọn duy trì đăng nhập (RememberMe).
/// </summary>
public class LoginRequestDto
{
    /// <summary>Tên đăng nhập hoặc địa chỉ Email đã đăng ký (<c>usernameOrEmail</c>).</summary>
    public string UsernameOrEmail { get; set; } = string.Empty;

    /// <summary>Mật khẩu người dùng (<c>password</c>).</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Tùy chọn duy trì phiên đăng nhập (nếu bật: Refresh Token có hạn 30 ngày thay vì 7 ngày - UC-01).</summary>
    public bool RememberMe { get; set; } = false;
}

/// <summary>
/// DTO Kết quả xác thực thành công (UC-01, Report 4 Class Diagram <c>AuthResponseDto</c>).
/// Trả về JWT Access Token, Refresh Token, thông tin hồ sơ người dùng và danh sách đa vai trò (Global Role Selector).
/// </summary>
public class AuthResponseDto
{
    /// <summary>JWT Access Token dùng cho header <c>Authorization: Bearer {token}</c>.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Chuỗi Refresh Token dùng để cấp lại Access Token mới khi hết hạn (cũng được đính kèm trong HttpOnly Cookie).</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>Thời điểm hết hạn của Access Token (UTC).</summary>
    public DateTime AccessTokenExpiresAt { get; set; }

    /// <summary>Thời điểm hết hạn của Refresh Token (UTC).</summary>
    public DateTime RefreshTokenExpiresAt { get; set; }

    /// <summary>Thông tin tóm tắt tài khoản và danh sách vai trò hỗ trợ Global Role Selector trên giao diện.</summary>
    public CurrentUserProfileDto User { get; set; } = null!;
}

/// <summary>
/// DTO Thông tin người dùng hiện tại đang đăng nhập (<c>GET /api/v1/auth/me</c>) và danh sách quyền đa vai trò (GBR-04).
/// </summary>
public class CurrentUserProfileDto
{
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Danh sách mã vai trò (ví dụ: ["ADMIN", "PLANNER"]) phục vụ Global Role Workspace Switcher trên Screen Flow.</summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>Danh sách chi tiết các vai trò kèm JSON phân quyền cụ thể của từng vai trò.</summary>
    public List<UserRoleScopeDto> RoleScopes { get; set; } = new();
}

/// <summary>
/// DTO Chi tiết vai trò và phạm vi quyền hạn được gán cho tài khoản.
/// </summary>
public class UserRoleScopeDto
{
    public int RoleId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string PermissionsJson { get; set; } = "{}";
    public DateTime AssignedAt { get; set; }
}

/// <summary>
/// DTO Yêu cầu làm mới hoặc thu hồi Refresh Token (UC-01, UC-02).
/// Nếu client không truyền trong body, API sẽ tự động đọc từ HttpOnly Cookie <c>wmms_refresh_token</c>.
/// </summary>
public class RefreshTokenRequestDto
{
    public string? RefreshToken { get; set; }
}

/// <summary>
/// DTO Yêu cầu đổi mật khẩu cá nhân khi đang đăng nhập (UC-03: Change Personal Password).
/// </summary>
public class ChangePasswordRequestDto
{
    /// <summary>Mật khẩu hiện tại để xác minh chính chủ.</summary>
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>Mật khẩu mới (tối thiểu 8 ký tự, gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt).</summary>
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>Xác nhận lại mật khẩu mới.</summary>
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

/// <summary>
/// DTO Yêu cầu khôi phục mật khẩu khi quên mật khẩu (UC-04: Request Password Reset).
/// </summary>
public class ForgotPasswordRequestDto
{
    /// <summary>Địa chỉ Email đã đăng ký với tài khoản.</summary>
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// DTO Phản hồi khi yêu cầu đặt lại mật khẩu (UC-04).
/// Luôn trả về thông báo chung MSG05 để chống dò quét tài khoản (Account Enumeration).
/// Trong môi trường Development, trả kèm mã OTP/ResetToken thử nghiệm để FE/QA dễ kiểm thử.
/// </summary>
public class ForgotPasswordResponseDto
{
    public string Message { get; set; } = string.Empty;
    public int ExpiresInMinutes { get; set; } = 15;
    public string? DevResetToken { get; set; }
    public string? DevOtpCode { get; set; }
}

/// <summary>
/// DTO Xác nhận đặt lại mật khẩu mới bằng Reset Token hoặc mã OTP 6 chữ số (UC-05: Confirm Password Reset).
/// </summary>
public class ResetPasswordRequestDto
{
    /// <summary>Địa chỉ Email của tài khoản cần đặt lại mật khẩu.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Chuỗi Reset Token (từ link email) HOẶC mã OTP 6 chữ số đã nhận qua email (UC-04, UC-05).</summary>
    public string TokenOrOtp { get; set; } = string.Empty;

    /// <summary>Mật khẩu mới đáp ứng chuẩn độ phức tạp bảo mật.</summary>
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>Xác nhận mật khẩu mới.</summary>
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
