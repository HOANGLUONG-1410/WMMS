using System.Text.Json;
using WMMS.Application.DTOs.ProductionTracking;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Enums;
using WMMS.Domain.Exceptions;

namespace WMMS.Application.Services.ProductionTracking;

/// <summary>
/// Dịch vụ Hoàn tất Lô sản xuất &amp; Nhập kho Thành phẩm (Finished Goods Receipt - UC-63, UC-64).
/// Đảm nhận việc kiểm tra toàn bộ các bước công đoạn định tuyến của lô sản xuất đã vượt qua kiểm định chất lượng (PASSED),
/// xác nhận bàn giao nhập kho thành phẩm (`DONE`), và tự động kích hoạt đồng bộ tiến độ của `OrderItem` cũng như `CustomerOrder`.
/// </summary>
public class FinishedGoodsService : IFinishedGoodsService
{
    private readonly IUnitOfWork _unitOfWork;

    public FinishedGoodsService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<List<FinishedGoodsBatchSummaryDto>> GetFinishedGoodsBatchesAsync(
        bool onlyReadyForReceipt = false,
        CancellationToken cancellationToken = default)
    {
        var allBatches = await _unitOfWork.ProductionBatches.GetAllActiveBatchesAsync(null, cancellationToken);

        var summaries = new List<FinishedGoodsBatchSummaryDto>();
        foreach (var batch in allBatches)
        {
            var orderedSteps = batch.BatchRoutingSteps
                .OrderBy(s => s.StageSequence)
                .ToList();

            var isAllStepsPassed = orderedSteps.Count > 0 &&
                                   orderedSteps.All(s => s.StepStatus == StepStatusEnum.PASSED);

            var isReadyForReceipt = batch.BatchStatus != BatchStatusEnum.DONE &&
                                    batch.BatchStatus != BatchStatusEnum.LOCKED &&
                                    batch.BatchStatus != BatchStatusEnum.HOLD_POINT_BLOCKED &&
                                    isAllStepsPassed;

            if (onlyReadyForReceipt && !isReadyForReceipt)
            {
                continue;
            }

            if (!onlyReadyForReceipt && !isReadyForReceipt && batch.BatchStatus != BatchStatusEnum.DONE)
            {
                continue;
            }

            var currentStep = orderedSteps.FirstOrDefault(s => s.StageSequence == batch.CurrentStageSeq)
                              ?? orderedSteps.LastOrDefault();
            var initialQty = orderedSteps.FirstOrDefault()?.TargetQty ?? batch.BatchQty;
            var customerOrder = batch.OrderItem?.CustomerOrder;

            summaries.Add(new FinishedGoodsBatchSummaryDto
            {
                BatchId = batch.BatchId,
                BatchCode = batch.BatchCode,
                BatchType = batch.BatchType.ToString(),
                BatchStatus = batch.BatchStatus.ToString(),
                OrderItemId = batch.OrderItemId,
                OrderId = batch.OrderItem?.OrderId ?? customerOrder?.OrderId ?? 0,
                PoNumber = customerOrder?.PoNumber ?? string.Empty,
                CustomerName = customerOrder?.Customer?.CompanyName ?? string.Empty,
                ProductCode = batch.OrderItem?.Product?.ProductSku ?? string.Empty,
                ProductName = batch.OrderItem?.Product?.ProductName ?? string.Empty,
                InitialQty = initialQty,
                CurrentQty = batch.BatchQty,
                OrderedQty = batch.OrderItem?.OrderedQty ?? 0,
                CurrentStageCode = currentStep?.StageName ?? $"STAGE_{batch.CurrentStageSeq}",
                IsAllRoutingStepsPassed = isAllStepsPassed,
                IsReadyForFinishedGoodsReceipt = isReadyForReceipt,
                EtdDate = customerOrder?.EtdDate ?? DateTime.MinValue,
                CreatedAt = batch.CreatedAt
            });
        }

        return summaries
            .OrderBy(s => s.EtdDate)
            .ThenBy(s => s.BatchCode)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<FinishedGoodsReceiptResultDto> CompleteBatchAndReceiveFinishedGoodsAsync(
        long batchId,
        CompleteFinishedBatchRequestDto request,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _unitOfWork.ProductionBatches.GetByIdAsync(batchId, cancellationToken);
        if (batch == null)
        {
            throw AppException.NotFound(
                $"Không tìm thấy lô sản xuất với mã định danh ID = {batchId}.");
        }

        if (batch.BatchStatus == BatchStatusEnum.DONE)
        {
            throw AppException.Conflict(
                $"Lô sản xuất '{batch.BatchCode}' đã được xác nhận hoàn tất và nhập kho thành phẩm trước đó.",
                MessageCodes.Msg08DuplicateConflict);
        }

        if (batch.BatchStatus == BatchStatusEnum.LOCKED)
        {
            throw new AppException(
                $"Lô sản xuất '{batch.BatchCode}' đang ở trạng thái khóa (LOCKED - chưa xuất kho nguyên vật liệu) nên không thể nhập kho thành phẩm.",
                400,
                MessageCodes.Msg13ValidationError);
        }

        if (batch.BatchStatus == BatchStatusEnum.HOLD_POINT_BLOCKED)
        {
            throw new AppException(
                $"Lô sản xuất '{batch.BatchCode}' đang bị chặn tại chốt kiểm soát Hold Point (HOLD_POINT_BLOCKED). Vui lòng xử lý lỗi QC hoặc chờ phê duyệt ghi đè trước khi hoàn tất lô.",
                400,
                MessageCodes.Msg13ValidationError);
        }

        var orderedSteps = batch.BatchRoutingSteps
            .OrderBy(s => s.StageSequence)
            .ToList();

        if (orderedSteps.Count == 0)
        {
            throw new AppException(
                $"Lô sản xuất '{batch.BatchCode}' chưa được khởi tạo danh sách bước công đoạn định tuyến (BatchRoutingSteps).",
                400,
                MessageCodes.Msg13ValidationError);
        }

        var incompleteStep = orderedSteps.FirstOrDefault(s => s.StepStatus != StepStatusEnum.PASSED);
        if (incompleteStep != null)
        {
            throw new AppException(
                $"Không thể nhập kho thành phẩm cho lô '{batch.BatchCode}' vì công đoạn '{incompleteStep.StageName}' (Thứ tự {incompleteStep.StageSequence}) đang ở trạng thái '{incompleteStep.StepStatus}' (yêu cầu đạt chuẩn QC PASSED).",
                400,
                MessageCodes.Msg13ValidationError);
        }

        var finalCompletedQty = request.ReceivedQty ?? batch.BatchQty;
        if (finalCompletedQty <= 0)
        {
            throw new AppException(
                "Số lượng thành phẩm thực nhận nhập kho phải lớn hơn 0.",
                400,
                MessageCodes.Msg13ValidationError);
        }

        if (finalCompletedQty > batch.BatchQty)
        {
            throw new AppException(
                $"Số lượng thành phẩm bàn giao ({finalCompletedQty}) không được vượt quá sản lượng hiện có đạt chuẩn của lô ({batch.BatchQty}).",
                400,
                MessageCodes.Msg13ValidationError);
        }

        var now = DateTime.UtcNow;
        var previousBatchStatus = batch.BatchStatus;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            batch.BatchQty = finalCompletedQty;
            batch.BatchStatus = BatchStatusEnum.DONE;

            var lastStep = orderedSteps.Last();
            batch.CurrentStageSeq = lastStep.StageSequence;
            lastStep.CompletedAt ??= now;

            await _unitOfWork.ProductionBatches.UpdateAsync(batch);

            // Ghi nhận Audit Log hoàn tất lô sản xuất và nhập kho thành phẩm
            await _unitOfWork.Boms.AddAuditLogAsync(new AuditLog
            {
                UserId = currentUserId > 0 ? currentUserId : null,
                ActionType = AuditActionConstants.FinishedGoodsBatchCompleted,
                EntityName = "ProductionBatch",
                EntityId = batch.BatchCode,
                BeforeState = JsonSerializer.Serialize(new
                {
                    BatchStatus = previousBatchStatus.ToString(),
                    BatchQty = batch.BatchQty
                }),
                AfterState = JsonSerializer.Serialize(new
                {
                    BatchStatus = batch.BatchStatus.ToString(),
                    CompletedQty = finalCompletedQty,
                    WarehouseLocation = request.WarehouseLocation,
                    HandoverNote = request.HandoverNote
                }),
                Reason = request.HandoverNote ?? $"Hoàn tất công đoạn đóng gói cuối cùng và nhập kho {finalCompletedQty} thành phẩm cho lô {batch.BatchCode}.",
                CreatedAt = now
            });

            var orderId = batch.OrderItem?.OrderId ?? batch.OrderItem?.CustomerOrder?.OrderId ?? 0;

            // Tải toàn bộ cấu trúc đơn hàng để tính toán và đồng bộ tiến độ OrderItem & SalesOrder
            var order = (orderId > 0 ? await _unitOfWork.OrderItems.GetOrderWithFullProgressGraphAsync(orderId, cancellationToken) : null)
                        ?? batch.OrderItem?.CustomerOrder;

            var targetOrderItem = order?.OrderItems.FirstOrDefault(oi => oi.OrderItemId == batch.OrderItemId)
                                  ?? batch.OrderItem;

            int orderedQty = targetOrderItem?.OrderedQty ?? 0;
            int totalCompletedForOrderItem = finalCompletedQty;
            bool isOrderItemDone = false;
            bool isSalesOrderDone = false;
            string orderItemStatusStr = targetOrderItem?.Status.ToString() ?? OrderItemStatusEnum.IN_PROD.ToString();
            string salesOrderStatusStr = order?.Status.ToString() ?? OrderStatusEnum.IN_PRODUCTION.ToString();

            if (targetOrderItem != null)
            {
                var itemBatches = targetOrderItem.ProductionBatches.ToList();
                if (!itemBatches.Any(b => b.BatchId == batch.BatchId))
                {
                    itemBatches.Add(batch);
                }
                else
                {
                    var trackedBatch = itemBatches.First(b => b.BatchId == batch.BatchId);
                    trackedBatch.BatchStatus = BatchStatusEnum.DONE;
                    trackedBatch.BatchQty = finalCompletedQty;
                }

                // Tính tổng sản lượng thành phẩm của OrderItem từ cả thẻ chính (MAIN_CARD) và các thẻ nhánh phụ 97-3 (REWORK_SUB_CARD / REPLENISH_LOT) đã hoàn tất (DONE)
                totalCompletedForOrderItem = itemBatches
                    .Where(b => b.BatchStatus == BatchStatusEnum.DONE &&
                                (b.BatchType == BatchTypeEnum.MAIN_CARD ||
                                 b.BatchType == BatchTypeEnum.REWORK_SUB_CARD ||
                                 b.BatchType == BatchTypeEnum.REPLENISH_LOT))
                    .Sum(b => b.BatchQty);

                var previousItemStatus = targetOrderItem.Status;
                if (orderedQty > 0 && totalCompletedForOrderItem >= orderedQty)
                {
                    targetOrderItem.Status = OrderItemStatusEnum.DONE;
                    isOrderItemDone = true;
                }
                else if (targetOrderItem.Status != OrderItemStatusEnum.DONE)
                {
                    targetOrderItem.Status = OrderItemStatusEnum.IN_PROD;
                }

                orderItemStatusStr = targetOrderItem.Status.ToString();
                await _unitOfWork.OrderItems.UpdateAsync(targetOrderItem);

                if (isOrderItemDone && previousItemStatus != OrderItemStatusEnum.DONE)
                {
                    await _unitOfWork.Boms.AddAuditLogAsync(new AuditLog
                    {
                        UserId = currentUserId > 0 ? currentUserId : null,
                        ActionType = AuditActionConstants.OrderItemProductionCompleted,
                        EntityName = "OrderItem",
                        EntityId = targetOrderItem.OrderItemId.ToString(),
                        BeforeState = JsonSerializer.Serialize(new { Status = previousItemStatus.ToString() }),
                        AfterState = JsonSerializer.Serialize(new
                        {
                            Status = targetOrderItem.Status.ToString(),
                            OrderedQty = orderedQty,
                            TotalCompletedQty = totalCompletedForOrderItem
                        }),
                        Reason = $"Dòng sản phẩm #{targetOrderItem.OrderItemId} đã hoàn thành đủ sản lượng đặt hàng ({totalCompletedForOrderItem}/{orderedQty}).",
                        CreatedAt = now
                    });
                }
            }

            if (order != null && order.OrderItems.Count > 0)
            {
                var previousOrderStatus = order.Status;
                if (order.OrderItems.All(oi => oi.Status == OrderItemStatusEnum.DONE))
                {
                    order.Status = OrderStatusEnum.COMPLETED;
                    isSalesOrderDone = true;
                }
                else if (order.Status == OrderStatusEnum.CONFIRMED || order.Status == OrderStatusEnum.IN_PLANNING)
                {
                    order.Status = OrderStatusEnum.IN_PRODUCTION;
                }

                salesOrderStatusStr = order.Status.ToString();
                await _unitOfWork.OrderItems.UpdateOrderAsync(order);

                if (isSalesOrderDone && previousOrderStatus != OrderStatusEnum.COMPLETED)
                {
                    await _unitOfWork.Boms.AddAuditLogAsync(new AuditLog
                    {
                        UserId = currentUserId > 0 ? currentUserId : null,
                        ActionType = AuditActionConstants.SalesOrderProductionCompleted,
                        EntityName = "CustomerOrder",
                        EntityId = order.PoNumber,
                        BeforeState = JsonSerializer.Serialize(new { OrderStatus = previousOrderStatus.ToString() }),
                        AfterState = JsonSerializer.Serialize(new { OrderStatus = order.Status.ToString() }),
                        Reason = $"Đơn hàng {order.PoNumber} đã hoàn tất 100% các dòng sản phẩm và sẵn sàng xuất xưởng.",
                        CreatedAt = now
                    });
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync();

            var remainingQty = Math.Max(0, orderedQty - totalCompletedForOrderItem);
            var completionPercent = orderedQty > 0
                ? Math.Min(100m, Math.Round((decimal)totalCompletedForOrderItem * 100m / orderedQty, 2))
                : 100m;

            return new FinishedGoodsReceiptResultDto
            {
                BatchId = batch.BatchId,
                BatchCode = batch.BatchCode,
                BatchType = batch.BatchType.ToString(),
                BatchStatus = batch.BatchStatus.ToString(),
                CompletedBatchQty = finalCompletedQty,
                OrderItemId = batch.OrderItemId,
                OrderId = order?.OrderId ?? orderId,
                PoNumber = order?.PoNumber ?? batch.OrderItem?.CustomerOrder?.PoNumber ?? string.Empty,
                ProductCode = targetOrderItem?.Product?.ProductSku ?? batch.OrderItem?.Product?.ProductSku ?? string.Empty,
                ProductName = targetOrderItem?.Product?.ProductName ?? batch.OrderItem?.Product?.ProductName ?? string.Empty,
                OrderedQty = orderedQty,
                TotalCompletedQtyForOrderItem = totalCompletedForOrderItem,
                RemainingQtyForOrderItem = remainingQty,
                OrderItemCompletionPercent = completionPercent,
                OrderItemStatus = orderItemStatusStr,
                SalesOrderStatus = salesOrderStatusStr,
                IsOrderItemFullyCompleted = isOrderItemDone,
                IsSalesOrderFullyCompleted = isSalesOrderDone,
                WarehouseLocation = request.WarehouseLocation,
                HandoverNote = request.HandoverNote,
                CompletedAt = now,
                Message = isSalesOrderDone
                    ? $"Đã nhập kho thành phẩm lô '{batch.BatchCode}' ({finalCompletedQty} sản phẩm). Đơn hàng '{order?.PoNumber}' đã hoàn tất 100%!"
                    : $"Đã nhập kho thành phẩm lô '{batch.BatchCode}' ({finalCompletedQty} sản phẩm). Tiến độ dòng sản phẩm đạt {completionPercent}% ({totalCompletedForOrderItem}/{orderedQty})."
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }
}
