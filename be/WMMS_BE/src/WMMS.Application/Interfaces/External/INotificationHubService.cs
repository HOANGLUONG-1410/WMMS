namespace WMMS.Application.Interfaces.External;

/// <summary>
/// Dịch vụ phát thông báo thời gian thực (SignalR Real-time Notification) giữa các bộ phận trong nhà máy (QT-01, QT-02, QT-03, SC-17).
/// </summary>
public interface INotificationHubService
{
    /// <summary>
    /// Phát thông báo real-time tới bộ phận Kế hoạch Vật tư (Material Planning Staff - KHVT) và Ban Giám đốc (Owner)
    /// ngay sau khi Sales xác nhận đơn hàng từ Staging Grid và khởi tạo Order Space (QT-01, UC-33, BR-ORD-03).
    /// </summary>
    Task NotifyOrderSpaceCreatedAsync(
        string spaceKey,
        string poNumber,
        string customerName,
        int totalUnits,
        int lineItemsCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phát thông báo real-time khi Kỹ sư BOM chốt định mức sang trạng thái READY_FOR_PRODUCTION hoặc cảnh báo thiếu vật tư (QT-02, QT-03).
    /// </summary>
    Task NotifyBomApprovedAsync(
        long bomId,
        string productSku,
        bool hasShortage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phát thông báo real-time khi Chủ xưởng phê duyệt hoặc từ chối PR gộp ngày (SC-15, UC-47, UC-48):
    /// phê duyệt thông báo cho Mua hàng; từ chối thông báo lại cho KHVT kèm lý do.
    /// </summary>
    Task NotifyPurchaseRequestDecidedAsync(
        string prCode,
        bool approved,
        string? reasonOrNotes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phát thông báo real-time khi Mua hàng phát hành PO mới hoặc đổi ngày hẹn giao (SC-16, UC-52, UC-53):
    /// Thủ kho cập nhật lịch hàng về, KHVT/Owner theo dõi tiến độ.
    /// </summary>
    Task NotifyPurchaseOrderChangedAsync(
        string poCode,
        string supplierName,
        DateTime expectedArrivalDate,
        bool isNew,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phát thông báo real-time khi Thủ kho bấm Commit phiếu nhập kho GRN &amp; kích hoạt Auto-Allocation (SC-17, UC-57, BR-WHS-01).
    /// </summary>
    Task NotifyGoodsReceiptCommittedAsync(
        string grnCode,
        string poCode,
        int allocatedOrderCount,
        IReadOnlyList<string> readyOrderPoNumbers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phát thông báo real-time khi Quản đốc / Line Leader phát hành Lệnh sản xuất &amp; Tách lô Kanban (SC-19, UC-62).
    /// </summary>
    Task NotifyProductionOrderReleasedAsync(
        long orderItemId,
        string poNumber,
        string productSku,
        int totalBatches,
        int totalQty,
        bool isOverdriver,
        CancellationToken cancellationToken = default);
}
