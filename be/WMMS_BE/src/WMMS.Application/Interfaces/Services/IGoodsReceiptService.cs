using WMMS.Application.DTOs.Warehouse;

namespace WMMS.Application.Interfaces.Services;

/// <summary>
/// Dịch vụ Nhập kho theo đơn PO (GRN) & Phân bổ tự động vật tư vào Staging (SC-17, UC-56, UC-57, BR-WHS-01).
/// </summary>
public interface IGoodsReceiptService
{
    /// <summary>
    /// Đối chiếu số lượng thực nhận với PO, cộng tồn vật lý, ghi sổ kho (INBOUND),
    /// rồi kích hoạt Auto-Allocation theo ETD tăng dần trong cùng một giao dịch nguyên tử.
    /// </summary>
    Task<GoodsReceiptResultDto> CommitGoodsReceiptAsync(
        CommitGoodsReceiptRequestDto request,
        long warehouseKeeperId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>Danh sách PO đang mở (ISSUED / PARTIALLY_RECEIVED) cùng số lượng còn phải nhận.</summary>
    Task<IReadOnlyList<OpenPurchaseOrderDto>> GetOpenPurchaseOrdersAsync(
        string? search,
        CancellationToken cancellationToken = default);
}
