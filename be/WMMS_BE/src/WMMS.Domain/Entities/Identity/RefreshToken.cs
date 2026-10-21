namespace WMMS.Domain.Entities.Identity;

/// <summary>
/// Thực thể Lưu trữ Refresh Token (<c>refresh_tokens</c>) phục vụ duy trì phiên đăng nhập và xoay vòng token (UC-01, UC-02, Report 4 Sequence Diagram 3.3.1).
/// Giá trị token thô chỉ gửi cho client (HttpOnly Cookie / Body), trong DB chỉ lưu bản băm SHA-256 (<c>token_hash</c>).
/// </summary>
public class RefreshToken
{
    /// <summary>Khóa chính (<c>token_id</c>).</summary>
    public long TokenId { get; set; }

    /// <summary>Định danh người dùng sở hữu phiên (<c>user_id</c>).</summary>
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Bản băm SHA-256 của Refresh Token (<c>token_hash</c>).</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Thời điểm hết hạn của Refresh Token (<c>expires_at</c> - UTC). Mặc định 7 ngày (hoặc 30 ngày nếu chọn Remember Me).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Cờ đánh dấu token đã bị thu hồi khi Logout, Refresh, đổi mật khẩu hoặc khóa tài khoản (<c>is_revoked</c>).</summary>
    public bool IsRevoked { get; set; } = false;

    /// <summary>Thời điểm thu hồi token (<c>revoked_at</c> - UTC).</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Địa chỉ IP của client lúc khởi tạo token (<c>created_by_ip</c>).</summary>
    public string? CreatedByIp { get; set; }

    /// <summary>Bản băm của token mới thay thế token này trong quá trình xoay vòng (Token Rotation - <c>replaced_by_token_hash</c>).</summary>
    public string? ReplacedByTokenHash { get; set; }

    /// <summary>Thời điểm tạo bản ghi (<c>created_at</c> - UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Kiểm tra Refresh Token còn hiệu lực (chưa bị thu hồi và chưa hết hạn).</summary>
    public bool IsActive => !IsRevoked && ExpiresAt > DateTime.UtcNow;

    /// <summary>Thu hồi Refresh Token hiện tại (UC-02, UC-03, UC-10).</summary>
    public void Revoke(string? replacedByTokenHash = null)
    {
        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
