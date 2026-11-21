using FluentValidation;
using WMMS.Application.DTOs.Orders;

namespace WMMS.Application.Validators.Orders;

/// <summary>
/// Bộ kiểm tra hợp lệ cho yêu cầu kiểm tra lại bảng dữ liệu đơn hàng xem trước (UC-32, BR-ORD-02).
/// </summary>
public class ValidateStagingGridRequestValidator : AbstractValidator<ValidateStagingGridRequestDto>
{
    public ValidateStagingGridRequestValidator()
    {
        RuleFor(x => x.Rows)
            .NotEmpty().WithMessage("Bảng dữ liệu đơn hàng xem trước phải có ít nhất một dòng sản phẩm.");
    }
}

/// <summary>
/// Bộ kiểm tra hợp lệ trước khi xác nhận đơn hàng bán và khởi tạo Không gian đơn hàng (UC-33, BR-ORD-02, BR-ORD-03).
/// </summary>
public class ConfirmSalesOrderRequestValidator : AbstractValidator<ConfirmSalesOrderRequestDto>
{
    public ConfirmSalesOrderRequestValidator()
    {
        RuleFor(x => x.PoNumber)
            .NotEmpty().WithMessage("Vui lòng nhập mã đơn hàng (PO Number) trước khi xác nhận.")
            .MaximumLength(100).WithMessage("Mã đơn hàng không được vượt quá 100 ký tự.");

        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Vui lòng nhập tên khách hàng / đối tác mua hàng.")
            .MaximumLength(255);

        RuleFor(x => x.Rows)
            .NotEmpty().WithMessage("Không thể xác nhận đơn hàng trống. Vui lòng thêm ít nhất một dòng sản phẩm.");
    }
}
