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
            .WithMessage("Mã lô sản xuất (BatchId) phải lớn hơn 0.");

        RuleFor(x => x.HoldPointStageSeq)
            .GreaterThan(0)
            .When(x => x.HoldPointStageSeq.HasValue)
            .WithMessage("Số thứ tự công đoạn Hold Point phải lớn hơn 0 nếu được chỉ định.");

        RuleFor(x => x.Justification)
            .NotEmpty()
            .WithMessage("Lý do giải trình là bắt buộc khi gửi yêu cầu mở khóa Hold Point Overdriver (KBN02).")
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
            .WithMessage("Lý do phê duyệt là bắt buộc đối với Chủ xưởng khi mở khóa Hold Point Overdriver (GBR-05).")
            .MinimumLength(5)
            .WithMessage("Lý do phê duyệt phải có ít nhất 5 ký tự.")
            .MaximumLength(500)
            .WithMessage("Lý do phê duyệt không được vượt quá 500 ký tự.");
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
            .WithMessage("Lý do từ chối là bắt buộc khi không phê duyệt mở khóa Hold Point Overdriver (KBN02).")
            .MinimumLength(5)
            .WithMessage("Lý do từ chối phải có ít nhất 5 ký tự.")
            .MaximumLength(500)
            .WithMessage("Lý do từ chối không được vượt quá 500 ký tự.");
    }
}

/// <summary>
/// Validator cho DTO Chuyển công đoạn sản xuất (Advance Stage / Move Kanban Card).
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
