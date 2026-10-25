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
                From = new MailAddress(fromEmail, "WMMS Security System"),
                Subject = "[WMMS] Password Reset Verification Code (OTP)",
                IsBodyHtml = true,
                Body = $"""
                    <div style="font-family: Arial, sans-serif; line-height: 1.6;">
                        <h2>WMMS - Password Reset Request</h2>
                        <p>Hello <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>
                        <p>We received a request to reset the password for your WMMS account.</p>
                        <p>Your 6-digit OTP verification code is:</p>
                        <h1 style="letter-spacing: 4px; color: #1d4ed8;">{otpCode}</h1>
                        <p>Or click the link below to reset your password:</p>
                        <p><a href="{resetLink}">{resetLink}</a></p>
                        <p>This code and link will expire in <strong>15 minutes</strong> (at {expiresAtUtc:yyyy-MM-dd HH:mm:ss} UTC).</p>
                        <p>If you did not request a password reset, please ignore this email.</p>
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
