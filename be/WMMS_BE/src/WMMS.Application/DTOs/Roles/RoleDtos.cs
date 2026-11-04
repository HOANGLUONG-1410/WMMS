namespace WMMS.Application.DTOs.Roles;

/// <summary>
/// DTO Thông tin Vai trò hệ thống và cấu hình phân quyền (UC-11: View Role List, UC-12: Configure Role Permissions).
/// </summary>
public class RoleDto
{
    public int RoleId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PermissionsJson { get; set; } = "{}";
    public int AssignedUsersCount { get; set; }
}

/// <summary>
/// DTO Cập nhật cấu hình ma trận quyền hạn JSON cho một Vai trò (UC-12: Configure Role Permissions).
/// Tuân thủ quy tắc BR-ADM-03: Không cho phép gỡ bỏ quyền quản trị cốt lõi của vai trò ADMIN.
/// </summary>
public class UpdateRolePermissionsDto
{
    public string? RoleName { get; set; }
    public string? Description { get; set; }

    /// <summary>Chuỗi JSON hợp lệ chứa cấu hình quyền hạn của vai trò.</summary>
    public string PermissionsJson { get; set; } = "{}";

    /// <summary>Lý do cập nhật cấu hình quyền để ghi vào bảng <c>audit_logs</c>.</summary>
    public string? Reason { get; set; }
}
