using FluentValidation;
using WMMS.Application.DTOs.InventoryPr;

namespace WMMS.Application.Validators.InventoryPr;

/// <summary>
/// Kiểm tra dữ liệu từ chối PR (UC-48, UC048.E1): lý do từ chối là bắt buộc, tối đa 500 ký tự (khớp cột <c>rejection_reason</c>).
/// </summary>
public class RejectPurchaseRequestValidator : AbstractValidator<RejectPurchaseRequestDto>
{
    public RejectPurchaseRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Rejection reason is required (UC048.E1).")
            .Must(r => string.IsNullOrWhiteSpace(r) || r.Trim().Length <= 500)
            .WithMessage("Rejection reason must not exceed 500 characters.");
    }
}

/// <summary>Kiểm tra ghi chú phê duyệt PR (UC-47): tùy chọn, tối đa 500 ký tự.</summary>
public class ApprovePurchaseRequestValidator : AbstractValidator<ApprovePurchaseRequestDto>
{
    public ApprovePurchaseRequestValidator()
    {
        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Approval notes must not exceed 500 characters.");
    }
}

/// <summary>Kiểm tra bộ lọc danh sách PR (UC-45).</summary>
public class PurchaseRequestFilterValidator : AbstractValidator<PurchaseRequestFilterDto>
{
    private static readonly string[] AllowedStatuses = { "PENDING_APPROVAL", "APPROVED", "REJECTED", "PO_ISSUED" };

    public PurchaseRequestFilterValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrWhiteSpace(s) || AllowedStatuses.Contains(s.Trim().ToUpperInvariant()))
            .WithMessage("Status must be one of: PENDING_APPROVAL, APPROVED, REJECTED, PO_ISSUED.");

        RuleFor(x => x)
            .Must(x => !x.FromDate.HasValue || !x.ToDate.HasValue || x.FromDate.Value.Date <= x.ToDate.Value.Date)
            .WithMessage("FromDate must not be later than ToDate.");
    }
}
