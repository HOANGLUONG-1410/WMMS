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
            .NotEmpty().WithMessage("Staging grid must contain at least one order line item (BR-ORD-02).");
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
            .NotEmpty().WithMessage("PO Number is required before confirming sales order (MSG01).")
            .MaximumLength(100).WithMessage("PO Number cannot exceed 100 characters.");

        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Customer / Buyer name is required (MSG01).")
            .MaximumLength(255);

        RuleFor(x => x.Rows)
            .NotEmpty().WithMessage("Cannot confirm an empty sales order. At least one product line is required (BR-ORD-02).");
    }
}
