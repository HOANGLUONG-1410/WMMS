using FluentValidation;
using WMMS.Application.DTOs.Purchasing;

namespace WMMS.Application.Validators.Purchasing;

/// <summary>
/// Kiểm tra cấu trúc yêu cầu tạo PO (UC-52): dòng đặt hàng hợp lệ, mỗi Supplier có đúng một bộ điều khoản,
/// ngày hẹn giao không ở quá khứ (UC052.E1). Các quy tắc phụ thuộc dữ liệu (PR đã duyệt, lượng còn lại) kiểm tra ở service.
/// </summary>
public class CreatePurchaseOrdersRequestValidator : AbstractValidator<CreatePurchaseOrdersRequestDto>
{
    public CreatePurchaseOrdersRequestValidator()
    {
        RuleFor(x => x.PrId).GreaterThan(0).WithMessage("PrId is required.");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("At least one PR line must be selected to generate a Purchase Order.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.PrItemId).GreaterThan(0).WithMessage("PrItemId is required.");
            line.RuleFor(l => l.SupplierId).GreaterThan(0).WithMessage("Each line must be assigned to a supplier (BR-PUR-03).");
            line.RuleFor(l => l.OrderedQty).GreaterThan(0).WithMessage("OrderedQty must be greater than 0.");
            line.RuleFor(l => l.UnitPrice).GreaterThan(0).WithMessage("UnitPrice must be greater than 0.");
        });

        RuleFor(x => x.Lines)
            .Must(lines => lines.GroupBy(l => (l.PrItemId, l.SupplierId)).All(g => g.Count() == 1))
            .WithMessage("Duplicate (PrItemId, SupplierId) lines are not allowed; merge quantities into one line.");

        RuleFor(x => x.SupplierTerms)
            .NotEmpty().WithMessage("Promised delivery date is required for each supplier.");

        RuleFor(x => x.SupplierTerms)
            .Must(t => t.GroupBy(s => s.SupplierId).All(g => g.Count() == 1))
            .WithMessage("Each supplier must appear only once in SupplierTerms.");

        RuleForEach(x => x.SupplierTerms).ChildRules(terms =>
        {
            terms.RuleFor(t => t.SupplierId).GreaterThan(0);
            terms.RuleFor(t => t.ExpectedArrivalDate)
                .Must(d => d.Date >= DateTime.UtcNow.Date)
                .WithMessage("Promised delivery date cannot be in the past (UC052.E1).");
            terms.RuleFor(t => t.Terms).MaximumLength(1000).WithMessage("Terms must not exceed 1000 characters.");
        });

        RuleFor(x => x)
            .Must(x =>
            {
                var lineSuppliers = x.Lines.Select(l => l.SupplierId).ToHashSet();
                var termSuppliers = x.SupplierTerms.Select(t => t.SupplierId).ToHashSet();
                return lineSuppliers.SetEquals(termSuppliers);
            })
            .WithMessage("SupplierTerms must contain exactly the suppliers that have order lines.");
    }
}

/// <summary>Kiểm tra cập nhật lịch giao (UC-53): ngày hẹn giao không ở quá khứ, ghi chú tối đa 500 ký tự.</summary>
public class UpdatePoEtaValidator : AbstractValidator<UpdatePoEtaDto>
{
    public UpdatePoEtaValidator()
    {
        RuleFor(x => x.ExpectedArrivalDate)
            .Must(d => d.Date >= DateTime.UtcNow.Date)
            .WithMessage("Promised delivery date cannot be in the past (UC052.E1).");
        RuleFor(x => x.Notes).MaximumLength(500).WithMessage("Notes must not exceed 500 characters.");
    }
}

/// <summary>Kiểm tra bộ lọc danh sách PO (UC-50).</summary>
public class PurchaseOrderFilterValidator : AbstractValidator<PurchaseOrderFilterDto>
{
    private static readonly string[] Statuses = { "ISSUED", "PARTIALLY_RECEIVED", "FULFILLED", "CANCELLED" };
    private static readonly string[] EtaStatuses = { "OVERDUE", "DUE_SOON" };

    public PurchaseOrderFilterValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrWhiteSpace(s) || Statuses.Contains(s.Trim().ToUpperInvariant()))
            .WithMessage("Status must be one of: ISSUED, PARTIALLY_RECEIVED, FULFILLED, CANCELLED.");
        RuleFor(x => x.EtaStatus)
            .Must(s => string.IsNullOrWhiteSpace(s) || EtaStatuses.Contains(s.Trim().ToUpperInvariant()))
            .WithMessage("EtaStatus must be one of: OVERDUE, DUE_SOON.");
        RuleFor(x => x)
            .Must(x => !x.FromEta.HasValue || !x.ToEta.HasValue || x.FromEta.Value.Date <= x.ToEta.Value.Date)
            .WithMessage("FromEta must not be later than ToEta.");
    }
}
