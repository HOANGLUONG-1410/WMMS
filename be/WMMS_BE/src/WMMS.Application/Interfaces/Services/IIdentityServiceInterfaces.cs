using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.AuditLogs;
using WMMS.Application.DTOs.Auth;
using WMMS.Application.DTOs.Roles;
using WMMS.Application.DTOs.Users;

namespace WMMS.Application.Interfaces.Services;

/// <summary>
/// Giao diện Dịch vụ Xác thực và Quản lý Phiên đăng nhập (Phân hệ AUT - UC-01 đến UC-05, Report 4 Class Diagram <c>IAuthService</c>).
/// </summary>
public interface IAuthService
{
    /// <summary>UC-01: Xác thực thông tin đăng nhập, kiểm tra khóa 5 lần sai / 30 phút, cấp JWT và Refresh Token.</summary>
    Task<AuthResponseDto> AuthenticateAsync(LoginRequestDto request, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>UC-01 / BR-SEC-01: Xoay vòng Refresh Token (Token Rotation) - thu hồi token cũ và cấp cặp JWT + Refresh Token mới.</summary>
    Task<AuthResponseDto> RefreshTokenAsync(string rawRefreshToken, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>UC-02: Đăng xuất khỏi hệ thống - thu hồi Refresh Token hiện hành và ghi Audit Log.</summary>
    Task RevokeTokenAsync(string? rawRefreshToken, long? currentUserId, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>Lấy thông tin hồ sơ người dùng đang đăng nhập kèm danh sách quyền đa vai trò (Global Role Selector).</summary>
    Task<CurrentUserProfileDto> GetCurrentUserProfileAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>UC-03: Đổi mật khẩu cá nhân khi đang đăng nhập, đồng thời thu hồi toàn bộ các Refresh Token cũ.</summary>
    Task ChangePasswordAsync(long userId, ChangePasswordRequestDto request, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>UC-04: Yêu cầu khôi phục mật khẩu (tạo Reset Token + OTP 6 số có hạn 15 phút, giới hạn tối đa 3 lần / 10 phút).</summary>
    Task<ForgotPasswordResponseDto> RequestPasswordResetAsync(ForgotPasswordRequestDto request, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>UC-05: Xác nhận đặt lại mật khẩu mới bằng Reset Token hoặc mã OTP 6 chữ số.</summary>
    Task ConfirmPasswordResetAsync(ResetPasswordRequestDto request, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>JOB-01: Mở khóa tự động các tài khoản đang ở trạng thái LOCKED đã hết thời gian khóa 30 phút.</summary>
    Task<int> UnlockExpiredAccountsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Giao diện Dịch vụ Quản trị Tài khoản &amp; Đa vai trò (Phân hệ ADM - UC-06 đến UC-10, Report 4 Sequence Diagram 3.3.1).
/// </summary>
public interface IUserService
{
    /// <summary>UC-06: Truy vấn danh sách tài khoản có phân trang, tìm kiếm và lọc theo Role / Status.</summary>
    Task<PagedResult<UserDto>> GetUsersAsync(UserQueryFilterDto filter, CancellationToken cancellationToken = default);

    /// <summary>UC-07: Xem chi tiết hồ sơ tài khoản và danh sách các vai trò được gán.</summary>
    Task<UserDto> GetUserByIdAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>UC-08: Tạo mới tài khoản nhân sự và gán 1..* vai trò trong một giao dịch nguyên tử (Report 4 Sequence Diagram 3.3.1).</summary>
    Task<UserDto> CreateStaffAccountAsync(CreateStaffDto dto, long adminUserId, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>UC-09: Cập nhật thông tin hồ sơ nhân sự và đồng bộ danh sách vai trò.</summary>
    Task<UserDto> UpdateUserAsync(long targetUserId, UpdateUserDto dto, long adminUserId, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>UC-09: Cập nhật riêng danh sách đa vai trò của nhân sự (Report 4 Class Diagram <c>UpdateStaffRolesAsync</c>).</summary>
    Task<UserDto> UpdateStaffRolesAsync(long targetUserId, UpdateUserRolesDto dto, long adminUserId, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>UC-10: Khóa / Mở khóa / Vô hiệu hóa tài khoản (chặn tự khóa chính mình BR-ADM-01, thu hồi toàn bộ Refresh Token khi khóa).</summary>
    Task<UserDto> UpdateUserStatusAsync(long targetUserId, UpdateUserStatusDto dto, long adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
}

/// <summary>
/// Giao diện Dịch vụ Quản lý Vai trò và Cấu hình Ma trận Phân quyền (UC-11 &amp; UC-12).
/// </summary>
public interface IRoleService
{
    /// <summary>UC-11: Lấy danh sách toàn bộ 8 vai trò trong hệ thống kèm số lượng nhân sự đang đảm nhận.</summary>
    Task<IReadOnlyList<RoleDto>> GetAllRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>UC-12: Cập nhật cấu hình JSON quyền hạn của một vai trò (bảo vệ quyền cốt lõi của ADMIN theo BR-ADM-03).</summary>
    Task<RoleDto> UpdateRolePermissionsAsync(int roleId, UpdateRolePermissionsDto dto, long adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
}

/// <summary>
/// Giao diện Dịch vụ Nhật ký Kiểm toán Hệ thống bất biến &amp; Cấu hình Tham số Toàn cục (UC-13, UC-14, UC-15, GBR-05, SCR-ADM-02).
/// </summary>
public interface IAuditLogService
{
    /// <summary>UC-13: Truy vấn phân trang lịch sử nhật ký kiểm toán theo người dùng, loại hành động, thực thể và khoảng thời gian.</summary>
    Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(AuditLogFilterDto filter, CancellationToken cancellationToken = default);

    /// <summary>UC-14: Xem chi tiết một bản ghi nhật ký kiểm toán (bao gồm JSON BeforeState và AfterState).</summary>
    Task<AuditLogDto> GetAuditLogByIdAsync(long logId, CancellationToken cancellationToken = default);

    /// <summary>UC-13 / SCR-ADM-02: Xuất dữ liệu nhật ký kiểm toán bất biến ra định dạng CSV theo bộ lọc hiện tại.</summary>
    Task<byte[]> ExportAuditLogsCsvAsync(AuditLogFilterDto filter, CancellationToken cancellationToken = default);

    /// <summary>UC-15: Lấy danh sách toàn bộ tham số cấu hình hệ thống (DEFAULT_SCRAP_MIN, DEFAULT_SCRAP_MAX, ETD_WARNING_DAYS).</summary>
    Task<IReadOnlyList<SystemConfigDto>> GetSystemConfigsAsync(CancellationToken cancellationToken = default);

    /// <summary>UC-15: Cập nhật giá trị tham số cấu hình hệ thống và ghi nhận vào Audit Log.</summary>
    Task<SystemConfigDto> UpdateSystemConfigAsync(string configKey, UpdateSystemConfigDto dto, long actorUserId, string? ipAddress, CancellationToken cancellationToken = default);
}
