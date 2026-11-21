using FluentValidation;
using WMMS.Application.DTOs.Orders;

namespace WMMS.Application.Validators.Orders;

/// <summary>
/// Bộ kiểm tra hợp lệ cho yêu cầu kiểm tra lại lưới Staging Grid (UC-32, BR-ORD-02).
/// </summary>
public class ValidateStagingGridRequestValidator : AbstractValidator<ValidateStagingGridRequestDto>
{
    public ValidateStagingGridRequestValidator()
    {
        RuleFor(x => x.Rows)
            .NotEmpty().WithMessage("Bảng tạm (Staging Grid) phải có ít nhất một dòng sản phẩm đặt hàng (BR-ORD-02).");
    }
}

/// <summary>
/// Bộ kiểm tra hợp lệ trước khi xác nhận đơn hàng bán và khởi tạo Order Space (UC-33, BR-ORD-02, BR-ORD-03).
/// </summary>
public class ConfirmSalesOrderRequestValidator : AbstractValidator<ConfirmSalesOrderRequestDto>
{
    public ConfirmSalesOrderRequestValidator()
    {
        RuleFor(x => x.PoNumber)
            .NotEmpty().WithMessage("Số PO khách hàng không được để trống trước khi xác nhận đơn hàng (MSG01).")
            .MaximumLength(100).WithMessage("Số PO khách hàng không được vượt quá 100 ký tự.");

        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Tên khách hàng / Đối tác mua hàng là bắt buộc (MSG01).")
            .MaximumLength(255).WithMessage("Tên khách hàng không được vượt quá 255 ký tự.");

        RuleFor(x => x.Rows)
            .NotEmpty().WithMessage("Không thể xác nhận đơn hàng trống. Cần ít nhất một dòng sản phẩm (BR-ORD-02).");
    }
}
