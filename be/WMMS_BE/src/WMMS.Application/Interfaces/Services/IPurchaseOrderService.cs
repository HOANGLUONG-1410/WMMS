using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.Purchasing;

namespace WMMS.Application.Interfaces.Services;

/// <summary>
/// Dịch vụ lập Đơn đặt hàng Nhà cung cấp (PO) gom theo Supplier và theo dõi tiến độ ETA (SC-16, UC-49..UC-53, BR-PUR-02, BR-PUR-03).
/// </summary>
public interface IPurchaseOrderService
{
    /// <summary>UC-49: Hàng đợi các PR đã duyệt còn dòng chưa đặt hết hàng.</summary>
    Task<List<ApprovedRequisitionDto>> GetApprovedRequisitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>UC-50: Danh sách PO có phân trang, lọc theo NCC, trạng thái, tình trạng ETA, khoảng ngày giao và từ khóa.</summary>
    Task<PagedResult<PurchaseOrderListItemDto>> GetPurchaseOrdersAsync(PurchaseOrderFilterDto filter, CancellationToken cancellationToken = default);

    /// <summary>UC-51: Chi tiết PO kèm dòng vật tư, tiến độ nhận hàng và các phiếu nhập kho.</summary>
    Task<PurchaseOrderDetailDto> GetPurchaseOrderByIdAsync(long poId, CancellationToken cancellationToken = default);

    /// <summary>Thẻ KPI SCR-19: số PO theo trạng thái, quá hạn và sắp đến hạn.</summary>
    Task<PurchaseOrderSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-52: Tạo PO từ PR đã duyệt. Các dòng được gom theo Supplier, mỗi Supplier một PO ở trạng thái ISSUED.
    /// Khi mọi dòng PR đã được đặt đủ thì PR chuyển PO_ISSUED.
    /// </summary>
    Task<List<PurchaseOrderDetailDto>> CreatePurchaseOrdersAsync(
        CreatePurchaseOrdersRequestDto request,
        long purchasingUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>UC-53: Cập nhật ngày hẹn giao và ghi chú vận chuyển cho PO chưa nhận đủ.</summary>
    Task<PurchaseOrderDetailDto> UpdateEtaAsync(
        long poId,
        UpdatePoEtaDto request,
        long purchasingUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}
