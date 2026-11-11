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
            .WithMessage("BatchId must be greater than 0.");

        RuleFor(x => x.HoldPointStageSeq)
            .GreaterThan(0)
            .When(x => x.HoldPointStageSeq.HasValue)
            .WithMessage("HoldPointStageSeq must be greater than 0 if specified.");

        RuleFor(x => x.Justification)
            .NotEmpty()
            .WithMessage("Justification is mandatory when requesting Hold Point Overdriver (KBN02).")
            .MinimumLength(5)
            .WithMessage("Justification must be at least 5 characters long.")
            .MaximumLength(500)
            .WithMessage("Justification cannot exceed 500 characters.");
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
            .WithMessage("Approval justification is mandatory for Owner Overdriver override (GBR-05).")
            .MinimumLength(5)
            .WithMessage("Approval justification must be at least 5 characters long.")
            .MaximumLength(500)
            .WithMessage("Approval justification cannot exceed 500 characters.");
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
            .WithMessage("Rejection reason is mandatory when declining Hold Point Overdriver (KBN02).")
            .MinimumLength(5)
            .WithMessage("Rejection reason must be at least 5 characters long.")
            .MaximumLength(500)
            .WithMessage("Rejection reason cannot exceed 500 characters.");
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
            .WithMessage("Stage advance notes cannot exceed 500 characters.");
    }
}
