using WMMS.Domain.Entities.Identity;

namespace WMMS.Application.Interfaces.Services;

/// <summary>
/// Giao diện Bộ khởi tạo và băm Token bảo mật (Report 4 Class Diagram 3.2.1 <c>IJwtTokenGenerator</c>).
/// Chịu trách nhiệm ký JWT Access Token chứa danh sách đa vai trò (Multi-Role Claims)
/// và sinh chuỗi Refresh Token / OTP ngẫu nhiên an toàn mật mã học (CSPRNG).
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Sinh JWT Access Token cho người dùng kèm danh sách các vai trò được gán (GBR-04).
    /// </summary>
    (string AccessToken, DateTime ExpiresAt) GenerateAccessToken(User user, IList<string> roles);

    /// <summary>
    /// Sinh chuỗi Refresh Token ngẫu nhiên 64-byte và trả về cả bản thô (gửi cho client) lẫn bản băm SHA-256 (lưu DB).
    /// </summary>
    (string RawToken, string TokenHash, DateTime ExpiresAt) GenerateRefreshToken(bool rememberMe = false);

    /// <summary>
    /// Tính toán mã băm một chiều SHA-256 (Hex/Base64) cho Refresh Token hoặc Password Reset Token (BR-SEC-01, BR-SEC-03).
    /// </summary>
    string ComputeSha256Hash(string rawValue);

    /// <summary>
    /// Sinh mã OTP 6 chữ số ngẫu nhiên an toàn mật mã học cho chức năng Quên mật khẩu (UC-04).
    /// </summary>
    string GenerateSixDigitOtp();
}

/// <summary>
/// Giao diện Băm và Kiểm tra mật khẩu chuẩn BCrypt (BR-SEC-01: work factor &gt;= 12).
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string plainPassword);
    bool VerifyPassword(string plainPassword, string passwordHash);
}

/// <summary>
/// Giao diện Dịch vụ gửi Email thông báo (dùng cho UC-04: Gửi mã OTP đặt lại mật khẩu và SC-16: Gửi Đơn đặt hàng nguyên liệu cho Nhà cung cấp).
/// </summary>
public interface IEmailService
{
    Task SendPasswordResetEmailAsync(
        string toEmail,
        string fullName,
        string resetToken,
        string otpCode,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// SC-16 / UC-52: Gửi email Đơn đặt hàng nguyên vật liệu (kèm tệp đính kèm biểu mẫu ĐƠN ĐẶT HÀNG) tới Nhà cung cấp.
    /// Trả về <c>true</c> nếu đã gửi trực tiếp qua máy chủ SMTP, hoặc <c>false</c> nếu môi trường local ghi nhận bản xem trước.
    /// </summary>
    Task<bool> SendPurchaseOrderEmailAsync(
        string toEmail,
        string supplierName,
        string poCode,
        string htmlDocument,
        CancellationToken cancellationToken = default) => Task.FromResult(false);
}
