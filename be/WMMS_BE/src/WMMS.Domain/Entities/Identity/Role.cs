namespace WMMS.Domain.Entities.Identity;

/// <summary>
/// Thực thể Vai trò hệ thống (<c>roles</c>) - Phân hệ AUT &amp; ADM (UC-11, UC-12, Report 4 Class Diagram 3.2.1).
/// Lưu trữ mã vai trò (ADMIN, OWNER, SALES, PLANNER, PURCHASING, WAREHOUSE, LEADER, QC)
/// và cấu hình ma trận phân quyền dạng JSON (<c>permissions_json</c>).
/// </summary>
public class Role
{
    /// <summary>Khóa chính vai trò (<c>role_id</c>).</summary>
    public int RoleId { get; set; }

    /// <summary>Mã vai trò chuẩn in hoa (<c>role_code</c>), ví dụ: ADMIN, PLANNER, QC.</summary>
    public string RoleCode { get; set; } = string.Empty;

    /// <summary>Tên hiển thị của vai trò (<c>role_name</c>).</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>Mô tả chi tiết phạm vi trách nhiệm của vai trò (<c>description</c> - Report 4 Class Diagram).</summary>
    public string? Description { get; set; }

    /// <summary>Chuỗi JSON định nghĩa danh sách quyền hạn / module scopes của vai trò (<c>permissions_json</c> - UC-12).</summary>
    public string PermissionsJson { get; set; } = "{}";

    /// <summary>Danh sách liên kết người dùng sở hữu vai trò này (<c>user_roles</c>).</summary>
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
