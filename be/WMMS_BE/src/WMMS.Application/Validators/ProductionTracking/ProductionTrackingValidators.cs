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

/// <summary>
/// Bộ kiểm tra tính hợp lệ cho bộ lọc Báo cáo Phân tích Lỗi Chất lượng &amp; Thống kê Tần suất Phá khóa Overdriver (Iteration 3, SC-20).
/// </summary>
public class QualityAnalyticsFilterValidator : AbstractValidator<QualityAnalyticsFilterDto>
{
    public QualityAnalyticsFilterValidator()
    {
        When(x => x.OrderId.HasValue, () =>
        {
            RuleFor(x => x.OrderId!.Value)
                .GreaterThan(0)
                .WithMessage("Mã đơn hàng (OrderId) cần lọc phải lớn hơn 0.");
        });

        When(x => x.StageSequence.HasValue, () =>
        {
            RuleFor(x => x.StageSequence!.Value)
                .InclusiveBetween(1, 20)
                .WithMessage("Thứ tự trạm công đoạn (StageSequence) phải nằm trong khoảng từ 1 đến 20.");
        });

        When(x => x.FromDate.HasValue && x.ToDate.HasValue, () =>
        {
            RuleFor(x => x)
                .Must(x => x.FromDate!.Value <= x.ToDate!.Value)
                .WithMessage("Ngày bắt đầu lọc (FromDate) không được lớn hơn ngày kết thúc (ToDate).");
        });

        RuleFor(x => x.TopN)
            .InclusiveBetween(1, 200)
            .WithMessage("Giới hạn số lượng bản ghi (TopN) phải nằm trong khoảng từ 1 đến 200.");
    }
}
