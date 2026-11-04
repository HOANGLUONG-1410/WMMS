using WMMS.Domain.Entities.InventoryPr;
using WMMS.Domain.Enums;

namespace WMMS.Application.Interfaces.Repositories;

/// <summary>
/// Giao diện kho lưu trữ Phiếu Yêu cầu Mua hàng Tổng hợp (<c>consolidated_prs</c>, <c>consolidated_pr_items</c>)
/// phục vụ quy tắc BR-PUR-01 (Gom PR tổng hợp trong ngày) và phân hệ SC-15.
/// </summary>
public interface IPurchaseRequestRepository
{
    Task<PurchaseRequest?> GetByIdAsync(long prId, CancellationToken cancellationToken = default);
    Task<PurchaseRequest?> GetByCodeAsync(string prCode, CancellationToken cancellationToken = default);
    Task<PurchaseRequest?> GetPendingPrByDateAsync(DateTime aggregationDate, CancellationToken cancellationToken = default);
    Task<List<PurchaseRequest>> GetAllAsync(PrStatusEnum? status = null, CancellationToken cancellationToken = default);
    Task<int> CountByDateAsync(DateTime aggregationDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-45: Tìm PR có phân trang, lọc ở tầng CSDL theo trạng thái, khoảng ngày tổng hợp và từ khóa (mã PR, mã/tên vật tư).
    /// </summary>
    Task<(List<PurchaseRequest> Items, int TotalCount)> SearchAsync(
        IReadOnlyCollection<PrStatusEnum>? statuses,
        DateTime? fromDate,
        DateTime? toDate,
        string? keyword,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-47/UC-48: Chuyển trạng thái PR một cách nguyên tử bằng câu lệnh UPDATE có điều kiện
    /// (<c>WHERE pr_status = expectedStatus</c>). Trả về false nếu PR đã bị phiên khác xử lý (UC047.E1).
    /// </summary>
    Task<bool> TryTransitionStatusAsync(
        long prId,
        PrStatusEnum expectedStatus,
        PrStatusEnum newStatus,
        long reviewerUserId,
        string? rejectionReason,
        DateTime reviewedAt,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// SC-16: Chuyển PR từ APPROVED sang PO_ISSUED (đã đặt đủ hàng) bằng UPDATE có điều kiện;
    /// không đổi người duyệt/thời điểm duyệt. Trả false nếu PR không còn ở trạng thái APPROVED.
    /// </summary>
    Task<bool> TryMarkPoIssuedAsync(long prId, DateTime updatedAt, CancellationToken cancellationToken = default);

    Task AddAsync(PurchaseRequest pr, CancellationToken cancellationToken = default);
    Task UpdateAsync(PurchaseRequest pr);
    Task AddItemAsync(PurchaseRequestItem item, CancellationToken cancellationToken = default);
    Task AddItemsRangeAsync(IEnumerable<PurchaseRequestItem> items, CancellationToken cancellationToken = default);
    Task RemoveItemsRangeAsync(IEnumerable<PurchaseRequestItem> items);
}
