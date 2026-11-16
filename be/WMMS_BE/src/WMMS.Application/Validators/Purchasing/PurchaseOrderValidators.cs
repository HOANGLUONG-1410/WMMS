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
            .WithMessage("Vui lòng chọn phiếu yêu cầu mua hàng cần lập đơn đặt hàng.");

        RuleFor(x => x.Lines)
            .NotEmpty()
            .WithMessage("Vui lòng chọn ít nhất một mặt hàng để phát hành đơn đặt hàng.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.PrItemId)
                .GreaterThan(0)
                .WithMessage("Vui lòng chọn dòng vật tư hợp lệ từ phiếu yêu cầu mua hàng.");

            line.RuleFor(l => l.SupplierId)
                .GreaterThan(0)
                .WithMessage("Mỗi mặt hàng đặt mua bắt buộc phải chọn một nhà cung cấp hợp lệ.");

            line.RuleFor(l => l.OrderedQty)
                .GreaterThan(0)
                .WithMessage("Số lượng đặt hàng phải lớn hơn 0.");

            line.RuleFor(l => l.UnitPrice)
                .GreaterThan(0)
                .WithMessage("Đơn giá thỏa thuận phải lớn hơn 0.");
        });

        RuleFor(x => x.Lines)
            .Must(lines => lines.GroupBy(l => (l.PrItemId, l.SupplierId)).All(g => g.Count() == 1))
            .WithMessage("Không được chọn trùng cùng một mặt hàng và nhà cung cấp trên nhiều dòng; vui lòng cộng gộp số lượng.");

        RuleFor(x => x.SupplierTerms)
            .NotEmpty()
            .WithMessage("Vui lòng nhập ngày hẹn giao hàng cho từng nhà cung cấp.");

        RuleFor(x => x.SupplierTerms)
            .Must(t => t.GroupBy(s => s.SupplierId).All(g => g.Count() == 1))
            .WithMessage("Mỗi nhà cung cấp chỉ được khai báo một mục điều khoản giao hàng.");

        RuleForEach(x => x.SupplierTerms).ChildRules(terms =>
        {
            terms.RuleFor(t => t.SupplierId)
                .GreaterThan(0)
                .WithMessage("Vui lòng chọn nhà cung cấp hợp lệ trong mục điều khoản giao hàng.");

            terms.RuleFor(t => t.ExpectedArrivalDate)
                .Must(d => d.Date >= DateTime.UtcNow.Date)
                .WithMessage("Ngày nhà cung cấp hẹn giao hàng không được nằm trong quá khứ.");

            terms.RuleFor(t => t.Terms)
                .MaximumLength(1000)
                .WithMessage("Điều khoản thanh toán và giao hàng không được vượt quá 1.000 ký tự.");
        });

        RuleFor(x => x)
            .Must(x =>
            {
                var lineSuppliers = x.Lines.Select(l => l.SupplierId).ToHashSet();
                var termSuppliers = x.SupplierTerms.Select(t => t.SupplierId).ToHashSet();
                return lineSuppliers.SetEquals(termSuppliers);
            })
            .WithMessage("Danh sách điều khoản giao hàng phải khớp với tất cả các nhà cung cấp có mặt hàng đặt mua.");
    }
}

/// <summary>Kiểm tra cập nhật lịch giao (UC-53): ngày hẹn giao không ở quá khứ, ghi chú tối đa 500 ký tự.</summary>
public class UpdatePoEtaValidator : AbstractValidator<UpdatePoEtaDto>
{
    public UpdatePoEtaValidator()
    {
        RuleFor(x => x.EffectiveEtaDate)
            .Must(d => d != default && d.Date >= DateTime.UtcNow.Date)
            .WithMessage("Ngày nhà cung cấp hẹn giao hàng không được bỏ trống hoặc nằm trong quá khứ.");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Ghi chú vận chuyển hoặc lý do dời lịch không được vượt quá 500 ký tự.");
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
            .WithMessage("Trạng thái đơn mua hàng cần lọc không hợp lệ (chỉ nhận: Đã phát hành, Nhận một phần, Đã nhận đủ, Đã hủy).");

        RuleFor(x => x.EtaStatus)
            .Must(s => string.IsNullOrWhiteSpace(s) || EtaStatuses.Contains(s.Trim().ToUpperInvariant()))
            .WithMessage("Bộ lọc tiến độ giao hàng (ETA) không hợp lệ (chỉ nhận: Quá hạn hoặc Sắp đến hạn).");

        RuleFor(x => x)
            .Must(x => !x.FromEta.HasValue || !x.ToEta.HasValue || x.FromEta.Value.Date <= x.ToEta.Value.Date)
            .WithMessage("Ngày bắt đầu lọc lịch giao hàng không được lớn hơn ngày kết thúc.");
    }
}
