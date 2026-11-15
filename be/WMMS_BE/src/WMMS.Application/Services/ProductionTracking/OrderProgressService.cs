using System.Text.Json;
using WMMS.Application.DTOs.ProductionTracking;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Entities.Orders;
using WMMS.Domain.Enums;
using WMMS.Domain.Exceptions;

namespace WMMS.Application.Services.ProductionTracking;

/// <summary>
/// Dịch vụ Quản lý Tiến độ Đơn hàng &amp; Tính toán Tỷ lệ Hoàn thành Tự động (Order Progress Tracking Engine - UC-64).
/// Tổng hợp tiến độ sản xuất thực tế từ các thẻ/lô Kanban (`MAIN_CARD`, `REWORK_SUB_CARD`, `REPLENISH_LOT`)
/// lên cấp dòng sản phẩm (`OrderItem`) và đơn hàng bán (`CustomerOrder`), đồng thời cảnh báo rủi ro trễ hạn giao hàng ETD.
/// </summary>
public class OrderProgressService : IOrderProgressService
{
    private const int DefaultEtdWarningDays = 5;
    private readonly IUnitOfWork _unitOfWork;

    public OrderProgressService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<OrderProductionProgressDto> GetOrderProgressByIdAsync(
        long orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.OrderItems.GetOrderWithFullProgressGraphAsync(orderId, cancellationToken);
        if (order == null)
        {
            throw AppException.NotFound(
                $"Không tìm thấy đơn hàng với mã định danh ID = {orderId}.");
        }

        var warningDays = await GetEtdWarningDaysAsync();
        return BuildOrderProgressDto(order, warningDays);
    }

    /// <inheritdoc />
    public async Task<List<OrderProductionProgressDto>> GetAllOrdersProgressAsync(
        string? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        var orders = await _unitOfWork.OrderItems.GetAllOrdersWithProgressGraphAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            if (!Enum.TryParse<OrderStatusEnum>(statusFilter.Trim(), ignoreCase: true, out var parsedStatus))
            {
                throw new AppException(
                    $"Bộ lọc trạng thái đơn hàng '{statusFilter}' không hợp lệ. Các giá trị hợp lệ: CONFIRMED, IN_PLANNING, IN_PRODUCTION, COMPLETED, CANCELLED, DRAFT.",
                    400,
                    MessageCodes.Msg13ValidationError);
            }

            orders = orders.Where(o => o.Status == parsedStatus).ToList();
        }

        var warningDays = await GetEtdWarningDaysAsync();
        return orders
            .Select(o => BuildOrderProgressDto(o, warningDays))
            .OrderBy(o => o.EtdDate)
            .ThenBy(o => o.OrderId)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<OrderProductionProgressDto> SynchronizeOrderProgressAsync(
        long orderId,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.OrderItems.GetOrderWithFullProgressGraphAsync(orderId, cancellationToken);
        if (order == null)
        {
            throw AppException.NotFound(
                $"Không tìm thấy đơn hàng với mã định danh ID = {orderId}.");
        }

        if (order.Status == OrderStatusEnum.CANCELLED)
        {
            throw new AppException(
                $"Đơn hàng '{order.PoNumber}' đã bị hủy (CANCELLED) nên không thể đồng bộ tiến độ sản xuất.",
                400,
                MessageCodes.Msg13ValidationError);
        }

        var now = DateTime.UtcNow;
        var previousOrderStatus = order.Status;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            foreach (var item in order.OrderItems)
            {
                var doneQty = item.ProductionBatches
                    .Where(b => b.BatchStatus == BatchStatusEnum.DONE &&
                                (b.BatchType == BatchTypeEnum.MAIN_CARD ||
                                 b.BatchType == BatchTypeEnum.REWORK_SUB_CARD ||
                                 b.BatchType == BatchTypeEnum.REPLENISH_LOT))
                    .Sum(b => b.BatchQty);

                if (item.OrderedQty > 0 && doneQty >= item.OrderedQty)
                {
                    item.Status = OrderItemStatusEnum.DONE;
                }
                else if (item.ProductionBatches.Any(b => b.BatchStatus != BatchStatusEnum.LOCKED))
                {
                    item.Status = OrderItemStatusEnum.IN_PROD;
                }

                await _unitOfWork.OrderItems.UpdateAsync(item);
            }

            if (order.OrderItems.Count > 0 && order.OrderItems.All(oi => oi.Status == OrderItemStatusEnum.DONE))
            {
                order.Status = OrderStatusEnum.COMPLETED;
            }
            else if (order.OrderItems.Any(oi => oi.Status == OrderItemStatusEnum.IN_PROD || oi.Status == OrderItemStatusEnum.DONE))
            {
                order.Status = OrderStatusEnum.IN_PRODUCTION;
            }

            await _unitOfWork.OrderItems.UpdateOrderAsync(order);

            if (order.Status != previousOrderStatus)
            {
                await _unitOfWork.Boms.AddAuditLogAsync(new AuditLog
                {
                    UserId = currentUserId > 0 ? currentUserId : null,
                    ActionType = AuditActionConstants.OrderProgressSynchronized,
                    EntityName = "CustomerOrder",
                    EntityId = order.PoNumber,
                    BeforeState = JsonSerializer.Serialize(new { OrderStatus = previousOrderStatus.ToString() }),
                    AfterState = JsonSerializer.Serialize(new { OrderStatus = order.Status.ToString() }),
                    Reason = $"Đồng bộ tự động trạng thái tiến độ đơn hàng {order.PoNumber} từ {previousOrderStatus} sang {order.Status}.",
                    CreatedAt = now
                });
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

        var warningDays = await GetEtdWarningDaysAsync();
        return BuildOrderProgressDto(order, warningDays);
    }

    internal static OrderProductionProgressDto BuildOrderProgressDto(CustomerOrder order, int etdWarningDays)
    {
        var today = DateTime.UtcNow.Date;
        var daysUntilEtd = (order.EtdDate.Date - today).Days;

        var itemDtos = new List<OrderItemProgressDto>();
        int totalOrderedUnits = 0;
        int totalCompletedUnits = 0;
        int totalInProgressUnits = 0;

        foreach (var item in order.OrderItems.OrderBy(i => i.OrderItemId))
        {
            var batches = item.ProductionBatches.OrderBy(b => b.BatchId).ToList();

            var completedQty = batches
                .Where(b => b.BatchStatus == BatchStatusEnum.DONE &&
                            (b.BatchType == BatchTypeEnum.MAIN_CARD ||
                             b.BatchType == BatchTypeEnum.REWORK_SUB_CARD ||
                             b.BatchType == BatchTypeEnum.REPLENISH_LOT))
                .Sum(b => b.BatchQty);

            var cappedCompletedQty = item.OrderedQty > 0
                ? Math.Min(item.OrderedQty, completedQty)
                : completedQty;

            var inProgressQty = batches
                .Where(b => b.BatchStatus == BatchStatusEnum.IN_PROGRESS || b.BatchStatus == BatchStatusEnum.QC_PENDING)
                .Sum(b => b.BatchQty);

            var blockedQty = batches
                .Where(b => b.BatchStatus == BatchStatusEnum.HOLD_POINT_BLOCKED)
                .Sum(b => b.BatchQty);

            var remainingQty = Math.Max(0, item.OrderedQty - cappedCompletedQty);
            var itemCompletionPercent = item.OrderedQty > 0
                ? Math.Min(100m, Math.Round((decimal)cappedCompletedQty * 100m / item.OrderedQty, 2))
                : 0m;

            var batchDtos = batches.Select(b =>
            {
                var steps = b.BatchRoutingSteps.OrderBy(s => s.StageSequence).ToList();
                var passedSteps = steps.Count(s => s.StepStatus == StepStatusEnum.PASSED);
                var totalSteps = steps.Count;
                var stagePercent = totalSteps > 0
                    ? Math.Round((decimal)passedSteps * 100m / totalSteps, 2)
                    : (b.BatchStatus == BatchStatusEnum.DONE ? 100m : 0m);

                var currentStep = steps.FirstOrDefault(s => s.StageSequence == b.CurrentStageSeq)
                                  ?? steps.LastOrDefault();
                var initialQty = steps.FirstOrDefault()?.TargetQty ?? b.BatchQty;

                return new BatchStageProgressDto
                {
                    BatchId = b.BatchId,
                    BatchCode = b.BatchCode,
                    BatchType = b.BatchType.ToString(),
                    BatchStatus = b.BatchStatus.ToString(),
                    InitialQty = initialQty,
                    CurrentQty = b.BatchQty,
                    CurrentStageCode = currentStep?.StageName ?? $"STAGE_{b.CurrentStageSeq}",
                    PassedStepsCount = passedSteps,
                    TotalStepsCount = totalSteps,
                    StageProgressPercent = stagePercent
                };
            }).ToList();

            itemDtos.Add(new OrderItemProgressDto
            {
                OrderItemId = item.OrderItemId,
                ProductId = item.ProductId,
                ProductCode = item.Product?.ProductSku ?? string.Empty,
                ProductName = item.Product?.ProductName ?? string.Empty,
                OrderedQty = item.OrderedQty,
                CompletedQty = cappedCompletedQty,
                InProgressQty = inProgressQty,
                BlockedQty = blockedQty,
                RemainingQty = remainingQty,
                CompletionPercent = itemCompletionPercent,
                ItemStatus = item.Status.ToString(),
                TotalBatches = batches.Count,
                DoneBatches = batches.Count(b => b.BatchStatus == BatchStatusEnum.DONE),
                BlockedBatches = batches.Count(b => b.BatchStatus == BatchStatusEnum.HOLD_POINT_BLOCKED),
                Batches = batchDtos
            });

            totalOrderedUnits += item.OrderedQty;
            totalCompletedUnits += cappedCompletedQty;
            totalInProgressUnits += inProgressQty;
        }

        var overallCompletionPercent = totalOrderedUnits > 0
            ? Math.Min(100m, Math.Round((decimal)totalCompletedUnits * 100m / totalOrderedUnits, 2))
            : 0m;

        var completedItemsCount = itemDtos.Count(i => i.ItemStatus == OrderItemStatusEnum.DONE.ToString() || i.CompletionPercent >= 100m);

        string etdRiskLevel;
        if (order.Status == OrderStatusEnum.COMPLETED || (itemDtos.Count > 0 && completedItemsCount == itemDtos.Count))
        {
            etdRiskLevel = "COMPLETED";
        }
        else if (daysUntilEtd < 0)
        {
            etdRiskLevel = "OVERDUE";
        }
        else if (daysUntilEtd <= etdWarningDays)
        {
            etdRiskLevel = "WARNING";
        }
        else
        {
            etdRiskLevel = "ON_TRACK";
        }

        return new OrderProductionProgressDto
        {
            OrderId = order.OrderId,
            PoNumber = order.PoNumber,
            CustomerName = order.Customer?.CompanyName ?? string.Empty,
            SpaceId = order.OrderSpace?.SpaceId,
            OrderStatus = order.Status.ToString(),
            OrderDate = order.OrderDate,
            EtdDate = order.EtdDate,
            DaysUntilEtd = daysUntilEtd,
            EtdRiskLevel = etdRiskLevel,
            TotalOrderedUnits = totalOrderedUnits,
            TotalCompletedUnits = totalCompletedUnits,
            TotalInProgressUnits = totalInProgressUnits,
            OverallCompletionPercent = overallCompletionPercent,
            TotalOrderItems = itemDtos.Count,
            CompletedOrderItems = completedItemsCount,
            Items = itemDtos
        };
    }

    private async Task<int> GetEtdWarningDaysAsync()
    {
        var parsedDays = await _unitOfWork.SystemConfigs.GetIntConfigAsync(SystemConfigKeys.EtdWarningDays, DefaultEtdWarningDays);
        return parsedDays > 0 ? parsedDays : DefaultEtdWarningDays;
    }
}
