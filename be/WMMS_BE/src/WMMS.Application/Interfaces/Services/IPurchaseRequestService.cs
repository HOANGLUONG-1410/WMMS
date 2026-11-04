using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.InventoryPr;

namespace WMMS.Application.Interfaces.Services;

/// <summary>
/// Dịch vụ quản lý Phiếu Yêu cầu Mua hàng gộp (PR) và luồng phê duyệt của Chủ xưởng (SC-15, UC-45..UC-48, BR-PUR-02).
/// </summary>
public interface IPurchaseRequestService
{
    /// <summary>
    /// UC-45: Danh sách PR có phân trang, lọc theo trạng thái/khoảng ngày/từ khóa.
    /// Khi <paramref name="restrictToApprovedOnly"/> = true (Purchasing), chỉ trả PR đã duyệt (APPROVED, PO_ISSUED) theo BR-PUR-02.
    /// </summary>
    Task<PagedResult<PurchaseRequestListItemDto>> GetPurchaseRequestsAsync(
        PurchaseRequestFilterDto filter,
        bool restrictToApprovedOnly,
        CancellationToken cancellationToken = default);

    /// <summary>UC-46: Chi tiết PR kèm dòng vật tư và đơn hàng nguồn.</summary>
    Task<PurchaseRequestDetailDto> GetPurchaseRequestByIdAsync(
        long prId,
        bool restrictToApprovedOnly,
        CancellationToken cancellationToken = default);

    /// <summary>UC-47: Chủ xưởng phê duyệt PR đang PENDING_APPROVAL (409 nếu PR đã được xử lý ở phiên khác).</summary>
    Task<PurchaseRequestDetailDto> ApprovePurchaseRequestAsync(
        long prId,
        ApprovePurchaseRequestDto request,
        long ownerUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>UC-48: Chủ xưởng từ chối PR đang PENDING_APPROVAL, bắt buộc có lý do.</summary>
    Task<PurchaseRequestDetailDto> RejectPurchaseRequestAsync(
        long prId,
        RejectPurchaseRequestDto request,
        long ownerUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}
