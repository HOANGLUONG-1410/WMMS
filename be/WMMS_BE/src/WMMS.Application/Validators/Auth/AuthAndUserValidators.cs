using FluentValidation;
using WMMS.Application.DTOs.Auth;
using WMMS.Application.DTOs.Roles;
using WMMS.Application.DTOs.Users;

namespace WMMS.Application.Validators.Auth;

/// <summary>
/// Bộ quy tắc kiểm tra tính hợp lệ cho yêu cầu Đăng nhập (UC-01).
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.UsernameOrEmail)
            .NotEmpty().WithMessage("Username or Email is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}

/// <summary>
/// Bộ quy tắc kiểm tra độ phức tạp mật khẩu khi Đổi mật khẩu cá nhân (UC-03, BR-SEC-01).
/// Yêu cầu tối thiểu 8 ký tự, có chữ hoa, chữ thường, chữ số và ký tự đặc biệt.
/// </summary>
public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequestDto>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("New password must be at least 8 characters long.")
            .Matches("[A-Z]").WithMessage("New password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("New password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("New password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("New password must contain at least one special character.")
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must be different from the current password.");

        RuleFor(x => x.ConfirmNewPassword)
            .Equal(x => x.NewPassword).WithMessage("Confirmation password does not match the new password.");
    }
}

/// <summary>
/// Bộ quy tắc kiểm tra yêu cầu Quên mật khẩu (UC-04).
/// </summary>
public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequestDto>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("Invalid email address format.");
    }
}

/// <summary>
/// Bộ quy tắc kiểm tra yêu cầu Đặt lại mật khẩu bằng Token hoặc OTP (UC-05).
/// </summary>
public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequestDto>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("Invalid email address format.");

        RuleFor(x => x.TokenOrOtp)
            .NotEmpty().WithMessage("Reset token or 6-digit OTP code is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("New password must be at least 8 characters long.")
            .Matches("[A-Z]").WithMessage("New password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("New password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("New password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("New password must contain at least one special character.");

        RuleFor(x => x.ConfirmNewPassword)
            .Equal(x => x.NewPassword).WithMessage("Confirmation password does not match the new password.");
    }
}

/// <summary>
/// Bộ quy tắc kiểm tra dữ liệu khi Admin tạo mới tài khoản nhân sự (UC-08, GBR-04).
/// </summary>
public class CreateStaffValidator : AbstractValidator<CreateStaffDto>
{
    public CreateStaffValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MinimumLength(3).WithMessage("Username must be at least 3 characters.")
            .MaximumLength(100).WithMessage("Username must not exceed 100 characters.")
            .Matches("^[a-zA-Z0-9._-]+$").WithMessage("Username can only contain letters, digits, dots, underscores, and hyphens.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.")
            .MaximumLength(255);

        RuleFor(x => x.InitialPassword)
            .NotEmpty().WithMessage("Initial password is required.")
            .MinimumLength(8).WithMessage("Initial password must be at least 8 characters long.")
            .Matches("[A-Z]").WithMessage("Initial password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Initial password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Initial password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Initial password must contain at least one special character.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(255);

        RuleFor(x => x.RoleCodes)
            .NotEmpty().WithMessage("At least one role must be assigned to the user account (GBR-04).");
    }
}

/// <summary>
/// Bộ quy tắc kiểm tra khi Admin cập nhật thông tin tài khoản (UC-09).
/// </summary>
public class UpdateUserValidator : AbstractValidator<UpdateUserDto>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.")
            .MaximumLength(255);

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(255);
    }
}

/// <summary>
/// Bộ quy tắc kiểm tra khi Admin khóa/mở khóa tài khoản (UC-10).
/// Bắt buộc nhập lý do (Reason) để lưu vết Audit Log.
/// </summary>
public class UpdateUserStatusValidator : AbstractValidator<UpdateUserStatusDto>
{
    public UpdateUserStatusValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Target status is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A mandatory reason is required when changing account status (UC-10, GBR-05).")
            .MinimumLength(5).WithMessage("Reason must be at least 5 characters.");
    }
}

/// <summary>
/// Bộ quy tắc kiểm tra khi Admin cấu hình lại quyền hạn của Vai trò (UC-12).
/// </summary>
public class UpdateRolePermissionsValidator : AbstractValidator<UpdateRolePermissionsDto>
{
    public UpdateRolePermissionsValidator()
    {
        RuleFor(x => x.PermissionsJson)
            .NotEmpty().WithMessage("Permissions JSON is required.");
    }
}
