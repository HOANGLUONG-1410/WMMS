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

    public async Task NotifyPurchaseRequestDecidedAsync(
        string prCode,
        bool approved,
        string? reasonOrNotes,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            EventType = approved ? "PR_APPROVED" : "PR_REJECTED",
            PrCode = prCode,
            Approved = approved,
            Detail = reasonOrNotes,
            Message = approved
                ? $"[SC-15] PR gộp {prCode} đã được Chủ xưởng phê duyệt. Bộ phận Mua hàng có thể phân nhóm theo NCC và phát hành PO."
                : $"[SC-15] PR gộp {prCode} đã bị Chủ xưởng từ chối. Lý do: {reasonOrNotes}. KHVT vui lòng điều chỉnh và gom lại.",
            TimestampUtc = DateTime.UtcNow
        };

        var targetRoles = approved
            ? new[] { "PURCHASING", "PLANNER", "OWNER" }
            : new[] { "PLANNER", "OWNER" };

        await _hubContext.Clients.Groups(targetRoles).SendAsync("ReceiveNotification", payload, cancellationToken);
    }

    public async Task NotifyPurchaseOrderChangedAsync(
        string poCode,
        string supplierName,
        DateTime expectedArrivalDate,
        bool isNew,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            EventType = isNew ? "PO_ISSUED" : "PO_ETA_UPDATED",
            PoCode = poCode,
            SupplierName = supplierName,
            ExpectedArrivalDate = expectedArrivalDate.ToString("yyyy-MM-dd"),
            Message = isNew
                ? $"[SC-16] PO {poCode} ({supplierName}) đã phát hành, dự kiến hàng về {expectedArrivalDate:dd/MM/yyyy}. Thủ kho vui lòng chuẩn bị tiếp nhận."
                : $"[SC-16] PO {poCode} ({supplierName}) đổi lịch giao hàng sang {expectedArrivalDate:dd/MM/yyyy}.",
            TimestampUtc = DateTime.UtcNow
        };

        await _hubContext.Clients.Groups(new[] { "WAREHOUSE", "PLANNER", "OWNER" })
            .SendAsync("ReceiveNotification", payload, cancellationToken);
    }

    public async Task NotifyGoodsReceiptCommittedAsync(
        string grnCode,
        string poCode,
        int allocatedOrderCount,
        IReadOnlyList<string> readyOrderPoNumbers,
        CancellationToken cancellationToken = default)
    {
        var readyMsg = readyOrderPoNumbers.Count > 0
            ? $" Có {readyOrderPoNumbers.Count} đơn hàng đã đủ 100% vật tư sẵn sàng sản xuất: {string.Join(", ", readyOrderPoNumbers)}."
            : string.Empty;

        var payload = new
        {
            EventType = "GOODS_RECEIPT_COMMITTED",
            GrnCode = grnCode,
            PoCode = poCode,
            AllocatedOrderCount = allocatedOrderCount,
            ReadyOrderPoNumbers = readyOrderPoNumbers,
            Message = $"[SC-17] Phiếu nhập kho {grnCode} (PO: {poCode}) đã được commit. Vật tư đã được tự động phân bổ vào {allocatedOrderCount} đơn hàng đang chờ.{readyMsg}",
            TimestampUtc = DateTime.UtcNow
        };

        await _hubContext.Clients.All.SendAsync("ReceiveNotification", payload, cancellationToken);
    }

    public async Task NotifyProductionOrderReleasedAsync(
        long orderItemId,
        string poNumber,
        string productSku,
        int totalBatches,
        int totalQty,
        bool isOverdriver,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            EventType = "PRODUCTION_ORDER_RELEASED",
            OrderItemId = orderItemId,
            PoNumber = poNumber,
            ProductSku = productSku,
            TotalBatches = totalBatches,
            TotalQty = totalQty,
            IsOverdriver = isOverdriver,
            Message = isOverdriver
                ? $"[SC-19] Lệnh sản xuất PO {poNumber} (SKU: {productSku}) đã được phát hành với {totalBatches} lô ({totalQty} SP) ở chế độ Overdrive. Thẻ lô được phép thực hiện công đoạn sơ chế phôi trước điểm dừng Hold Point."
                : $"[SC-19] Lệnh sản xuất PO {poNumber} (SKU: {productSku}) đã phát hành {totalBatches} thẻ lô Kanban ({totalQty} SP) thành công lên bảng điều độ phân xưởng.",
            TimestampUtc = DateTime.UtcNow
        };

        await _hubContext.Clients.All.SendAsync("ReceiveNotification", payload, cancellationToken);
    }

    public async Task NotifyHoldPointOverrideRequestedAsync(
        string batchCode,
        int holdPointStageSeq,
        string requesterName,
        string justification,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            EventType = "HOLD_POINT_OVERDRIVE_REQUESTED",
            BatchCode = batchCode,
            HoldPointStageSeq = holdPointStageSeq,
            RequesterName = requesterName,
            Justification = justification,
            Message = $"[SC-20] Tổ trưởng {requesterName} vừa gửi yêu cầu ghi đè điểm dừng Hold Point cho lô {batchCode} (Công đoạn #{holdPointStageSeq}). Lý do: {justification}.",
            TimestampUtc = DateTime.UtcNow
        };

        await _hubContext.Clients.Groups(new[] { "OWNER", "ADMIN", "PLANNER" })
            .SendAsync("ReceiveNotification", payload, cancellationToken);
    }

    public async Task NotifyHoldPointOverrideDecidedAsync(
        string batchCode,
        int holdPointStageSeq,
        bool isApproved,
        string approverName,
        string justification,
        CancellationToken cancellationToken = default)
    {
        var actionText = isApproved ? "PHÊ DUYỆT" : "TỪ CHỐI";
        var payload = new
        {
            EventType = isApproved ? "HOLD_POINT_OVERDRIVE_APPROVED" : "HOLD_POINT_OVERDRIVE_REJECTED",
            BatchCode = batchCode,
            HoldPointStageSeq = holdPointStageSeq,
            IsApproved = isApproved,
            ApproverName = approverName,
            Justification = justification,
            Message = $"[SC-20] Yêu cầu ghi đè điểm dừng Hold Point lô {batchCode} (Công đoạn #{holdPointStageSeq}) đã được Chủ xưởng {approverName} {actionText}. Ghi chú: {justification}.",
            TimestampUtc = DateTime.UtcNow
        };

        await _hubContext.Clients.Groups(new[] { "LEADER", "PLANNER", "OWNER", "ADMIN" })
            .SendAsync("ReceiveNotification", payload, cancellationToken);
    }
}
