using Microsoft.AspNetCore.SignalR;
using WMMS.Application.Interfaces.External;

namespace WMMS.API.Hubs;

/// <summary>
/// SignalR Hub phát thông báo thời gian thực cho các vai trò trong xưởng (Sales, Material Planning Staff, Purchasing, Warehouse, Owner).
/// Endpoint kết nối: <c>/hubs/notifications</c>.
/// </summary>
public class WmmsNotificationHub : Hub
{
    public async Task JoinRoleGroup(string roleCode)
    {
        if (!string.IsNullOrWhiteSpace(roleCode))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roleCode.Trim().ToUpperInvariant());
        }
    }
}

/// <summary>
/// Triển khai <see cref="INotificationHubService"/> qua <see cref="IHubContext{WmmsNotificationHub}"/>.
/// </summary>
public class SignalRNotificationHubService : INotificationHubService
{
    private readonly IHubContext<WmmsNotificationHub> _hubContext;

    public SignalRNotificationHubService(IHubContext<WmmsNotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyOrderSpaceCreatedAsync(
        string spaceKey,
        string poNumber,
        string customerName,
        int totalUnits,
        int lineItemsCount,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            EventType = "ORDER_SPACE_CREATED",
            SpaceKey = spaceKey,
            PoNumber = poNumber,
            CustomerName = customerName,
            TotalUnits = totalUnits,
            LineItemsCount = lineItemsCount,
            Message = $"[QT-01] Không gian đơn hàng mới {spaceKey} (PO: {poNumber} - {customerName}, {totalUnits} SP) đã được khởi tạo. Bộ phận KHVT vui lòng kiểm tra và bóc tách BOM.",
            TimestampUtc = DateTime.UtcNow
        };

        await _hubContext.Clients.All.SendAsync("ReceiveNotification", payload, cancellationToken);
    }

    public async Task NotifyBomApprovedAsync(
        long bomId,
        string productSku,
        bool hasShortage,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            EventType = "BOM_APPROVED",
            BomId = bomId,
            ProductSku = productSku,
            HasShortage = hasShortage,
            Message = hasShortage
                ? $"[QT-03] BOM #{bomId} (SKU: {productSku}) đã chốt nhưng phát hiện thiếu tồn kho khả dụng. Đã đưa vào danh sách chờ tổng hợp PR cuối ngày."
                : $"[QT-02] BOM #{bomId} (SKU: {productSku}) đã đủ vật tư khả dụng và sẵn sàng sản xuất (READY_FOR_PRODUCTION).",
            TimestampUtc = DateTime.UtcNow
        };

        await _hubContext.Clients.All.SendAsync("ReceiveNotification", payload, cancellationToken);
    }
}
