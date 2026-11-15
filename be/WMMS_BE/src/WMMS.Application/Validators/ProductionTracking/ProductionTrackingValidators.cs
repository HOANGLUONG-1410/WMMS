using FluentValidation;
using WMMS.Application.DTOs.ProductionTracking;

namespace WMMS.Application.Validators.ProductionTracking;

/// <summary>
/// Bộ kiểm tra tính hợp lệ cho yêu cầu xác nhận hoàn tất lô sản xuất và nhập kho thành phẩm (UC-63).
/// </summary>
public class CompleteFinishedBatchValidator : AbstractValidator<CompleteFinishedBatchRequestDto>
{
    public CompleteFinishedBatchValidator()
    {
        When(x => x.ReceivedQty.HasValue, () =>
        {
            RuleFor(x => x.ReceivedQty!.Value)
                .GreaterThan(0)
                .WithMessage("Số lượng thành phẩm thực nhận nhập kho phải lớn hơn 0.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.WarehouseLocation), () =>
        {
            RuleFor(x => x.WarehouseLocation!)
                .MaximumLength(150)
                .WithMessage("Vị trí lưu kho thành phẩm không được vượt quá 150 ký tự.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.HandoverNote), () =>
        {
            RuleFor(x => x.HandoverNote!)
                .MaximumLength(500)
                .WithMessage("Ghi chú bàn giao nhập kho thành phẩm không được vượt quá 500 ký tự.");
        });
    }
}
