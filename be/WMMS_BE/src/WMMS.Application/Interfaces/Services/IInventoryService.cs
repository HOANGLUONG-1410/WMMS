using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.InventoryPr;

namespace WMMS.Application.Interfaces.Services;

/// <summary>
/// Giao diện dịch vụ Quản lý Tồn kho 3 cấp &amp; Sổ kho điện tử (SC-12, SC-13).
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// SC-12: Lấy danh sách tồn kho 3 cấp (Physical, Allocated, Available) của tất cả vật tư,
    /// hỗ trợ lọc theo nhóm vật tư (Gỗ/LUMBER, Sơn/FINISH_PAINT, Phụ kiện/HARDWARE, Đóng gói/PACKAGING) và tìm kiếm.
    /// </summary>
    Task<PagedResult<InventoryStockDto>> GetStocksAsync(
        InventoryStockFilterDto filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// SC-12: Xem chi tiết tồn kho 3 cấp của một vật tư và danh sách các đơn hàng đang giữ chỗ (Allocated/Reserved) vật tư đó.
    /// Ném 404 (MSG10) nếu mã vật tư không tồn tại.
    /// </summary>
    Task<InventoryStockDetailDto> GetStockByMaterialIdAsync(
        long materialId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// SC-13: Truy vấn lịch sử giao dịch sổ kho từ bảng <c>inventory_transactions</c> (phân trang, lọc theo ngày, loại giao dịch, vật tư).
    /// </summary>
    Task<PagedResult<InventoryTransactionDto>> GetLedgerAsync(
        InventoryLedgerFilterDto filter,
        CancellationToken cancellationToken = default);
}
