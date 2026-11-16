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
        RuleFor(x => x.PrId)
            .GreaterThan(0)
            .WithMessage("Mã phiếu yêu cầu mua hàng (PrId) là bắt buộc và phải lớn hơn 0.");

        RuleFor(x => x.Lines)
            .NotEmpty()
            .WithMessage("Bắt buộc phải chọn ít nhất một dòng PR để phát hành Đơn đặt hàng (PO).");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.PrItemId)
                .GreaterThan(0)
                .WithMessage("Mã dòng vật tư PR (PrItemId) là bắt buộc và phải lớn hơn 0.");

            line.RuleFor(l => l.SupplierId)
                .GreaterThan(0)
                .WithMessage("Mỗi dòng đặt hàng bắt buộc phải gán cho một Nhà cung cấp hợp lệ (BR-PUR-03).");

            line.RuleFor(l => l.OrderedQty)
                .GreaterThan(0)
                .WithMessage("Số lượng đặt hàng phải lớn hơn 0.");

            line.RuleFor(l => l.UnitPrice)
                .GreaterThan(0)
                .WithMessage("Đơn giá thỏa thuận phải lớn hơn 0.");
        });

        RuleFor(x => x.Lines)
            .Must(lines => lines.GroupBy(l => (l.PrItemId, l.SupplierId)).All(g => g.Count() == 1))
            .WithMessage("Không được phép trùng lặp cặp (PrItemId, SupplierId); vui lòng gộp số lượng vào một dòng.");

        RuleFor(x => x.SupplierTerms)
            .NotEmpty()
            .WithMessage("Bắt buộc phải có thông tin ngày hẹn giao hàng cho từng Nhà cung cấp.");

        RuleFor(x => x.SupplierTerms)
            .Must(t => t.GroupBy(s => s.SupplierId).All(g => g.Count() == 1))
            .WithMessage("Mỗi Nhà cung cấp chỉ được xuất hiện một lần trong danh sách điều khoản giao hàng.");

        RuleForEach(x => x.SupplierTerms).ChildRules(terms =>
        {
            terms.RuleFor(t => t.SupplierId)
                .GreaterThan(0)
                .WithMessage("Mã Nhà cung cấp trong điều khoản phải lớn hơn 0.");

            terms.RuleFor(t => t.ExpectedArrivalDate)
                .Must(d => d.Date >= DateTime.UtcNow.Date)
                .WithMessage("Ngày hẹn giao hàng của Nhà cung cấp không được ở quá khứ (UC052.E1).");

            terms.RuleFor(t => t.Terms)
                .MaximumLength(1000)
                .WithMessage("Điều khoản thanh toán & giao hàng không được vượt quá 1,000 ký tự.");
        });

        RuleFor(x => x)
            .Must(x =>
            {
                var lineSuppliers = x.Lines.Select(l => l.SupplierId).ToHashSet();
                var termSuppliers = x.SupplierTerms.Select(t => t.SupplierId).ToHashSet();
                return lineSuppliers.SetEquals(termSuppliers);
            })
            .WithMessage("Danh sách điều khoản giao hàng phải khớp chính xác với tất cả các Nhà cung cấp có dòng đặt hàng.");
    }
}

/// <summary>Kiểm tra cập nhật lịch giao (UC-53): ngày hẹn giao không ở quá khứ, ghi chú tối đa 500 ký tự.</summary>
public class UpdatePoEtaValidator : AbstractValidator<UpdatePoEtaDto>
{
    public UpdatePoEtaValidator()
    {
        RuleFor(x => x.ExpectedArrivalDate)
            .Must(d => d.Date >= DateTime.UtcNow.Date)
            .WithMessage("Ngày hẹn giao hàng của Nhà cung cấp không được ở quá khứ (UC052.E1).");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Ghi chú vận chuyển / lý do dời lịch không vượt quá 500 ký tự.");
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
            .WithMessage("Trạng thái PO phải thuộc một trong các giá trị: ISSUED, PARTIALLY_RECEIVED, FULFILLED, CANCELLED.");

        RuleFor(x => x.EtaStatus)
            .Must(s => string.IsNullOrWhiteSpace(s) || EtaStatuses.Contains(s.Trim().ToUpperInvariant()))
            .WithMessage("Trạng thái ETA phải thuộc một trong các giá trị: OVERDUE, DUE_SOON.");

        RuleFor(x => x)
            .Must(x => !x.FromEta.HasValue || !x.ToEta.HasValue || x.FromEta.Value.Date <= x.ToEta.Value.Date)
            .WithMessage("Từ ngày ETA không được lớn hơn Đến ngày ETA.");
    }
}
