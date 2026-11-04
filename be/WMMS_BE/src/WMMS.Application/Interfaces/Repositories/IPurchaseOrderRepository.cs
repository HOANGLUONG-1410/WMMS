using WMMS.Domain.Entities.WarehousePo;
using WMMS.Domain.Enums;

namespace WMMS.Application.Interfaces.Repositories;

/// <summary>
/// Giao diện kho lưu trữ Đơn đặt hàng Nhà cung cấp (<c>purchase_orders</c>, <c>purchase_order_details</c>)
/// và Phiếu nhập kho (<c>goods_receipt_notes</c>) phục vụ SC-16 và SC-17.
/// </summary>
public interface IPurchaseOrderRepository
{
    /// <summary>
    /// UC-50: Tìm PO có phân trang, lọc ở tầng CSDL. <paramref name="overdueBefore"/>: chỉ lấy PO chưa nhận đủ có ETA trước ngày này;
    /// <paramref name="dueSoonFrom"/>/<paramref name="dueSoonTo"/>: chỉ lấy PO chưa nhận đủ có ETA trong khoảng đó.
    /// </summary>
    Task<(List<PurchaseOrder> Items, int TotalCount)> SearchAsync(
        long? supplierId,
        PoStatusEnum? status,
        DateTime? fromEta,
        DateTime? toEta,
        DateTime? overdueBefore,
        DateTime? dueSoonFrom,
        DateTime? dueSoonTo,
        string? keyword,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Số PO theo từng trạng thái, quá hạn (ETA &lt; today) và sắp đến hạn (today..dueSoonTo) cho thẻ KPI.</summary>
    Task<(int Issued, int Partial, int Fulfilled, int Cancelled, int Overdue, int DueSoon)> GetStatusCountsAsync(
        DateTime today,
        DateTime dueSoonTo,
        CancellationToken cancellationToken = default);

    /// <summary>Tổng lượng đã đặt theo (PrId, MaterialId) trong các PO chưa hủy, dùng để tính lượng còn có thể đặt của dòng PR.</summary>
    Task<Dictionary<(long PrId, long MaterialId), decimal>> GetOrderedQtyByPrAsync(
        IReadOnlyCollection<long> prIds,
        CancellationToken cancellationToken = default);

    /// <summary>Số PO đã có mã bắt đầu bằng <paramref name="codePrefix"/> (để sinh mã PO-SUP-yyyyMMdd-NNN).</summary>
    Task<int> CountByCodePrefixAsync(string codePrefix, CancellationToken cancellationToken = default);

    /// <summary>Lấy Nhà cung cấp theo danh sách ID (SupplierId → Supplier).</summary>
    Task<Dictionary<long, Supplier>> GetSuppliersByIdsAsync(IReadOnlyCollection<long> supplierIds, CancellationToken cancellationToken = default);

    Task<PurchaseOrder?> GetByIdAsync(long poId, CancellationToken cancellationToken = default);
    Task<PurchaseOrder?> GetByCodeAsync(string poCode, CancellationToken cancellationToken = default);
    Task<List<PurchaseOrder>> GetByPrIdAsync(long prId, CancellationToken cancellationToken = default);
    Task<List<PurchaseOrder>> GetBySupplierIdAsync(long supplierId, CancellationToken cancellationToken = default);
    Task<List<PurchaseOrder>> GetAllAsync(PoStatusEnum? status = null, CancellationToken cancellationToken = default);
    Task AddAsync(PurchaseOrder po, CancellationToken cancellationToken = default);
    Task UpdateAsync(PurchaseOrder po);
    Task AddDetailsRangeAsync(IEnumerable<PurchaseOrderDetail> details, CancellationToken cancellationToken = default);
    Task<GoodsReceipt?> GetGoodsReceiptByIdAsync(long grnId, CancellationToken cancellationToken = default);
    Task<List<GoodsReceipt>> GetGoodsReceiptsByPoIdAsync(long poId, CancellationToken cancellationToken = default);
    Task AddGoodsReceiptAsync(GoodsReceipt grn, CancellationToken cancellationToken = default);
}
