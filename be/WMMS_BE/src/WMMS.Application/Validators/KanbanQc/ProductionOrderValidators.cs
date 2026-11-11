using FluentValidation;
using WMMS.Application.DTOs.KanbanQc;

namespace WMMS.Application.Validators.KanbanQc;

/// <summary>
/// Bộ kiểm tra hợp lệ dữ liệu cho yêu cầu chia lô và phát hành lệnh sản xuất (SC-19, UC-62).
/// </summary>
public class GenerateBatchesRequestValidator : AbstractValidator<GenerateBatchesRequestDto>
{
    public GenerateBatchesRequestValidator()
    {
        RuleFor(x => x.OrderItemId)
            .GreaterThan(0)
            .WithMessage("Mã dòng sản phẩm đơn hàng (OrderItemId) phải lớn hơn 0.");

        When(x => x.MaxBatchSize.HasValue, () =>
        {
            RuleFor(x => x.MaxBatchSize!.Value)
                .GreaterThan(0)
                .WithMessage("Công suất trạm / Kích thước lô định mức phải lớn hơn 0.")
                .LessThanOrEqualTo(10000)
                .WithMessage("Kích thước lô tối đa không vượt quá 10,000 sản phẩm/lô.");
        });

        When(x => x.CustomBatchSplits != null && x.CustomBatchSplits.Count > 0, () =>
        {
            RuleForEach(x => x.CustomBatchSplits)
                .GreaterThan(0)
                .WithMessage("Số lượng của từng lô tùy chỉnh phải lớn hơn 0.");
        });

        When(x => x.AllowOverdrive, () =>
        {
            RuleFor(x => x.Justification)
                .NotEmpty()
                .WithMessage("Cơ chế chạy trước Overdrive bắt buộc phải cung cấp lý do giải trình kỹ thuật (QT-06, KBN02).")
                .MaximumLength(500)
                .WithMessage("Lý do giải trình Overdrive không vượt quá 500 ký tự.");
        });
    }
}

/// <summary>
/// Bộ kiểm tra hợp lệ dữ liệu cho yêu cầu chuyển bước công đoạn tuần tự trên Kanban (SC-19).
/// </summary>
public class AdvanceStepRequestValidator : AbstractValidator<AdvanceStepRequestDto>
{
    public AdvanceStepRequestValidator()
    {
        When(x => x.TargetStageSeq.HasValue, () =>
        {
            RuleFor(x => x.TargetStageSeq!.Value)
                .GreaterThan(0)
                .WithMessage("Thứ tự công đoạn đích phải lớn hơn 0.")
                .LessThanOrEqualTo(20)
                .WithMessage("Thứ tự công đoạn không vượt quá 20.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.Notes), () =>
        {
            RuleFor(x => x.Notes)
                .MaximumLength(500)
                .WithMessage("Ghi chú chuyển bước không vượt quá 500 ký tự.");
        });
    }
}

/// <summary>
/// Bộ kiểm tra hợp lệ dữ liệu cho yêu cầu ghi nhận kiểm định KCS và tách nhánh 97%-3% (SC-19, QT-06).
/// </summary>
public class RecordQcInspectionValidator : AbstractValidator<RecordQcInspectionDto>
{
    public RecordQcInspectionValidator()
    {
        RuleFor(x => x.BatchStepId)
            .GreaterThan(0)
            .WithMessage("Mã công đoạn thực thi (BatchStepId) phải lớn hơn 0.");

        RuleFor(x => x.InspectedQty)
            .GreaterThan(0)
            .WithMessage("Số lượng kiểm định thực tế phải lớn hơn 0 (Quy tắc 100% kiểm định - BR-008).");

        RuleFor(x => x.PassQty)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Số lượng đạt chuẩn không được âm.");

        RuleFor(x => x.FailQty)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Số lượng lỗi không được âm.");

        RuleFor(x => x)
            .Must(x => x.PassQty + x.FailQty == x.InspectedQty)
            .WithMessage("Tổng số lượng Pass và Fail phải bằng đúng tổng số lượng kiểm định (PassQty + FailQty == InspectedQty).");

        When(x => x.FailQty > 0, () =>
        {
            RuleFor(x => x.Disposition)
                .NotEmpty()
                .WithMessage("Khi có sản phẩm lỗi, bắt buộc phải chọn phương án xử lý (REWORK_SUB_CARD hoặc SCRAP_REPLENISH).");
        });
    }
}

/// <summary>
/// Bộ kiểm tra hợp lệ cho yêu cầu kích hoạt Overdrive của Tổ trưởng (SC-20, UC-63).
/// </summary>
public class RequestOverdriveValidator : AbstractValidator<RequestOverdriveDto>
{
    public RequestOverdriveValidator()
    {
        RuleFor(x => x.Justification)
            .NotEmpty()
            .WithMessage("Lý do giải trình kỹ thuật và rủi ro thời hạn ETD là bắt buộc khi gửi yêu cầu Overdrive (UC-63).")
            .MaximumLength(500)
            .WithMessage("Lý do giải trình Overdrive không vượt quá 500 ký tự.");

        When(x => x.HoldPointStageSeq.HasValue, () =>
        {
            RuleFor(x => x.HoldPointStageSeq!.Value)
                .GreaterThan(1)
                .WithMessage("Công đoạn chốt chặn Hold Point phải từ công đoạn 2 trở đi.");
        });
    }
}

/// <summary>
/// Bộ kiểm tra hợp lệ cho quyết định phê duyệt Overdrive của Chủ xưởng (SC-20, KBN02).
/// </summary>
public class ApproveOverdriveValidator : AbstractValidator<ApproveOverdriveDto>
{
    public ApproveOverdriveValidator()
    {
        When(x => x.HoldPointStageSeq.HasValue, () =>
        {
            RuleFor(x => x.HoldPointStageSeq!.Value)
                .GreaterThan(1)
                .WithMessage("Công đoạn chốt chặn Hold Point phải từ công đoạn 2 trở đi.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.Notes), () =>
        {
            RuleFor(x => x.Notes)
                .MaximumLength(500)
                .WithMessage("Ghi chú chỉ đạo của Chủ xưởng không vượt quá 500 ký tự.");
        });
    }
}

