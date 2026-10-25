namespace WMMS.Domain.Entities.Identity;

/// <summary>
/// Thực thể Nhật ký kiểm toán bất biến (<c>audit_logs</c>) - Tuân thủ quy tắc GBR-05 (Immutable System Audit Trail) và UC-13, UC-14.
/// Lưu vết toàn bộ các thao tác quan trọng về bảo mật, quản trị tài khoản, phân quyền và nghiệp vụ.
/// </summary>
public class AuditLog
{
    /// <summary>Khóa chính nhật ký (<c>log_id</c>).</summary>
    public long LogId { get; set; }

    /// <summary>Định danh người dùng thực hiện hành động (<c>user_id</c> - có thể null nếu là hành động hệ thống tự động hoặc đăng nhập thất bại).</summary>
    public long? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Mã loại hành động (<c>action_type</c>), ví dụ: AUTH_LOGIN_SUCCESS, USER_CREATED, USER_STATUS_CHANGED.</summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>Tên thực thể hoặc phân hệ tác động (<c>entity_name</c>), ví dụ: User, Role, RefreshToken.</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>Định danh của bản ghi bị tác động (<c>entity_id</c>).</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Ảnh chụp dữ liệu trước khi thay đổi dạng JSON (<c>before_state</c>).</summary>
    public string? BeforeState { get; set; }

    /// <summary>Ảnh chụp dữ liệu sau khi thay đổi dạng JSON (<c>after_state</c>).</summary>
    public string? AfterState { get; set; }

    /// <summary>Lý do thực hiện thao tác hoặc ghi chú bổ sung (<c>reason</c>).</summary>
    public string? Reason { get; set; }

    /// <summary>Địa chỉ IP của người thực hiện (<c>ip_address</c>).</summary>
    public string? IpAddress { get; set; }

    /// <summary>Thời điểm ghi nhật ký (<c>created_at</c> - UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
