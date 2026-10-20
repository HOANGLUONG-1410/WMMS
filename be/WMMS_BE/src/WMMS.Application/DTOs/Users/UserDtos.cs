namespace WMMS.Application.DTOs.Users;

/// <summary>
/// DTO Tham số lọc và phân trang danh sách tài khoản người dùng (UC-06: View Account List).
/// </summary>
public class UserQueryFilterDto
{
    /// <summary>Từ khóa tìm kiếm theo Username, Email, FullName hoặc Phone.</summary>
    public string? SearchKeyword { get; set; }

    /// <summary>Lọc theo mã vai trò (ví dụ: ADMIN, SALES, PLANNER, QC).</summary>
    public string? RoleCode { get; set; }

    /// <summary>Lọc theo trạng thái tài khoản: ACTIVE, INACTIVE, BANNED, LOCKED.</summary>
    public string? Status { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// DTO Thông tin tài khoản người dùng trả về trong danh sách và xem chi tiết (UC-06, UC-07).
/// </summary>
public class UserDto
{
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Status { get; set; } = string.Empty;
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<string> Roles { get; set; } = new();
}

/// <summary>
/// DTO Tạo mới tài khoản nhân sự bởi Quản trị viên (UC-08: Create New User Account, Report 4 Sequence Diagram 3.3.1 <c>CreateStaffDto</c>).
/// </summary>
public class CreateStaffDto
{
    /// <summary>Tên đăng nhập duy nhất (3-100 ký tự, không chứa khoảng trắng).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Địa chỉ Email công việc duy nhất.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Mật khẩu khởi tạo ban đầu (sẽ được băm bằng BCrypt work factor 12 trước khi lưu DB).</summary>
    public string InitialPassword { get; set; } = string.Empty;

    /// <summary>Họ và tên đầy đủ của nhân sự.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Số điện thoại liên hệ.</summary>
    public string? Phone { get; set; }

    /// <summary>Danh sách mã vai trò được gán (tối thiểu 1 vai trò theo quy tắc Multi-Role RBAC GBR-04, ví dụ: ["PLANNER", "LEADER"]).</summary>
    public List<string> RoleCodes { get; set; } = new();
}

/// <summary>
/// DTO Cập nhật thông tin hồ sơ tài khoản và phân quyền vai trò bởi Admin (UC-09: Update Account Information &amp; Roles).
/// </summary>
public class UpdateUserDto
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }

    /// <summary>Danh sách mã vai trò mới (nếu truyền vào sẽ đồng bộ lại bảng <c>user_roles</c>).</summary>
    public List<string>? RoleCodes { get; set; }

    /// <summary>Lý do cập nhật hồ sơ / quyền hạn để lưu vào bảng <c>audit_logs</c>.</summary>
    public string? Reason { get; set; }
}

/// <summary>
/// DTO Cập nhật riêng danh sách đa vai trò của nhân sự (UC-09, Report 4 Class Diagram <c>UpdateStaffRolesAsync</c>).
/// </summary>
public class UpdateUserRolesDto
{
    /// <summary>Danh sách mã vai trò gán cho nhân sự (ví dụ: ["SALES", "PLANNER"]).</summary>
    public List<string> RoleCodes { get; set; } = new();

    /// <summary>Lý do thay đổi phân quyền.</summary>
    public string? Reason { get; set; }
}

/// <summary>
/// DTO Khóa / Mở khóa / Vô hiệu hóa tài khoản nhân sự (UC-10: Ban or Unban Account).
/// Tuân thủ BR-ADM-01 (Admin không thể tự khóa chính mình) và tự động thu hồi toàn bộ Refresh Token đang hoạt động.
/// </summary>
public class UpdateUserStatusDto
{
    /// <summary>Trạng thái mục tiêu: ACTIVE, INACTIVE, hoặc BANNED.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Lý do bắt buộc khi thay đổi trạng thái tài khoản để lưu vết kiểm toán (GBR-05, UC-10).</summary>
    public string Reason { get; set; } = string.Empty;
}
