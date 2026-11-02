namespace WMMS.Application.Interfaces.External;

/// <summary>
/// Dịch vụ phát thông báo thời gian thực (SignalR Real-time Notification) giữa các bộ phận trong nhà máy (QT-01, QT-02, QT-03).
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
}
