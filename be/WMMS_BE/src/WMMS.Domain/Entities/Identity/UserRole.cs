namespace WMMS.Domain.Entities.Identity;

/// <summary>
/// Thực thể liên kết Đa vai trò Người dùng - Vai trò (<c>user_roles</c>) theo quy tắc GBR-04 (Multi-Role RBAC).
/// Cho phép một nhân sự đảm nhận đồng thời nhiều vai trò trong nhà máy (ví dụ: vừa là PLANNER vừa là LEADER).
/// </summary>
public class UserRole
{
    /// <summary>Định danh người dùng (<c>user_id</c>).</summary>
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Định danh vai trò (<c>role_id</c>).</summary>
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    /// <summary>Thời điểm gán vai trò (<c>assigned_at</c> - UTC).</summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
