using WMMS.Application.DTOs.ProductionTracking;

namespace WMMS.Application.Interfaces.Services;

/// <summary>
/// Giao diện dịch vụ nghiệp vụ Hoàn tất Lô sản xuất &amp; Nhập kho Thành phẩm (Finished Goods Receipt - UC-63, UC-64).
/// Kiểm tra toàn bộ công đoạn định tuyến của lô đã đạt QC (PASSED), xác nhận nhập kho thành phẩm và kích hoạt đồng bộ tiến độ đơn hàng.
/// </summary>
public interface IFinishedGoodsService
{
    /// <summary>
    /// Lấy danh sách các lô sản xuất đã hoàn tất toàn bộ công đoạn (sẵn sàng nhập kho thành phẩm) hoặc đã nhập kho thành phẩm (`DONE`).
    /// </summary>
    Task<List<FinishedGoodsBatchSummaryDto>> GetFinishedGoodsBatchesAsync(
        bool onlyReadyForReceipt = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xác nhận hoàn tất lô sản xuất tại công đoạn đóng gói cuối cùng, chuyển trạng thái lô sang `DONE`
    /// và tự động tính toán, đồng bộ trạng thái hoàn thành của `OrderItem` và `CustomerOrder`.
    /// </summary>
    Task<FinishedGoodsReceiptResultDto> CompleteBatchAndReceiveFinishedGoodsAsync(
        long batchId,
        CompleteFinishedBatchRequestDto request,
        long currentUserId,
        CancellationToken cancellationToken = default);
}
