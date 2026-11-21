using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WMMS.Application.Interfaces.Services;

namespace WMMS.Infrastructure.Mail;

/// <summary>
/// Dịch vụ gửi Email hỗ trợ khôi phục mật khẩu (UC-04: Request Password Reset).
/// Nếu cấu hình <c>SmtpSettings:Host</c> được khai báo trong <c>appsettings.json</c>, hệ thống sẽ gửi email thực qua SMTP.
/// Nếu chưa cấu hình SMTP (môi trường Dev/Local), hệ thống sẽ ghi log chi tiết mã OTP và Reset Token ra Console để phục vụ kiểm thử.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendPasswordResetEmailAsync(
        string toEmail,
        string fullName,
        string resetToken,
        string otpCode,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        var smtpSection = _configuration.GetSection("SmtpSettings");
        var host = smtpSection["Host"];
        var frontendResetUrl = _configuration["Cors:AllowedOrigins:0"] ?? "http://localhost:5173";
        var resetLink = $"{frontendResetUrl.TrimEnd('/')}/reset-password?email={Uri.EscapeDataString(toEmail)}&token={Uri.EscapeDataString(resetToken)}";

        _logger.LogInformation(
            "[WMMS MAIL - UC-04 Password Reset] To: {Email} ({FullName}) | OTP (6-digit): {OtpCode} | ExpiresAt(UTC): {ExpiresAt:O} | ResetLink: {ResetLink}",
            toEmail, fullName, otpCode, expiresAtUtc, resetLink);

        if (string.IsNullOrWhiteSpace(host))
        {
            // Môi trường local/dev chưa cấu hình SMTP server: hoàn tất an toàn sau khi đã log OTP & Reset Link
            return;
        }

        try
        {
            var port = int.TryParse(smtpSection["Port"], out var p) ? p : 587;
            var username = smtpSection["Username"];
            var password = smtpSection["Password"];
            var fromEmail = smtpSection["FromEmail"] ?? username ?? "no-reply@wmms.local";
            var enableSsl = !bool.TryParse(smtpSection["EnableSsl"], out var ssl) || ssl;

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                Credentials = new NetworkCredential(username, password)
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, "Hệ thống Bảo mật WMMS"),
                Subject = "[WMMS] Mã xác thực đặt lại mật khẩu (OTP)",
                IsBodyHtml = true,
                Body = $"""
                    <div style="font-family: Arial, sans-serif; line-height: 1.6;">
                        <h2>WMMS - Yêu cầu đặt lại mật khẩu</h2>
                        <p>Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>
                        <p>Chúng tôi đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản WMMS của bạn.</p>
                        <p>Mã xác thực OTP gồm 6 chữ số của bạn là:</p>
                        <h1 style="letter-spacing: 4px; color: #1d4ed8;">{otpCode}</h1>
                        <p>Hoặc nhấp vào liên kết bên dưới để đặt lại mật khẩu:</p>
                        <p><a href="{resetLink}">{resetLink}</a></p>
                        <p>Mã xác thực và liên kết này sẽ hết hạn sau <strong>15 phút</strong> (vào lúc {expiresAtUtc:yyyy-MM-dd HH:mm:ss} UTC).</p>
                        <p>Nếu bạn không thực hiện yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
                    </div>
                    """
            };
            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch SMTP email to {Email}. OTP was logged for fallback verification.", toEmail);
        }
    }
}
