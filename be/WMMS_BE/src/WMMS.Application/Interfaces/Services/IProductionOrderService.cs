using WMMS.Application.DTOs.KanbanQc;

namespace WMMS.Application.Interfaces.Services;

/// <summary>
/// Giao diện Dịch vụ Điều độ &amp; Phát hành Lệnh sản xuất, Tách lô thông minh (Phân hệ 8 - SC-18, SC-19, UC-62).
/// </summary>
public interface IProductionOrderService
{
    /// <summary>
    /// Thuật toán chia nhỏ lệnh sản xuất thành các lô/thẻ công đoạn (Lots) theo công suất trạm hoặc cấu hình tùy biến,
    /// sinh mã định danh duy nhất chuẩn hóa và phát hành Production Order (SC-19, UC-62, BR-005).
    /// </summary>
    Task<ProductionOrderSummaryDto> GenerateProductionOrderAndSplitLotsAsync(
        GenerateBatchesRequestDto request,
        long currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách thẻ lô sản xuất Kanban theo mã OrderItemId.
    /// </summary>
    Task<List<ProductionBatchDto>> GetBatchesByOrderItemIdAsync(
        long orderItemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách thẻ lô sản xuất Kanban theo mã đơn hàng OrderId.
    /// </summary>
    Task<List<ProductionBatchDto>> GetBatchesByOrderIdAsync(
        long orderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách thẻ lô sản xuất Kanban theo mã Không gian đơn hàng (Order Space Key).
    /// </summary>
    Task<List<ProductionBatchDto>> GetBatchesBySpaceKeyAsync(
        string spaceKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy thông tin chi tiết một thẻ lô sản xuất theo BatchId (kèm toàn bộ các công đoạn và cổng QC).
    /// </summary>
    Task<ProductionBatchDto?> GetBatchByIdAsync(
        long batchId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh mục Trạm gia công tiêu chuẩn của xưởng gỗ (SC-18, Workstation Catalog).
    /// </summary>
    List<StandardWorkstationDto> GetStandardWorkstations();

    /// <summary>
    /// Lấy danh mục Điểm kiểm tra chất lượng &amp; Trạm dừng kỹ thuật bắt buộc (SC-18, QC Gate Catalog).
    /// </summary>
    List<StandardQualityGateDto> GetStandardQualityGates();

    /// <summary>
    /// SC-19 &amp; QT-05: Cưỡng chế tuần tự di chuyển thẻ công đoạn trên Kanban (S -> S + 1).
    /// Bắt buộc điều kiện PassedQty == BatchQty tại công đoạn hiện tại (100% QC Pass - BR-008) mới cho phép chuyển bước.
    /// Tự động kích hoạt khóa chốt chặn kỹ thuật (Hold Point Lock Engine) nếu là lô Overdrive chưa xuất phụ kiện.
    /// </summary>
    Task<ProductionBatchDto> AdvanceStepAsync(
        long batchId,
        AdvanceStepRequestDto request,
        long currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// SC-19, QT-06 &amp; KBN01: Ghi nhận kiểm định chất lượng tại cổng KCS (100% Rule)
    /// và thực thi thuật toán tách nhánh 97%-3% (sinh thẻ nhánh phụ REWORK_SUB_CARD nếu phát hiện lỗi).
    /// </summary>
    Task<QcInspectionResultDto> RecordQcInspectionAsync(
        long batchId,
        RecordQcInspectionDto request,
        long currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// SC-20, UC-63 &amp; QT-06: Tổ trưởng sản xuất (Line Leader) gửi yêu cầu kích hoạt chế độ chạy trước Overdrive
    /// khi cận hạn ETD nhưng thiếu phụ kiện ốc vít/sơn, thiết lập trạm dừng Hold Point.
    /// </summary>
    Task<OverdriveOperationResultDto> RequestOverdriveAsync(
        long batchId,
        RequestOverdriveDto request,
        long currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// SC-20, KBN02 &amp; QT-06: Chủ xưởng (Factory Owner) phê duyệt hoặc từ chối yêu cầu Overdrive,
    /// cho phép sơ chế phôi gỗ thô trước trạm dừng Hold Point và lưu vết Audit Log.
    /// </summary>
    Task<OverdriveOperationResultDto> ApproveOverdriveAsync(
        long batchId,
        ApproveOverdriveDto request,
        long currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// SC-20: Mở khóa chốt chặn Hold Point khi Thủ kho bấm xác nhận xuất kho phụ kiện bổ sung (Goods Issue).
    /// </summary>
    Task<OverdriveOperationResultDto> UnlockHoldPointAsync(
        long batchId,
        long currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// SC-20b, KBN01 &amp; QT-06: Nghiệm thu lại (Re-inspection) cho thẻ nhánh phụ 3% Rework / Scrap Replenish
    /// sau khi thợ mộc xử lý xong lỗi phôi gỗ hoặc bù phôi mới.
    /// </summary>
    Task<ReinspectSubBatchResultDto> ReinspectSubBatchAsync(
        long subBatchId,
        ReinspectSubBatchDto request,
        long currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// SC-20b: Tái hòa nhập / Gộp lô (Merge Lot) thẻ nhánh phụ đã sửa chữa đạt chuẩn vào thẻ chính
    /// khi cả hai thẻ đang ở cùng công đoạn (CurrentStageSeq) và thẻ chính chưa hoàn tất.
    /// </summary>
    Task<MergeSubBatchResultDto> MergeSubBatchAsync(
        long subBatchId,
        MergeSubBatchDto request,
        long currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// SC-20b: Cho phép thẻ nhánh phụ 3% chuyển tiếp độc lập sang công đoạn tiếp theo
    /// khi thẻ chính đã chạy trước sang công đoạn khác hoặc theo chỉ đạo điều độ phân xưởng.
    /// </summary>
    Task<ContinueSubBatchResultDto> ContinueSubBatchIndependentlyAsync(
        long subBatchId,
        ContinueSubBatchIndependentlyDto request,
        long currentUserId,
        CancellationToken cancellationToken = default);
}

