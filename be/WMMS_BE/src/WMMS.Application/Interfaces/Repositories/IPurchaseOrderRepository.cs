using WMMS.Domain.Entities.WarehousePo;
using WMMS.Domain.Enums;

namespace WMMS.Application.Interfaces.Repositories;

/// <summary>
/// Giao diện kho lưu trữ Đơn đặt hàng Nhà cung cấp (<c>purchase_orders</c>, <c>purchase_order_details</c>)
/// và Phiếu nhập kho (<c>goods_receipt_notes</c>) phục vụ SC-16 và SC-17.
/// </summary>
public interface IPurchaseOrderRepository
{
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
