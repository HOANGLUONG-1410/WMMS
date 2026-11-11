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
            .NotEmpty().WithMessage("Tên đăng nhập hoặc Email không được để trống.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.");
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
            .NotEmpty().WithMessage("Mật khẩu hiện tại không được để trống.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Mật khẩu mới không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu mới phải có ít nhất 8 ký tự.")
            .Matches("[A-Z]").WithMessage("Mật khẩu mới phải chứa ít nhất một chữ cái viết hoa.")
            .Matches("[a-z]").WithMessage("Mật khẩu mới phải chứa ít nhất một chữ cái viết thường.")
            .Matches("[0-9]").WithMessage("Mật khẩu mới phải chứa ít nhất một chữ số.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Mật khẩu mới phải chứa ít nhất một ký tự đặc biệt.")
            .NotEqual(x => x.CurrentPassword).WithMessage("Mật khẩu mới phải khác mật khẩu hiện tại.");

        RuleFor(x => x.ConfirmNewPassword)
            .Equal(x => x.NewPassword).WithMessage("Mật khẩu xác nhận không khớp với mật khẩu mới.");
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
            .NotEmpty().WithMessage("Địa chỉ Email không được để trống.")
            .EmailAddress().WithMessage("Định dạng địa chỉ Email không hợp lệ.");
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
            .NotEmpty().WithMessage("Địa chỉ Email không được để trống.")
            .EmailAddress().WithMessage("Định dạng địa chỉ Email không hợp lệ.");

        RuleFor(x => x.TokenOrOtp)
            .NotEmpty().WithMessage("Mã xác thực đặt lại mật khẩu hoặc mã OTP không được để trống.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Mật khẩu mới không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu mới phải có ít nhất 8 ký tự.")
            .Matches("[A-Z]").WithMessage("Mật khẩu mới phải chứa ít nhất một chữ cái viết hoa.")
            .Matches("[a-z]").WithMessage("Mật khẩu mới phải chứa ít nhất một chữ cái viết thường.")
            .Matches("[0-9]").WithMessage("Mật khẩu mới phải chứa ít nhất một chữ số.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Mật khẩu mới phải chứa ít nhất một ký tự đặc biệt.");

        RuleFor(x => x.ConfirmNewPassword)
            .Equal(x => x.NewPassword).WithMessage("Mật khẩu xác nhận không khớp với mật khẩu mới.");
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
            .NotEmpty().WithMessage("Tên tài khoản không được để trống.")
            .MinimumLength(3).WithMessage("Tên tài khoản phải có ít nhất 3 ký tự.")
            .MaximumLength(100).WithMessage("Tên tài khoản không được vượt quá 100 ký tự.")
            .Matches("^[a-zA-Z0-9._-]+$").WithMessage("Tên tài khoản chỉ được chứa chữ cái, chữ số, dấu chấm, gạch dưới và gạch ngang.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Định dạng email không hợp lệ.")
            .MaximumLength(255);

        RuleFor(x => x.InitialPassword)
            .NotEmpty().WithMessage("Mật khẩu khởi tạo không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu khởi tạo phải có ít nhất 8 ký tự.")
            .Matches("[A-Z]").WithMessage("Mật khẩu khởi tạo phải chứa ít nhất một chữ cái viết hoa.")
            .Matches("[a-z]").WithMessage("Mật khẩu khởi tạo phải chứa ít nhất một chữ cái viết thường.")
            .Matches("[0-9]").WithMessage("Mật khẩu khởi tạo phải chứa ít nhất một chữ số.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Mật khẩu khởi tạo phải chứa ít nhất một ký tự đặc biệt.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(255);

        RuleFor(x => x.RoleCodes)
            .NotEmpty().WithMessage("Phải gán ít nhất một vai trò cho tài khoản người dùng (GBR-04).");
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
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Định dạng email không hợp lệ.")
            .MaximumLength(255);

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
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
            .NotEmpty().WithMessage("Trạng thái cập nhật không được để trống.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Bắt buộc phải nhập lý do khi thay đổi trạng thái tài khoản (UC-10, GBR-05).")
            .MinimumLength(5).WithMessage("Lý do phải có ít nhất 5 ký tự.");
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
            .NotEmpty().WithMessage("Dữ liệu phân quyền JSON không được để trống.");
    }
}
