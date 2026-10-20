using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.Auth;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;
using WMMS.Domain.Exceptions;

namespace WMMS.API.Controllers;

/// <summary>
/// Controller Xác thực và Quản lý Phiên đăng nhập (Phân hệ AUT - UC-01 đến UC-05, Report 4 Class Diagram <c>AuthController</c>).
/// Phục vụ các màn hình trên Screen Flow: Login, Global Role Selector, Change Password, và Password Reset.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private const string RefreshTokenCookieName = "wmms_refresh_token";

    private readonly IAuthService _authService;
    private readonly IValidator<LoginRequestDto> _loginValidator;
    private readonly IValidator<ChangePasswordRequestDto> _changePasswordValidator;
    private readonly IValidator<ForgotPasswordRequestDto> _forgotPasswordValidator;
    private readonly IValidator<ResetPasswordRequestDto> _resetPasswordValidator;

    public AuthController(
        IAuthService authService,
        IValidator<LoginRequestDto> loginValidator,
        IValidator<ChangePasswordRequestDto> changePasswordValidator,
        IValidator<ForgotPasswordRequestDto> forgotPasswordValidator,
        IValidator<ResetPasswordRequestDto> resetPasswordValidator)
    {
        _authService = authService;
        _loginValidator = loginValidator;
        _changePasswordValidator = changePasswordValidator;
        _forgotPasswordValidator = forgotPasswordValidator;
        _resetPasswordValidator = resetPasswordValidator;
    }

    /// <summary>
    /// UC-01: Đăng nhập vào hệ thống bằng Username hoặc Email và mật khẩu.
    /// Cấp JWT Access Token, Refresh Token (cả trong JSON body và HttpOnly Cookie) kèm danh sách quyền đa vai trò (Global Role Selector).
    /// Tự động khóa tài khoản 30 phút nếu nhập sai mật khẩu 5 lần liên tiếp (BR-SEC-02).
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status423Locked)]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        await _loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await _authService.AuthenticateAsync(request, GetClientIpAddress(), cancellationToken);
        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAt);

        return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Logged in successfully (UC-01)."));
    }

    /// <summary>
    /// UC-01 / BR-SEC-01: Làm mới JWT Access Token bằng Refresh Token (Token Rotation).
    /// Hỗ trợ đọc Refresh Token từ JSON body hoặc từ HttpOnly Cookie <c>wmms_refresh_token</c>.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshTokenAsync([FromBody] RefreshTokenRequestDto? request, CancellationToken cancellationToken)
    {
        var rawToken = !string.IsNullOrWhiteSpace(request?.RefreshToken)
            ? request.RefreshToken
            : Request.Cookies[RefreshTokenCookieName];

        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw AppException.Unauthorized("Refresh token was not provided in request body or HttpOnly cookie.", MessageCodes.Msg12InvalidToken);
        }

        var result = await _authService.RefreshTokenAsync(rawToken, GetClientIpAddress(), cancellationToken);
        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAt);

        return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Access token refreshed successfully."));
    }

    /// <summary>
    /// UC-02: Đăng xuất khỏi hệ thống (Log Out System).
    /// Thu hồi Refresh Token hiện tại trong cơ sở dữ liệu, xóa HttpOnly Cookie và ghi nhận vào bảng <c>audit_logs</c>.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeTokenAsync([FromBody] RefreshTokenRequestDto? request, CancellationToken cancellationToken)
    {
        var rawToken = !string.IsNullOrWhiteSpace(request?.RefreshToken)
            ? request.RefreshToken
            : Request.Cookies[RefreshTokenCookieName];

        var currentUserId = TryGetCurrentUserId();
        await _authService.RevokeTokenAsync(rawToken, currentUserId, GetClientIpAddress(), cancellationToken);

        Response.Cookies.Delete(RefreshTokenCookieName);

        return Ok(ApiResponse<object?>.Ok(null, "Logged out successfully and session token revoked (UC-02)."));
    }

    /// <summary>
    /// Lấy thông tin hồ sơ người dùng hiện tại và danh sách đa vai trò (Global Role Workspace Switcher trên Screen Flow).
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = GetRequiredCurrentUserId();
        var profile = await _authService.GetCurrentUserProfileAsync(userId, cancellationToken);
        return Ok(ApiResponse<CurrentUserProfileDto>.Ok(profile, "Fetched current user profile and multi-role scopes."));
    }

    /// <summary>
    /// UC-03: Đổi mật khẩu cá nhân khi đang đăng nhập.
    /// Yêu cầu xác minh mật khẩu hiện tại, kiểm tra độ phức tạp mật khẩu mới, băm BCrypt và thu hồi mọi phiên cũ.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordRequestDto request, CancellationToken cancellationToken)
    {
        await _changePasswordValidator.ValidateAndThrowAsync(request, cancellationToken);

        var userId = GetRequiredCurrentUserId();
        await _authService.ChangePasswordAsync(userId, request, GetClientIpAddress(), cancellationToken);

        Response.Cookies.Delete(RefreshTokenCookieName);

        return Ok(ApiResponse<object?>.Ok(null, "Password changed successfully. Please log in again with your new password (UC-03)."));
    }

    /// <summary>
    /// UC-04: Yêu cầu khôi phục mật khẩu khi quên mật khẩu (Request Password Reset).
    /// Sinh mã OTP 6 chữ số &amp; Reset Token có hiệu lực 15 phút (tối đa 3 lần / 10 phút), trả về thông báo MSG05 chống dò quét email.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ForgotPasswordResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPasswordAsync([FromBody] ForgotPasswordRequestDto request, CancellationToken cancellationToken)
    {
        await _forgotPasswordValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await _authService.RequestPasswordResetAsync(request, GetClientIpAddress(), cancellationToken);
        return Ok(ApiResponse<ForgotPasswordResponseDto>.Ok(result, result.Message, MessageCodes.Msg05PasswordResetDispatched));
    }

    /// <summary>
    /// UC-05: Xác nhận đặt lại mật khẩu mới bằng mã OTP 6 chữ số hoặc Reset Token (Confirm Password Reset).
    /// Tiêu thụ token một lần, cập nhật mật khẩu mới, mở khóa tài khoản nếu đang bị LOCKED và thu hồi toàn bộ phiên cũ.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPasswordAsync([FromBody] ResetPasswordRequestDto request, CancellationToken cancellationToken)
    {
        await _resetPasswordValidator.ValidateAndThrowAsync(request, cancellationToken);

        await _authService.ConfirmPasswordResetAsync(request, GetClientIpAddress(), cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Password has been reset successfully. You can now log in with your new password (UC-05)."));
    }

    private void SetRefreshTokenCookie(string rawRefreshToken, DateTime expiresAtUtc)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = expiresAtUtc
        };
        Response.Cookies.Append(RefreshTokenCookieName, rawRefreshToken, cookieOptions);
    }

    private long GetRequiredCurrentUserId()
    {
        var userId = TryGetCurrentUserId();
        if (!userId.HasValue)
        {
            throw AppException.Unauthorized("Invalid or missing user identity claim in token.");
        }
        return userId.Value;
    }

    private long? TryGetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(sub, out var id) ? id : null;
    }

    private string? GetClientIpAddress()
    {
        if (Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            return forwardedFor.FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();
        }
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
