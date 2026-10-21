namespace WMMS.Domain.Entities.Identity;

/// <summary>
/// Thực thể Lưu trữ Mã khôi phục mật khẩu (<c>password_reset_tokens</c>) - Đặc tả UC-04 (Request Password Reset) &amp; UC-05 (Confirm Password Reset).
/// Hỗ trợ xác thực qua cả Reset Token (URL link) hoặc mã OTP 6 chữ số có thời hạn 15 phút (BR-SEC-03).
/// </summary>
public class PasswordResetToken
{
    /// <summary>Khóa chính (<c>reset_token_id</c>).</summary>
    public long ResetTokenId { get; set; }

    /// <summary>Định danh người dùng yêu cầu đặt lại mật khẩu (<c>user_id</c>).</summary>
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Bản băm SHA-256 của chuỗi Reset Token hoặc mã OTP 6 số (<c>token_hash</c> - BR-SEC-03).</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Bản băm SHA-256 của mã OTP 6 chữ số để người dùng có thể nhập mã OTP trực tiếp trên màn hình Password Reset (<c>otp_hash</c>).</summary>
    public string OtpHash { get; set; } = string.Empty;

    /// <summary>Thời điểm hết hạn (mặc định 15 phút kể từ khi tạo - <c>expires_at</c> UTC, UC-04).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Đánh dấu mã đã được sử dụng hay chưa (<c>is_used</c>). Mỗi mã chỉ được dùng duy nhất 1 lần (UC-05).</summary>
    public bool IsUsed { get; set; } = false;

    /// <summary>Thời điểm mã được sử dụng (<c>used_at</c> - UTC).</summary>
    public DateTime? UsedAt { get; set; }

    /// <summary>Địa chỉ IP yêu cầu đặt lại mật khẩu (<c>requested_by_ip</c>).</summary>
    public string? RequestedByIp { get; set; }

    /// <summary>Thời điểm tạo yêu cầu (<c>created_at</c> - UTC), dùng để kiểm tra giới hạn Rate-limit tối đa 3 lần / 10 phút (UC-04).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Kiểm tra mã khôi phục còn hợp lệ hay không (chưa dùng và chưa quá 15 phút).</summary>
    public bool IsValid => !IsUsed && ExpiresAt > DateTime.UtcNow;

    /// <summary>Đánh dấu mã khôi phục đã được tiêu thụ sau khi đặt lại mật khẩu thành công (UC-05).</summary>
    public void MarkAsUsed()
    {
        IsUsed = true;
        UsedAt = DateTime.UtcNow;
    }
}
