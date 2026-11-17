using FluentValidation;
using WMMS.Application.DTOs.KanbanQc;

namespace WMMS.Application.Validators.KanbanQc;

/// <summary>
/// Validator cho DTO Yêu cầu Ghi đè điểm dừng Hold Point do Tổ trưởng SX gửi lên (SC-20, KBN02).
/// </summary>
public class RequestHoldPointOverrideValidator : AbstractValidator<RequestHoldPointOverrideDto>
{
    public RequestHoldPointOverrideValidator()
    {
        RuleFor(x => x.BatchId)
            .GreaterThan(0)
            .WithMessage("Vui lòng chọn lô sản xuất hợp lệ.");

        RuleFor(x => x.HoldPointStageSeq)
            .GreaterThan(0)
            .When(x => x.HoldPointStageSeq.HasValue)
            .WithMessage("Thứ tự công đoạn chốt kiểm soát phải lớn hơn 0.");

        RuleFor(x => x.Justification)
            .NotEmpty()
            .WithMessage("Vui lòng nhập lý do giải trình khi xin phép chạy vượt chốt kiểm soát chờ vật tư.")
            .MinimumLength(5)
            .WithMessage("Lý do giải trình phải có ít nhất 5 ký tự.")
            .MaximumLength(500)
            .WithMessage("Lý do giải trình không được vượt quá 500 ký tự.");
    }
}

/// <summary>
/// Validator cho DTO Phê duyệt Ghi đè điểm dừng Hold Point do Chủ xưởng (OWNER) xác nhận (SC-20, KBN02).
/// </summary>
public class ApproveHoldPointOverrideValidator : AbstractValidator<ApproveHoldPointOverrideDto>
{
    public ApproveHoldPointOverrideValidator()
    {
        RuleFor(x => x.Justification)
            .NotEmpty()
            .WithMessage("Vui lòng nhập ý kiến phê duyệt cho phép chạy vượt chốt kiểm soát.")
            .MinimumLength(5)
            .WithMessage("Ý kiến phê duyệt phải có ít nhất 5 ký tự.")
            .MaximumLength(500)
            .WithMessage("Ý kiến phê duyệt không được vượt quá 500 ký tự.");
    }
}

/// <summary>
/// Validator cho DTO Từ chối Yêu cầu Ghi đè điểm dừng Hold Point (SC-20, KBN02).
/// </summary>
public class RejectHoldPointOverrideValidator : AbstractValidator<RejectHoldPointOverrideDto>
{
    public RejectHoldPointOverrideValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Vui lòng nhập lý do từ chối yêu cầu chạy vượt chốt kiểm soát.")
            .MinimumLength(5)
            .WithMessage("Lý do từ chối phải có ít nhất 5 ký tự.")
            .MaximumLength(500)
            .WithMessage("Lý do từ chối không được vượt quá 500 ký tự.");
    }
}

/// <summary>
/// Validator cho DTO Chuyển công đoạn sản xuất tuần tự.
/// </summary>
public class AdvanceStageRequestValidator : AbstractValidator<AdvanceStageRequestDto>
{
    public AdvanceStageRequestValidator()
    {
        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Notes))
            .WithMessage("Ghi chú chuyển công đoạn không được vượt quá 500 ký tự.");
    }
}
