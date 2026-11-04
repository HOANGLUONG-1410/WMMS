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
    Task AddAsync(PurchaseRequest pr, CancellationToken cancellationToken = default);
    Task UpdateAsync(PurchaseRequest pr);
    Task AddItemAsync(PurchaseRequestItem item, CancellationToken cancellationToken = default);
    Task AddItemsRangeAsync(IEnumerable<PurchaseRequestItem> items, CancellationToken cancellationToken = default);
    Task RemoveItemsRangeAsync(IEnumerable<PurchaseRequestItem> items);
}
