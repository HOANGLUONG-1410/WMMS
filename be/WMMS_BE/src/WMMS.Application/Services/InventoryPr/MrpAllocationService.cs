using System.Text.Json;
using WMMS.Application.DTOs.InventoryPr;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;
using WMMS.Domain.Entities.CatalogBom;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Entities.InventoryPr;
using WMMS.Domain.Entities.KanbanQc;
using WMMS.Domain.Entities.Orders;
using WMMS.Domain.Entities.WarehousePo;
using WMMS.Domain.Enums;
using WMMS.Domain.Exceptions;

namespace WMMS.Application.Services.InventoryPr;

/// <summary>
/// Thực thi Bộ máy Tính toán Nhu cầu Nguyên vật liệu (MRP) &amp; Phân bổ Tồn kho Tự động (SC-14).
/// Đảm nhiệm:
/// 1. Tính toán Gross Demand có cộng hệ số hao hụt tự nhiên (BR-BOM-02 / BR-002).
/// 2. Loại trừ tồn kho đã giữ chỗ cho đơn hàng khác (BR-INV-01), chỉ đối chiếu trên AvailableStock.
/// 3. Tự động khóa mềm (Soft-Allocate) tồn kho khả dụng vào bảng <c>material_allocations</c> (BR-INV-02).
/// 4. Tự động chuyển trạng thái OrderItem &amp; BOM sang READY_PROD / READY_FOR_PRODUCTION (nếu đủ 100%)
///    hoặc WAITING_MAT / WAITING_FOR_MATERIALS (nếu thiếu vật tư).
/// 5. Tự động gom phần thiếu hụt ròng (Net Shortage) vào Phiếu PR Tổng hợp trong ngày (<c>consolidated_prs</c>, BR-PUR-01).
/// 6. Tự động phân bổ lại vật tư mới nhập kho cho các đơn hàng đang chờ theo thứ tự ưu tiên ETD gần nhất (FIFO by ETD).
/// </summary>
public class MrpAllocationService : IMrpAllocationService
{
    private readonly IOrderItemRepository _orderItemRepository;
    private readonly IMaterialStockRepository _materialStockRepository;
    private readonly IMaterialAllocationRepository _materialAllocationRepository;
    private readonly IPurchaseRequestRepository _purchaseRequestRepository;
    private readonly IBomRepository _bomRepository;
    private readonly ISystemConfigRepository _systemConfigRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MrpAllocationService(
        IOrderItemRepository orderItemRepository,
        IMaterialStockRepository materialStockRepository,
        IMaterialAllocationRepository materialAllocationRepository,
        IPurchaseRequestRepository purchaseRequestRepository,
        IBomRepository bomRepository,
        ISystemConfigRepository systemConfigRepository,
        IUnitOfWork unitOfWork)
    {
        _orderItemRepository = orderItemRepository;
        _materialStockRepository = materialStockRepository;
        _materialAllocationRepository = materialAllocationRepository;
        _purchaseRequestRepository = purchaseRequestRepository;
        _bomRepository = bomRepository;
        _systemConfigRepository = systemConfigRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<MrpAnalysisResultDto> AnalyzeOrderMrpAsync(
        long orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderItemRepository.GetOrderByIdAsync(orderId, cancellationToken)
            ?? throw AppException.NotFound("Không tìm thấy đơn hàng được yêu cầu.");

        var orderItems = await _orderItemRepository.GetByOrderIdWithFullMrpGraphAsync(orderId, cancellationToken);
        var warningDays = (int)await _systemConfigRepository.GetDecimalConfigAsync(SystemConfigKeys.EtdWarningDays, 7m);

        var result = BuildMrpAnalysisResult(
            scope: "ORDER",
            orderItems: orderItems,
            order: order,
            space: order.OrderSpace,
            etdWarningDays: warningDays);

        return result;
    }

    /// <inheritdoc />
    public async Task<MrpAnalysisResultDto> AnalyzeOrderSpaceMrpAsync(
        long spaceId,
        CancellationToken cancellationToken = default)
    {
        var space = await _orderItemRepository.GetOrderSpaceByIdAsync(spaceId, cancellationToken)
            ?? throw AppException.NotFound("Không tìm thấy không gian đơn hàng được yêu cầu.");

        var orderItems = await _orderItemRepository.GetBySpaceIdWithFullMrpGraphAsync(spaceId, cancellationToken);
        var warningDays = (int)await _systemConfigRepository.GetDecimalConfigAsync(SystemConfigKeys.EtdWarningDays, 7m);

        var result = BuildMrpAnalysisResult(
            scope: "ORDER_SPACE",
            orderItems: orderItems,
            order: space.CustomerOrder,
            space: space,
            etdWarningDays: warningDays);

        return result;
    }

    /// <inheritdoc />
    public async Task<MrpAnalysisResultDto> AnalyzeFactoryMrpAsync(
        CancellationToken cancellationToken = default)
    {
        var orderItems = await _orderItemRepository.GetAllActiveOrderItemsWithMrpGraphAsync(cancellationToken);
        var warningDays = (int)await _systemConfigRepository.GetDecimalConfigAsync(SystemConfigKeys.EtdWarningDays, 7m);

        return BuildMrpAnalysisResult(
            scope: "FACTORY_ALL",
            orderItems: orderItems,
            order: null,
            space: null,
            etdWarningDays: warningDays);
    }

    /// <inheritdoc />
    public async Task<AutoAllocationResultDto> ExecuteAutoAllocationForOrderAsync(
        long orderId,
        AutoAllocationRequestDto? request,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderItemRepository.GetOrderByIdAsync(orderId, cancellationToken)
            ?? throw AppException.NotFound("Không tìm thấy đơn hàng được yêu cầu.");

        var orderItems = await _orderItemRepository.GetByOrderIdWithFullMrpGraphAsync(orderId, cancellationToken);
        if (orderItems.Count == 0)
        {
            throw new AppException($"Đơn hàng {order.PoNumber} chưa có hạng mục sản phẩm nào để phân bổ tồn kho.", 400, MessageCodes.Msg13ValidationError);
        }

        return await ExecuteCoreAutoAllocationAsync(
            orderItems: orderItems,
            order: order,
            space: order.OrderSpace,
            options: request ?? new AutoAllocationRequestDto(),
            currentUserId: currentUserId,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AutoAllocationResultDto> ExecuteAutoAllocationForOrderSpaceAsync(
        long spaceId,
        AutoAllocationRequestDto? request,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var space = await _orderItemRepository.GetOrderSpaceByIdAsync(spaceId, cancellationToken)
            ?? throw AppException.NotFound("Không tìm thấy không gian đơn hàng được yêu cầu.");

        var orderItems = await _orderItemRepository.GetBySpaceIdWithFullMrpGraphAsync(spaceId, cancellationToken);
        if (orderItems.Count == 0)
        {
            throw new AppException($"Không gian đơn hàng {space.SpaceKey} chưa có hạng mục sản phẩm nào để phân bổ tồn kho.", 400, MessageCodes.Msg13ValidationError);
        }

        return await ExecuteCoreAutoAllocationAsync(
            orderItems: orderItems,
            order: space.CustomerOrder,
            space: space,
            options: request ?? new AutoAllocationRequestDto(),
            currentUserId: currentUserId,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AutoAllocationResultDto> ExecuteAutoAllocationForOrderItemAsync(
        long orderItemId,
        AutoAllocationRequestDto? request,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var orderItem = await _orderItemRepository.GetByIdWithFullMrpGraphAsync(orderItemId, cancellationToken)
            ?? throw AppException.NotFound("Không tìm thấy hạng mục sản phẩm trong đơn hàng.");

        return await ExecuteCoreAutoAllocationAsync(
            orderItems: new List<OrderItem> { orderItem },
            order: orderItem.CustomerOrder,
            space: orderItem.CustomerOrder.OrderSpace,
            options: request ?? new AutoAllocationRequestDto(),
            currentUserId: currentUserId,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ReallocateByEtdResultDto> ReallocateAvailableStockByEtdAsync(
        ReallocateByEtdRequestDto request,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var waitingItems = await _orderItemRepository.GetWaitingMaterialItemsOrderedByEtdAsync(
            request.MaterialIds,
            cancellationToken);

        var allMaterialIds = waitingItems
            .Where(oi => oi.BomHeader != null)
            .SelectMany(oi => oi.BomHeader!.BomDetails.Select(bd => bd.MaterialId))
            .Distinct()
            .ToList();

        if (request.MaterialIds != null && request.MaterialIds.Count > 0)
        {
            var filterSet = request.MaterialIds.ToHashSet();
            allMaterialIds = allMaterialIds.Where(id => filterSet.Contains(id)).ToList();
        }

        var stockMap = await _materialStockRepository.GetByMaterialIdsAsync(allMaterialIds, cancellationToken);

        var reallocatedMaterialIds = new HashSet<long>();
        var unlockedOrderItemIds = new List<long>();
        var updatedAllocationDtos = new List<MaterialOrderAllocationDto>();
        int allocationsIncreasedCount = 0;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Duyệt qua các OrderItem đang thiếu vật tư theo đúng thứ tự ETD sớm nhất (FIFO by ETD)
            foreach (var item in waitingItems)
            {
                if (item.BomHeader == null || item.BomHeader.BomDetails.Count == 0)
                {
                    continue;
                }

                var groupedBomLines = item.BomHeader.BomDetails
                    .GroupBy(d => d.MaterialId)
                    .Select(g => new
                    {
                        MaterialId = g.Key,
                        Material = g.First().Material,
                        TotalGrossDemand = Math.Round(g.Sum(x => ResolveGrossDemand(x, item.OrderedQty, item.BomHeader.ScrapRatePercent)), 4)
                    })
                    .ToList();

                foreach (var detail in groupedBomLines)
                {
                    if (request.MaterialIds != null && request.MaterialIds.Count > 0 && !request.MaterialIds.Contains(detail.MaterialId))
                    {
                        continue;
                    }

                    if (!stockMap.TryGetValue(detail.MaterialId, out var stock))
                    {
                        stock = await _materialStockRepository.GetOrCreateByMaterialIdAsync(detail.MaterialId, cancellationToken);
                        stockMap[detail.MaterialId] = stock;
                    }

                    var existingAlloc = item.MaterialAllocations
                        .FirstOrDefault(ma => ma.MaterialId == detail.MaterialId && ma.AllocationStatus != AllocationStatusEnum.RELEASED);

                    // Bảo toàn phần RequiredQty đã cộng thêm từ cấp bù phế phẩm (Scrap Replenishment) thay vì ghi đè bằng BOM gốc
                    var requiredGross = existingAlloc != null
                        ? Math.Max(detail.TotalGrossDemand, existingAlloc.RequiredQty)
                        : detail.TotalGrossDemand;

                    var currentReservedAndIssued = existingAlloc != null
                        ? existingAlloc.ReservedQty + existingAlloc.IssuedQty
                        : 0m;

                    var shortage = Math.Max(0m, Math.Round(requiredGross - currentReservedAndIssued, 4));
                    if (shortage <= 0m || stock.AvailableQty <= 0m)
                    {
                        continue;
                    }

                    var newlyAllocated = stock.AllocateUpToAvailable(shortage);
                    if (newlyAllocated <= 0m)
                    {
                        continue;
                    }

                    await _materialStockRepository.UpdateAsync(stock);
                    reallocatedMaterialIds.Add(detail.MaterialId);
                    allocationsIncreasedCount++;

                    if (existingAlloc == null)
                    {
                        existingAlloc = new MaterialAllocation
                        {
                            OrderItemId = item.OrderItemId,
                            OrderItem = item,
                            MaterialId = detail.MaterialId,
                            Material = detail.Material,
                            RequiredQty = requiredGross,
                            ReservedQty = newlyAllocated,
                            IssuedQty = 0m,
                            AllocationStatus = AllocationStatusEnum.RESERVED,
                            AllocatedAt = now
                        };
                        item.MaterialAllocations.Add(existingAlloc);
                        await _materialAllocationRepository.AddAsync(existingAlloc, cancellationToken);
                    }
                    else
                    {
                        existingAlloc.RequiredQty = requiredGross;
                        existingAlloc.ReservedQty = Math.Round(existingAlloc.ReservedQty + newlyAllocated, 4);
                        existingAlloc.AllocatedAt = now;
                        await _materialAllocationRepository.UpdateAsync(existingAlloc);
                    }

                    updatedAllocationDtos.Add(MapAllocationDto(existingAlloc, item));
                }

                // Kiểm tra xem sau khi phân bổ bù theo ETD, OrderItem này đã đủ 100% vật tư của mọi dòng BOM chưa
                bool isFullyFulfilled = IsOrderItemFullyAllocated(item);
                if (isFullyFulfilled && item.Status != OrderItemStatusEnum.READY_PROD && item.Status != OrderItemStatusEnum.IN_PROD && item.Status != OrderItemStatusEnum.DONE)
                {
                    item.Status = OrderItemStatusEnum.READY_PROD;
                    await _orderItemRepository.UpdateAsync(item);

                    if (item.BomHeader.BomStatus != BomStatusEnum.READY_FOR_PRODUCTION)
                    {
                        item.BomHeader.BomStatus = BomStatusEnum.READY_FOR_PRODUCTION;
                        item.BomHeader.UpdatedAt = now;
                        await _bomRepository.UpdateAsync(item.BomHeader);
                    }

                    unlockedOrderItemIds.Add(item.OrderItemId);
                }
            }

            if (allocationsIncreasedCount > 0)
            {
                await _bomRepository.AddAuditLogAsync(new AuditLog
                {
                    UserId = currentUserId > 0 ? currentUserId : 1,
                    ActionType = AuditActionConstants.InventoryReallocatedByEtd,
                    EntityName = "MaterialAllocation",
                    EntityId = request.ReferenceCode ?? "FIFO-ETD-REALLOC",
                    BeforeState = null,
                    AfterState = JsonSerializer.Serialize(new
                    {
                        ReferenceCode = request.ReferenceCode,
                        MaterialsReallocatedCount = reallocatedMaterialIds.Count,
                        AllocationsIncreasedCount = allocationsIncreasedCount,
                        UnlockedOrderItemIds = unlockedOrderItemIds
                    }),
                    Reason = $"Tự động phân bổ lại tồn kho khả dụng theo thứ tự ưu tiên ngày giao hàng gần nhất. Đã mở khóa {unlockedOrderItemIds.Count} hạng mục đơn hàng sang trạng thái sẵn sàng sản xuất.",
                    CreatedAt = now
                });
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync();

            return new ReallocateByEtdResultDto
            {
                ExecutedAt = now,
                ReferenceCode = request.ReferenceCode,
                MaterialsReallocatedCount = reallocatedMaterialIds.Count,
                AllocationsIncreasedCount = allocationsIncreasedCount,
                OrderItemsUnlockedToReadyCount = unlockedOrderItemIds.Count,
                UnlockedOrderItemIds = unlockedOrderItemIds,
                UpdatedAllocations = updatedAllocationDtos
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<MaterialOrderAllocationDto>> ReleaseOrderAllocationsAsync(
        long orderId,
        string reason,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var allocations = await _materialAllocationRepository.GetByOrderIdAsync(orderId, cancellationToken);
        var activeReserved = allocations
            .Where(a => a.AllocationStatus == AllocationStatusEnum.RESERVED && a.ReservedQty > 0m)
            .ToList();

        if (activeReserved.Count == 0)
        {
            return new List<MaterialOrderAllocationDto>();
        }

        if (activeReserved.Any(a => a.OrderItem != null && a.OrderItem.Status is OrderItemStatusEnum.IN_PROD or OrderItemStatusEnum.DONE))
        {
            throw AppException.Conflict(
                "Không thể giải phóng vật tư giữ chỗ vì đơn hàng đã vào sản xuất hoặc đã hoàn thành.",
                MessageCodes.Msg08DuplicateConflict);
        }

        var materialIds = activeReserved.Select(a => a.MaterialId).Distinct().ToList();
        var stockMap = await _materialStockRepository.GetByMaterialIdsAsync(materialIds, cancellationToken);
        var now = DateTime.UtcNow;
        var releasedDtos = new List<MaterialOrderAllocationDto>();

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var affectedOrderItems = new HashSet<OrderItem>();
            foreach (var alloc in activeReserved)
            {
                var qtyToRelease = alloc.ReservedQty;
                if (stockMap.TryGetValue(alloc.MaterialId, out var stock))
                {
                    stock.ReleaseAllocation(qtyToRelease);
                    await _materialStockRepository.UpdateAsync(stock);
                }

                alloc.ReservedQty = 0m;
                // Nếu dòng phân bổ đã xuất kho một phần xuống xưởng (IssuedQty > 0), giữ trạng thái ISSUED_TO_SHOP/RESERVED để không làm mất dấu lượng đã xuất
                if (alloc.IssuedQty > 0m)
                {
                    alloc.AllocationStatus = alloc.IssuedQty >= alloc.RequiredQty
                        ? AllocationStatusEnum.ISSUED_TO_SHOP
                        : AllocationStatusEnum.RESERVED;
                }
                else
                {
                    alloc.AllocationStatus = AllocationStatusEnum.RELEASED;
                }
                await _materialAllocationRepository.UpdateAsync(alloc);

                if (alloc.OrderItem != null)
                {
                    affectedOrderItems.Add(alloc.OrderItem);
                }

                releasedDtos.Add(MapAllocationDto(alloc, alloc.OrderItem!));
            }

            // Hạ trạng thái các OrderItem / BOM vừa bị giải phóng giữ chỗ khỏi READY_PROD / READY_FOR_PRODUCTION về chờ vật tư
            foreach (var item in affectedOrderItems)
            {
                if (item.Status == OrderItemStatusEnum.READY_PROD)
                {
                    item.Status = OrderItemStatusEnum.WAITING_MAT;
                    await _orderItemRepository.UpdateAsync(item);
                }

                if (item.BomHeader != null && item.BomHeader.BomStatus == BomStatusEnum.READY_FOR_PRODUCTION)
                {
                    item.BomHeader.BomStatus = BomStatusEnum.WAITING_FOR_MATERIALS;
                    item.BomHeader.UpdatedAt = now;
                    await _bomRepository.UpdateAsync(item.BomHeader);
                }
            }

            await _bomRepository.AddAuditLogAsync(new AuditLog
            {
                UserId = currentUserId > 0 ? currentUserId : 1,
                ActionType = AuditActionConstants.InventoryAllocationReleased,
                EntityName = "CustomerOrder",
                EntityId = orderId.ToString(),
                BeforeState = null,
                AfterState = JsonSerializer.Serialize(new
                {
                    OrderId = orderId,
                    ReleasedLinesCount = releasedDtos.Count
                }),
                Reason = string.IsNullOrWhiteSpace(reason)
                    ? $"Giải phóng giữ chỗ vật tư cho đơn hàng #{orderId}."
                    : reason,
                CreatedAt = now
            });

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync();

            return releasedDtos;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IssueOrderAllocationsResultDto> IssueOrderAllocationsAsync(
        long orderId,
        string? reason,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderItemRepository.GetOrderByIdAsync(orderId, cancellationToken)
            ?? throw AppException.NotFound("Không tìm thấy đơn hàng được yêu cầu.");

        var allocations = await _materialAllocationRepository.GetByOrderIdAsync(orderId, cancellationToken);
        var activeReserved = allocations
            .Where(a => a.AllocationStatus == AllocationStatusEnum.RESERVED && a.ReservedQty > 0m)
            .ToList();

        if (activeReserved.Count == 0)
        {
            throw new AppException(
                "Đơn hàng này không có vật tư nào đang ở trạng thái giữ chỗ để thực hiện xuất kho.",
                400,
                MessageCodes.Msg13ValidationError);
        }

        var materialIds = activeReserved.Select(a => a.MaterialId).Distinct().ToList();
        var stockMap = await _materialStockRepository.GetByMaterialIdsAsync(materialIds, cancellationToken);
        var now = DateTime.UtcNow;
        var issuedDtos = new List<MaterialOrderAllocationDto>();
        var transactions = new List<InventoryTransaction>();
        decimal totalIssuedQty = 0m;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            foreach (var alloc in activeReserved)
            {
                var qtyToIssue = alloc.ReservedQty;

                if (stockMap.TryGetValue(alloc.MaterialId, out var stock))
                {
                    stock.IssueAllocatedStock(qtyToIssue);
                    await _materialStockRepository.UpdateAsync(stock);
                }

                alloc.IssuedQty = Math.Round(alloc.IssuedQty + qtyToIssue, 4);
                alloc.ReservedQty = 0m;
                // Chỉ đánh dấu ISSUED_TO_SHOP khi đã xuất đủ RequiredQty; nếu xuất dở dang giữ RESERVED để GRN tiếp tục phân bổ phần thiếu
                alloc.AllocationStatus = alloc.IssuedQty >= alloc.RequiredQty
                    ? AllocationStatusEnum.ISSUED_TO_SHOP
                    : AllocationStatusEnum.RESERVED;
                await _materialAllocationRepository.UpdateAsync(alloc);

                transactions.Add(new InventoryTransaction
                {
                    MaterialId = alloc.MaterialId,
                    TxType = InventoryTransactionTypeEnum.OUT_ISSUE,
                    Quantity = qtyToIssue,
                    ReferenceCode = order.PoNumber ?? $"ORDER-{orderId}",
                    CreatedAt = now
                });

                totalIssuedQty += qtyToIssue;
                issuedDtos.Add(MapAllocationDto(alloc, alloc.OrderItem));
            }

            if (transactions.Count > 0)
            {
                await _materialStockRepository.AddTransactionsRangeAsync(transactions, cancellationToken);
            }

            // Chỉ mở khóa lô LOCKED và chốt chặn Hold Point cho các OrderItem đã xuất kho ĐỦ 100% vật tư (UC-59, BR-PRD-01)
            var fullyIssuedOrderItemIds = allocations
                .Where(a => a.AllocationStatus != AllocationStatusEnum.RELEASED)
                .GroupBy(a => a.OrderItemId)
                .Where(g => g.All(a => a.AllocationStatus == AllocationStatusEnum.ISSUED_TO_SHOP && a.IssuedQty >= a.RequiredQty))
                .Select(g => g.Key)
                .ToHashSet();

            var batches = await _unitOfWork.ProductionBatches.GetByOrderIdAsync(orderId, cancellationToken);
            int unlockedBatchesCount = 0;
            foreach (var batch in batches)
            {
                if (batch.BatchStatus == BatchStatusEnum.DONE || !fullyIssuedOrderItemIds.Contains(batch.OrderItemId))
                {
                    continue;
                }

                batch.GoodsIssuedUnlocked = true;
                if (batch.BatchStatus == BatchStatusEnum.LOCKED)
                {
                    batch.UnlockForProduction();
                    var currentStep = batch.BatchRoutingSteps.FirstOrDefault(s => s.StageSequence == batch.CurrentStageSeq);
                    if (currentStep != null && (currentStep.StepStatus == StepStatusEnum.WAITING || currentStep.StepStatus == StepStatusEnum.BLOCKED))
                    {
                        currentStep.StepStatus = StepStatusEnum.IN_PROGRESS;
                        await _unitOfWork.ProductionBatches.UpdateStepAsync(currentStep);
                    }
                }
                else if (batch.BatchStatus == BatchStatusEnum.HOLD_POINT_BLOCKED)
                {
                    batch.BatchStatus = BatchStatusEnum.IN_PROGRESS;
                    var blockedSteps = batch.BatchRoutingSteps
                        .Where(s => s.StepStatus == StepStatusEnum.BLOCKED)
                        .OrderBy(s => s.StageSequence)
                        .ToList();

                    var firstBlockedStep = blockedSteps.FirstOrDefault();
                    if (firstBlockedStep != null)
                    {
                        batch.CurrentStageSeq = firstBlockedStep.StageSequence;
                    }

                    foreach (var blockedStep in blockedSteps)
                    {
                        blockedStep.StepStatus = blockedStep.StageSequence == batch.CurrentStageSeq
                            ? StepStatusEnum.IN_PROGRESS
                            : StepStatusEnum.WAITING;
                        await _unitOfWork.ProductionBatches.UpdateStepAsync(blockedStep);
                    }
                }
                batch.UpdatedAt = now;
                await _unitOfWork.ProductionBatches.UpdateAsync(batch);
                unlockedBatchesCount++;
            }

            await _bomRepository.AddAuditLogAsync(new AuditLog
            {
                UserId = currentUserId > 0 ? currentUserId : 1,
                ActionType = AuditActionConstants.InventoryAllocationIssued,
                EntityName = "CustomerOrder",
                EntityId = orderId.ToString(),
                BeforeState = $"ReservedAllocationsCount_{activeReserved.Count}",
                AfterState = JsonSerializer.Serialize(new
                {
                    OrderId = orderId,
                    PoNumber = order.PoNumber,
                    IssuedAllocationsCount = issuedDtos.Count,
                    TotalQuantityIssued = totalIssuedQty,
                    BatchesUnlockedCount = unlockedBatchesCount
                }),
                Reason = string.IsNullOrWhiteSpace(reason)
                    ? $"Xác nhận xuất kho cấp phát {totalIssuedQty} đơn vị vật tư cho đơn hàng {order.PoNumber} bàn giao xuống xưởng."
                    : reason,
                CreatedAt = now
            });

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync();

            return new IssueOrderAllocationsResultDto
            {
                OrderId = orderId,
                PoNumber = order.PoNumber ?? string.Empty,
                IssuedAt = now,
                IssuedAllocationsCount = issuedDtos.Count,
                TotalQuantityIssued = totalIssuedQty,
                Message = $"Xác nhận xuất kho cấp phát vật tư cho đơn hàng {order.PoNumber} thành công ({issuedDtos.Count} dòng vật tư, tổng {totalIssuedQty}).",
                Allocations = issuedDtos
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<MaterialOrderAllocationDto>> GetAllocationsByOrderIdAsync(
        long orderId,
        CancellationToken cancellationToken = default)
    {
        var allocations = await _materialAllocationRepository.GetByOrderIdAsync(orderId, cancellationToken);
        return allocations.Select(a => MapAllocationDto(a, a.OrderItem)).ToList();
    }

    /// <inheritdoc />
    public async Task<List<MaterialOrderAllocationDto>> GetAllocationsBySpaceIdAsync(
        long spaceId,
        CancellationToken cancellationToken = default)
    {
        var allocations = await _materialAllocationRepository.GetBySpaceIdAsync(spaceId, cancellationToken);
        return allocations.Select(a => MapAllocationDto(a, a.OrderItem)).ToList();
    }

    /// <inheritdoc />
    public async Task<ScrapReplenishmentResultDto> RequestScrapReplenishmentAsync(
        ScrapReplenishmentRequestDto request,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (request.ScrappedQty <= 0)
        {
            throw new DomainException("Số lượng sản phẩm phế phẩm cần cấp bù phải lớn hơn 0.", "INVALID_SCRAPPED_QTY");
        }

        var orderItem = await _orderItemRepository.GetByIdWithFullMrpGraphAsync(request.OrderItemId, cancellationToken);
        if (orderItem == null)
        {
            throw new NotFoundException("Không tìm thấy sản phẩm tương ứng trong đơn hàng.");
        }

        var bom = orderItem.BomHeader;
        if (bom == null)
        {
            throw new DomainException($"Sản phẩm '{orderItem.Product?.ProductName}' chưa được thiết lập định mức BOM.", "BOM_NOT_CONFIGURED");
        }

        var customerOrder = orderItem.CustomerOrder;
        var poNumber = customerOrder?.PoNumber ?? $"PO-{orderItem.OrderId}";
        var now = DateTime.UtcNow;
        var today = now.Date;

        // Chặn cấp bù vượt sản lượng còn thiếu thực tế & chặn gửi trùng yêu cầu cấp bù chưa phát hành lô (chống nhân đôi giữ chỗ / PR).
        // Cùng công thức "sản lượng còn thiếu" với IssueReplenishLotAsync để hai API luôn khớp nhau.
        var existingOrderBatches = await _unitOfWork.ProductionBatches.GetByOrderItemIdAsync(orderItem.OrderItemId, cancellationToken);
        var activeAndDoneQty = existingOrderBatches.Where(b => b.BatchQty > 0).Sum(b => b.BatchQty);

        var absorbLogs = await _bomRepository.GetAuditLogsAsync("OrderItem", new[] { orderItem.OrderItemId.ToString() }, "SCRAP_REPLENISHMENT_ABSORBED", cancellationToken);
        int absorbedQty = 0;
        foreach (var log in absorbLogs)
        {
            if (!string.IsNullOrEmpty(log.AfterState))
            {
                try
                {
                    using var doc = JsonDocument.Parse(log.AfterState);
                    if (doc.RootElement.TryGetProperty("AbsorbedShortfallQty", out var prop))
                    {
                        absorbedQty += prop.GetInt32();
                    }
                }
                catch { }
            }
        }

        var missingQty = Math.Max(0, orderItem.OrderedQty - activeAndDoneQty - absorbedQty);
        if (missingQty <= 0)
        {
            throw new DomainException(
                $"Hạng mục '{orderItem.Product?.ProductName}' đã đủ sản lượng hoặc đã được Chủ xưởng chấp nhận thiếu hụt ({activeAndDoneQty + absorbedQty}/{orderItem.OrderedQty} SP), không có phế phẩm cần cấp bù.",
                "SCRAP_REPLENISH_NOT_REQUIRED");
        }

        var issuedReplenishLotsQty = existingOrderBatches
            .Where(b => b.BatchType == BatchTypeEnum.REPLENISH_LOT)
            .Sum(b => b.BatchRoutingSteps.OrderBy(s => s.StageSequence).FirstOrDefault()?.TargetQty ?? b.BatchQty);
        decimal cumulativeRequestedUnits = 0m;
        foreach (var g in bom.BomDetails.GroupBy(d => d.MaterialId))
        {
            var perUnitGross = g.Sum(d => d.NetQtyPerUnit) * (1m + bom.ScrapRatePercent / 100m);
            if (perUnitGross <= 0m) continue;
            var alloc = orderItem.MaterialAllocations
                .FirstOrDefault(ma => ma.MaterialId == g.Key && ma.AllocationStatus != AllocationStatusEnum.RELEASED);
            if (alloc == null) continue;
            var baseGross = g.Sum(d => CalculateDemandForQuantity(d, orderItem.OrderedQty, bom.ScrapRatePercent));
            var extraRequired = Math.Max(0m, alloc.RequiredQty - baseGross);
            cumulativeRequestedUnits = Math.Max(cumulativeRequestedUnits, extraRequired / perUnitGross);
        }
        var pendingRequestedQty = Math.Max(0m, Math.Round(cumulativeRequestedUnits, MidpointRounding.AwayFromZero) - issuedReplenishLotsQty);
        var unrequestedMissingQty = Math.Max(0, missingQty - (int)pendingRequestedQty);
        if (request.ScrappedQty > unrequestedMissingQty)
        {
            throw new DomainException(
                $"Số lượng cấp bù yêu cầu ({request.ScrappedQty} SP) vượt quá sản lượng phế phẩm chưa được cấp bù ({unrequestedMissingQty} SP). Đã có {pendingRequestedQty} SP đang giữ chỗ/chờ mua thêm.",
                "SCRAP_QTY_EXCEEDED");
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var groupedBomDetails = bom.BomDetails
                .GroupBy(d => d.MaterialId)
                .Select(g => new
                {
                    MaterialId = g.Key,
                    Material = g.First().Material,
                    GrossRequired = Math.Round(g.Sum(x => CalculateDemandForQuantity(x, request.ScrappedQty, bom.ScrapRatePercent)), 4)
                })
                .ToList();
            var detailsDtoList = new List<ScrapMaterialAllocationDetailDto>();
            decimal totalAllocated = 0m;
            decimal totalShortage = 0m;
            var shortageLinesForPr = new List<(long MaterialId, decimal ShortageQty)>();

            foreach (var detail in groupedBomDetails)
            {
                var grossRequired = detail.GrossRequired;
                var stock = await _materialStockRepository.GetOrCreateByMaterialIdAsync(detail.MaterialId, cancellationToken);

                decimal available = Math.Max(0m, stock.AvailableQty);
                decimal allocated = Math.Min(available, grossRequired);
                decimal shortage = Math.Max(0m, grossRequired - allocated);

                if (allocated > 0m)
                {
                    stock.AllocatedQty += allocated;
                    stock.AvailableQty = Math.Max(0m, stock.PhysicalQty - stock.AllocatedQty);
                    stock.UpdatedAt = now;
                    await _materialStockRepository.UpdateAsync(stock);
                }

                var existingAlloc = orderItem.MaterialAllocations
                    .FirstOrDefault(ma => ma.MaterialId == detail.MaterialId && ma.AllocationStatus != AllocationStatusEnum.RELEASED);

                AllocationStatusEnum effectiveStatus;
                if (existingAlloc != null)
                {
                    existingAlloc.RequiredQty = Math.Round(existingAlloc.RequiredQty + grossRequired, 4);
                    existingAlloc.ReservedQty = Math.Round(existingAlloc.ReservedQty + allocated, 4);
                    existingAlloc.AllocationStatus = AllocationStatusEnum.RESERVED;
                    existingAlloc.AllocatedAt = now;
                    await _materialAllocationRepository.UpdateAsync(existingAlloc);
                    effectiveStatus = existingAlloc.AllocationStatus;
                }
                else
                {
                    var allocation = new MaterialAllocation
                    {
                        OrderItemId = orderItem.OrderItemId,
                        MaterialId = detail.MaterialId,
                        RequiredQty = grossRequired,
                        ReservedQty = allocated,
                        AllocationStatus = AllocationStatusEnum.RESERVED,
                        AllocatedAt = now
                    };
                    orderItem.MaterialAllocations.Add(allocation);
                    await _materialAllocationRepository.AddAsync(allocation, cancellationToken);
                    effectiveStatus = allocation.AllocationStatus;
                }

                totalAllocated += allocated;
                totalShortage += shortage;

                if (shortage > 0m)
                {
                    shortageLinesForPr.Add((detail.MaterialId, shortage));
                }

                detailsDtoList.Add(new ScrapMaterialAllocationDetailDto
                {
                    MaterialId = detail.MaterialId,
                    MaterialCode = detail.Material?.MaterialCode ?? string.Empty,
                    MaterialName = detail.Material?.MaterialName ?? string.Empty,
                    MaterialGroup = detail.Material?.MaterialGroup.ToString() ?? string.Empty,
                    UnitOfMeasure = detail.Material?.UnitOfMeasure ?? string.Empty,
                    GrossRequiredQty = grossRequired,
                    AllocatedFromStockQty = allocated,
                    ShortageQty = shortage,
                    AllocationStatus = effectiveStatus.ToString()
                });
            }

            // Tự động gom thiếu hụt vào PR nếu có thiếu kho và autoCreatePrIfShortage = true
            long? createdPrId = null;
            string? createdPrCode = null;
            if (totalShortage > 0m && request.AutoCreatePrIfShortage)
            {
                var dailyPr = await _purchaseRequestRepository.GetPendingPrByDateAsync(today, cancellationToken);
                if (dailyPr == null)
                {
                    var seq = await _purchaseRequestRepository.CountByDateAsync(today, cancellationToken) + 1;
                    string prCode = $"PR-{now:yyyyMMdd}-{seq:D3}";
                    while (await _purchaseRequestRepository.GetByCodeAsync(prCode, cancellationToken) != null)
                    {
                        seq++;
                        prCode = $"PR-{now:yyyyMMdd}-{seq:D3}";
                    }
                    dailyPr = new PurchaseRequest
                    {
                        PrCode = prCode,
                        AggregationDate = today,
                        CreatedBy = currentUserId > 0 ? currentUserId : 1,
                        PrStatus = PrStatusEnum.PENDING_APPROVAL,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    await _purchaseRequestRepository.AddAsync(dailyPr, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                // Chống mua trùng: trừ phần thiếu hụt của (Vật tư, OrderItem) đã nằm trong PR APPROVED đang chờ phát hành PO
                var approvedCoverage = await LoadApprovedPrCoverageAsync(cancellationToken);
                bool queuedAnyShortageLine = false;

                foreach (var item in shortageLinesForPr)
                {
                    var existingItem = dailyPr.Items.FirstOrDefault(i => i.MaterialId == item.MaterialId);
                    var currentAllocForItem = orderItem.MaterialAllocations
                        .FirstOrDefault(ma => ma.MaterialId == item.MaterialId && ma.AllocationStatus != AllocationStatusEnum.RELEASED);
                    // Truyền tổng thiếu hụt thực tế hiện tại của OrderItem (bao gồm cả thiếu gốc lẫn thiếu cấp bù phế phẩm) để MergeSourceOrderJson không làm tụt lượng thiếu
                    var totalItemShortageNow = currentAllocForItem != null
                        ? Math.Max(item.ShortageQty, Math.Round(currentAllocForItem.RequiredQty - (currentAllocForItem.ReservedQty + currentAllocForItem.IssuedQty), 4))
                        : item.ShortageQty;

                    var uncoveredShortage = Math.Max(0m, Math.Round(totalItemShortageNow - approvedCoverage.GetValueOrDefault((item.MaterialId, orderItem.OrderItemId), 0m), 4));
                    if (uncoveredShortage <= 0m)
                    {
                        continue;
                    }

                    queuedAnyShortageLine = true;

                    var sourceOrders = new[]
                    {
                        new
                        {
                            orderItem.OrderId,
                            PoNumber = poNumber,
                            SpaceKey = customerOrder?.OrderSpace?.SpaceKey ?? string.Empty,
                            orderItem.OrderItemId,
                            ProductSku = orderItem.Product?.ProductSku ?? string.Empty,
                            EtdDate = customerOrder?.EtdDate.ToString("yyyy-MM-dd") ?? string.Empty,
                            ShortageQty = uncoveredShortage
                        }
                    };
                    var sourceJson = JsonSerializer.Serialize(sourceOrders);

                    if (existingItem == null)
                    {
                        var newItem = new PurchaseRequestItem
                        {
                            PrId = dailyPr.PrId,
                            MaterialId = item.MaterialId,
                            TotalShortageQty = uncoveredShortage,
                            SuggestedOrderQty = uncoveredShortage,
                            SourceOrderIdsJson = sourceJson
                        };
                        dailyPr.Items.Add(newItem);
                        await _purchaseRequestRepository.AddItemAsync(newItem, cancellationToken);
                    }
                    else
                    {
                        var existingTopUp = Math.Max(0m, existingItem.SuggestedOrderQty - existingItem.TotalShortageQty);
                        var mergedJson = MergeSourceOrderJson(existingItem.SourceOrderIdsJson, sourceJson);
                        var aggregatedShortage = SumShortageFromSourceOrderJson(mergedJson, existingItem.TotalShortageQty + uncoveredShortage);
                        existingItem.SourceOrderIdsJson = mergedJson;
                        existingItem.TotalShortageQty = aggregatedShortage;
                        existingItem.SuggestedOrderQty = Math.Round(aggregatedShortage + existingTopUp, 4);
                    }
                }

                dailyPr.UpdatedAt = now;
                await _purchaseRequestRepository.UpdateAsync(dailyPr);
                if (queuedAnyShortageLine)
                {
                    createdPrId = dailyPr.PrId;
                    createdPrCode = dailyPr.PrCode;
                }
            }

            await _bomRepository.AddAuditLogAsync(new AuditLog
            {
                UserId = currentUserId > 0 ? currentUserId : 1,
                ActionType = "SCRAP_REPLENISHMENT_REQUESTED",
                EntityName = "OrderItem",
                EntityId = orderItem.OrderItemId.ToString(),
                BeforeState = $"ScrappedQty_{request.ScrappedQty}",
                AfterState = JsonSerializer.Serialize(new
                {
                    ScrappedQty = request.ScrappedQty,
                    TotalAllocated = totalAllocated,
                    TotalShortage = totalShortage,
                    CreatedPrId = createdPrId
                }),
                Reason = string.IsNullOrWhiteSpace(request.Reason)
                    ? $"Yêu cầu cấp bù vật tư cho {request.ScrappedQty} SP phế phẩm của đơn {poNumber}."
                    : $"Yêu cầu cấp bù vật tư cho {request.ScrappedQty} SP phế phẩm của đơn {poNumber}. Lý do: {request.Reason.Trim()}",
                CreatedAt = now
            });

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync();

            bool isFullyCovered = totalShortage == 0m;
            string msg = isFullyCovered
                ? $"Đã giữ chỗ đủ vật tư từ kho cho {request.ScrappedQty} sản phẩm sản xuất bù ({totalAllocated} đơn vị vật tư)."
                : createdPrCode != null
                    ? $"Đã giữ chỗ {totalAllocated} đơn vị vật tư từ kho; phần thiếu hụt ({totalShortage} đơn vị) đã tự động tạo Phiếu yêu cầu mua hàng {createdPrCode} trình Chủ xưởng duyệt."
                    : $"Đã giữ chỗ {totalAllocated} đơn vị vật tư từ kho; phần thiếu hụt ({totalShortage} đơn vị) {(request.AutoCreatePrIfShortage ? "đã nằm trong Phiếu yêu cầu mua hàng được duyệt đang chờ phát hành đơn đặt hàng" : "chưa được lập Phiếu yêu cầu mua hàng do tắt tự động tạo PR")}.";

            return new ScrapReplenishmentResultDto
            {
                OrderItemId = orderItem.OrderItemId,
                PoNumber = poNumber,
                ProductSku = orderItem.Product?.ProductSku ?? string.Empty,
                ProductName = orderItem.Product?.ProductName ?? string.Empty,
                ScrappedQty = request.ScrappedQty,
                IsFullyCoveredByStock = isFullyCovered,
                CreatedPurchaseRequestId = createdPrId,
                PurchaseRequestCode = createdPrCode,
                TotalAllocatedFromStock = totalAllocated,
                TotalShortageQty = totalShortage,
                Message = msg,
                Allocations = detailsDtoList
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ScrapReplenishmentPreviewDto> GetScrapReplenishmentPreviewAsync(
        long orderItemId,
        int? overrideScrappedQty = null,
        CancellationToken cancellationToken = default)
    {
        var orderItem = await _orderItemRepository.GetByIdWithFullMrpGraphAsync(orderItemId, cancellationToken);
        if (orderItem == null)
        {
            throw new NotFoundException("Không tìm thấy sản phẩm tương ứng trong đơn hàng.");
        }

        var product = orderItem.Product;
        var order = orderItem.CustomerOrder;
        string poNumber = order?.PoNumber ?? "PO-DEFAULT";

        var bom = orderItem.BomHeader ?? await _bomRepository.GetByOrderItemIdAsync(orderItemId);
        if (bom == null)
        {
            throw new DomainException(
                $"Sản phẩm {product?.ProductName} ({product?.ProductSku}) trong đơn hàng chưa được thiết lập định mức nguyên vật liệu (BOM).",
                "BOM_NOT_FOUND");
        }

        var batches = orderItem.ProductionBatches.ToList();
        var replenishBatches = batches.Where(b => b.BatchType == BatchTypeEnum.REPLENISH_LOT).ToList();
        var mainBatches = batches.Where(b => b.BatchType != BatchTypeEnum.REPLENISH_LOT).ToList();

        int activeMainQty = mainBatches.Sum(b => b.BatchQty);
        int autoDetectedScrap = Math.Max(0, orderItem.OrderedQty - activeMainQty);

        int scrappedQty = (overrideScrappedQty.HasValue && overrideScrappedQty.Value > 0)
            ? overrideScrappedQty.Value
            : autoDetectedScrap;

        var result = new ScrapReplenishmentPreviewDto
        {
            OrderItemId = orderItem.OrderItemId,
            PoNumber = poNumber,
            ProductSku = product?.ProductSku ?? string.Empty,
            ProductName = product?.ProductName ?? string.Empty,
            OrderedQty = orderItem.OrderedQty,
            ActiveBatchQty = activeMainQty,
            ScrappedQty = scrappedQty,
            HasReplenishLotIssued = replenishBatches.Any(),
            ReplenishLotBatchCodes = replenishBatches.Select(b => b.BatchCode).ToList()
        };

        if (scrappedQty <= 0)
        {
            result.IsFullyCoveredByStock = true;
            result.Recommendation = "Dòng đơn hàng hiện đủ sản lượng theo kế hoạch, không có phế phẩm cần cấp bù.";
            return result;
        }

        var groupedBomDetails = bom.BomDetails
            .GroupBy(d => d.MaterialId)
            .Select(g => new
            {
                MaterialId = g.Key,
                Material = g.First().Material,
                GrossRequired = Math.Round(g.Sum(x => CalculateDemandForQuantity(x, scrappedQty, bom.ScrapRatePercent)), 4)
            })
            .ToList();
        decimal totalAllocated = 0m;
        decimal totalShortage = 0m;

        foreach (var detail in groupedBomDetails)
        {
            var grossRequired = detail.GrossRequired;
            var stock = await _materialStockRepository.GetOrCreateByMaterialIdAsync(detail.MaterialId, cancellationToken);

            decimal available = Math.Max(0m, stock.AvailableQty);
            decimal allocated = Math.Min(available, grossRequired);
            decimal shortage = Math.Max(0m, grossRequired - allocated);

            totalAllocated += allocated;
            totalShortage += shortage;

            result.MaterialRequirements.Add(new ScrapMaterialAllocationDetailDto
            {
                MaterialId = detail.MaterialId,
                MaterialCode = detail.Material?.MaterialCode ?? string.Empty,
                MaterialName = detail.Material?.MaterialName ?? string.Empty,
                MaterialGroup = detail.Material?.MaterialGroup.ToString() ?? string.Empty,
                UnitOfMeasure = detail.Material?.UnitOfMeasure ?? string.Empty,
                GrossRequiredQty = grossRequired,
                AllocatedFromStockQty = allocated,
                ShortageQty = shortage,
                AllocationStatus = shortage > 0 ? "SHORTAGE" : "STOCK_AVAILABLE"
            });
        }

        result.TotalAllocatedFromStock = totalAllocated;
        result.TotalShortageQty = totalShortage;
        result.IsFullyCoveredByStock = totalShortage == 0m;

        result.Recommendation = result.IsFullyCoveredByStock
            ? $"Tồn kho khả dụng ĐỦ để cấp phát bù cho {scrappedQty} sản phẩm ngay từ kho xưởng. Quản đốc có thể gửi yêu cầu xuất phôi bù và phát hành lô sản xuất bù."
            : $"Tồn kho KHÔNG ĐỦ vật tư để bù {scrappedQty} sản phẩm (thiếu hụt vật tư). Cần lập Phiếu Yêu cầu mua hàng (PR) gửi Chủ xưởng phê duyệt mua thêm trước khi sản xuất bù.";

        return result;
    }

    /// <summary>
    /// Tính toán các chỉ số phế phẩm và phân bổ cấp bù của một dòng OrderItem (UC-75).
    /// Trả về: (MissingQty, PendingRequestedQty, UnrequestedMissingQty).
    /// </summary>
    private static (int MissingQty, int PendingRequestedQty, int UnrequestedMissingQty) CalculateScrapQuantities(
        OrderItem orderItem,
        BomHeader? bom,
        List<ProductionBatch> batches)
    {
        var activeAndDoneQty = batches.Where(b => b.BatchQty > 0).Sum(b => b.BatchQty);
        var missingQty = Math.Max(0, orderItem.OrderedQty - activeAndDoneQty);

        if (bom == null || bom.BomDetails.Count == 0)
        {
            return (missingQty, 0, missingQty);
        }

        var issuedReplenishLotsQty = batches
            .Where(b => b.BatchType == BatchTypeEnum.REPLENISH_LOT)
            .Sum(b => b.BatchRoutingSteps.OrderBy(s => s.StageSequence).FirstOrDefault()?.TargetQty ?? b.BatchQty);

        decimal cumulativeRequestedUnits = 0m;
        foreach (var g in bom.BomDetails.GroupBy(d => d.MaterialId))
        {
            var perUnitGross = g.Sum(d => d.NetQtyPerUnit) * (1m + bom.ScrapRatePercent / 100m);
            if (perUnitGross <= 0m) continue;

            var alloc = orderItem.MaterialAllocations
                .FirstOrDefault(ma => ma.MaterialId == g.Key && ma.AllocationStatus != AllocationStatusEnum.RELEASED);
            if (alloc == null) continue;

            var baseGross = g.Sum(d => CalculateDemandForQuantity(d, orderItem.OrderedQty, bom.ScrapRatePercent));
            var extraRequired = Math.Max(0m, alloc.RequiredQty - baseGross);
            cumulativeRequestedUnits = Math.Max(cumulativeRequestedUnits, extraRequired / perUnitGross);
        }

        var pendingRequestedQty = (int)Math.Max(0m, Math.Round(cumulativeRequestedUnits, MidpointRounding.AwayFromZero) - issuedReplenishLotsQty);
        var unrequestedMissingQty = Math.Max(0, missingQty - pendingRequestedQty);

        return (missingQty, pendingRequestedQty, unrequestedMissingQty);
    }

    /// <summary>
    /// Kiểm tra xem chuỗi JSON nguồn đơn hàng trong dòng PR có chứa chính xác OrderItemId mục tiêu hay không (tránh so khớp nhầm chuỗi con dạng "X7" với "X").
    /// </summary>
    private static bool SourceOrderJsonMatchesOrderItemId(string? json, long targetOrderItemId)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var elem in doc.RootElement.EnumerateArray())
                {
                    if (elem.TryGetProperty("OrderItemId", out var prop) && prop.GetInt64() == targetOrderItemId)
                    {
                        return true;
                    }
                    if (elem.TryGetProperty("orderItemId", out var propLower) && propLower.GetInt64() == targetOrderItemId)
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
            var pattern = $@"""OrderItemId""\s*:\s*{targetOrderItemId}(?!\d)";
            return System.Text.RegularExpressions.Regex.IsMatch(json, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        return false;
    }

    /// <inheritdoc />
    public async Task<List<ScrapReplenishmentSummaryDto>> GetScrapReplenishmentRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        var activeOrderItems = await _orderItemRepository.GetAllOrderItemsForScrapReplenishmentAsync(cancellationToken);
        var results = new List<ScrapReplenishmentSummaryDto>();
        var prs = await _purchaseRequestRepository.GetAllAsync(null, cancellationToken);

        // Lấy danh sách AuditLog chấp nhận thiếu hụt (UC075.1) để đối chiếu
        var itemIds = activeOrderItems.Select(oi => oi.OrderItemId.ToString()).ToList();
        var absorbLogs = await _bomRepository.GetAuditLogsAsync("OrderItem", itemIds, "SCRAP_REPLENISHMENT_ABSORBED", cancellationToken);
        var absorbedMap = new Dictionary<long, int>();
        foreach (var log in absorbLogs)
        {
            if (long.TryParse(log.EntityId, out var oId) && !string.IsNullOrEmpty(log.AfterState))
            {
                try
                {
                    using var doc = JsonDocument.Parse(log.AfterState);
                    if (doc.RootElement.TryGetProperty("AbsorbedShortfallQty", out var prop))
                    {
                        absorbedMap[oId] = absorbedMap.GetValueOrDefault(oId, 0) + prop.GetInt32();
                    }
                }
                catch { }
            }
        }

        // Lọc danh sách PR đang mở (chỉ PENDING_APPROVAL hoặc APPROVED, loại trừ REJECTED và PO_ISSUED)
        var openPrs = prs
            .Where(pr => pr.PrStatus == PrStatusEnum.PENDING_APPROVAL || pr.PrStatus == PrStatusEnum.APPROVED)
            .ToList();

        foreach (var item in activeOrderItems)
        {
            var batches = item.ProductionBatches.ToList();
            var mainBatches = batches.Where(b => b.BatchType != BatchTypeEnum.REPLENISH_LOT).ToList();
            var replenishBatches = batches.Where(b => b.BatchType == BatchTypeEnum.REPLENISH_LOT).ToList();

            int activeMainQty = mainBatches.Sum(b => b.BatchQty);
            int totalScrapped = Math.Max(0, item.OrderedQty - activeMainQty);
            if (totalScrapped <= 0)
            {
                continue;
            }

            int issuedRepQty = replenishBatches.Sum(b => b.BatchQty);
            int absorbedQty = absorbedMap.GetValueOrDefault(item.OrderItemId, 0);
            int missingQty = Math.Max(0, item.OrderedQty - (activeMainQty + issuedRepQty) - absorbedQty);

            var order = item.CustomerOrder;
            decimal tolerancePcnt = 5.0m;
            int maxAbsorbAllowed = (int)Math.Ceiling(item.OrderedQty * (tolerancePcnt / 100m));
            bool canAbsorb = missingQty > 0 && missingQty <= maxAbsorbAllowed;

            // Xác định trạng thái cấp bù
            var activeAllocations = item.MaterialAllocations
                .Where(a => a.AllocationStatus != AllocationStatusEnum.RELEASED)
                .ToList();

            var (rawMissing, pendingRequestedQty, _) = CalculateScrapQuantities(item, item.BomHeader, batches);
            bool hasScrapAllocationsRequested = pendingRequestedQty > 0;
            bool isMaterialIssued = hasScrapAllocationsRequested && activeAllocations.All(a => a.AllocationStatus == AllocationStatusEnum.ISSUED_TO_SHOP);
            bool isMaterialReserved = hasScrapAllocationsRequested && activeAllocations.Any(a => a.ReservedQty > 0);

            // Tìm PR liên quan đang mở khớp CHÍNH XÁC OrderItemId
            PurchaseRequest? relatedPr = null;
            foreach (var pr in openPrs)
            {
                if (pr.Items.Any(pri => SourceOrderJsonMatchesOrderItemId(pri.SourceOrderIdsJson, item.OrderItemId)))
                {
                    relatedPr = pr;
                    break;
                }
            }

            string status;
            string statusLabel;
            if (absorbedQty > 0 && missingQty == 0)
            {
                status = "SHORTFALL_ABSORBED";
                statusLabel = "Chủ xưởng đã chấp nhận thiếu hụt (Đóng luồng)";
            }
            else if (missingQty == 0)
            {
                status = "LOT_ISSUED";
                statusLabel = "Đã phát hành lô sản xuất bù đủ";
            }
            else if (isMaterialIssued)
            {
                status = "MATERIAL_ISSUED";
                statusLabel = "Đã xuất kho phôi bù, chờ phát hành thẻ lô";
            }
            else if (relatedPr != null && relatedPr.PrStatus == PrStatusEnum.PENDING_APPROVAL)
            {
                status = "PR_PENDING_APPROVAL";
                statusLabel = $"Chờ duyệt PR mua thêm ({relatedPr.PrCode})";
            }
            else if (isMaterialReserved)
            {
                status = "MATERIAL_RESERVED";
                statusLabel = "Đã giữ chỗ vật tư, chờ xuất kho";
            }
            else
            {
                status = "PENDING_REVIEW";
                statusLabel = "Chờ Chủ xưởng / Quản đốc xem xét cấp bù";
            }

            // Kiểm tra tồn kho khả dụng phải gộp theo từng MaterialId
            bool isStockSufficient = false;
            if (item.BomHeader != null && missingQty > 0 && item.BomHeader.BomDetails.Count > 0)
            {
                var groupedDemands = item.BomHeader.BomDetails
                    .GroupBy(d => d.MaterialId)
                    .Select(g => new
                    {
                        MaterialId = g.Key,
                        TotalGross = g.Sum(d => CalculateDemandForQuantity(d, missingQty, item.BomHeader.ScrapRatePercent)),
                        AvailableStock = g.First().Material?.MaterialStock?.AvailableQty ?? 0m
                    })
                    .ToList();

                isStockSufficient = groupedDemands.Count > 0 && groupedDemands.All(g => g.AvailableStock >= g.TotalGross);
            }

            results.Add(new ScrapReplenishmentSummaryDto
            {
                OrderItemId = item.OrderItemId,
                OrderId = item.OrderId,
                PoNumber = order?.PoNumber ?? "PO-DEFAULT",
                CustomerName = order?.Customer?.CompanyName ?? string.Empty,
                ProductSku = item.Product?.ProductSku ?? string.Empty,
                ProductName = item.Product?.ProductName ?? string.Empty,
                OrderedQty = item.OrderedQty,
                ActiveBatchQty = activeMainQty,
                TotalScrappedQty = totalScrapped,
                IssuedReplenishLotQty = issuedRepQty,
                MissingQty = missingQty,
                CanAbsorbShortfall = canAbsorb,
                ShortfallTolerancePercent = tolerancePcnt,
                AbsorbedShortfallQty = absorbedQty > 0 ? absorbedQty : null,
                Status = status,
                StatusLabel = statusLabel,
                IsStockSufficient = isStockSufficient,
                PurchaseRequestId = relatedPr?.PrId,
                PurchaseRequestCode = relatedPr?.PrCode,
                PurchaseRequestStatus = relatedPr?.PrStatus.ToString(),
                EtdDate = order?.EtdDate
            });
        }

        return results.OrderBy(r => r.EtdDate).ToList();
    }

    /// <inheritdoc />
    public async Task<AbsorbScrapShortfallResultDto> AbsorbScrapShortfallAsync(
        long orderItemId,
        AbsorbScrapShortfallRequestDto request,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Reason))
        {
            throw new DomainException("Bắt buộc phải nhập lý do khi Chủ xưởng quyết định chấp nhận thiếu hụt phế phẩm.", "REASON_REQUIRED");
        }

        var orderItem = await _orderItemRepository.GetByIdWithFullMrpGraphAsync(orderItemId, cancellationToken);
        if (orderItem == null)
        {
            throw new NotFoundException("Không tìm thấy sản phẩm tương ứng trong đơn hàng.");
        }

        var batches = orderItem.ProductionBatches.ToList();
        var mainBatches = batches.Where(b => b.BatchType != BatchTypeEnum.REPLENISH_LOT).ToList();
        var replenishBatches = batches.Where(b => b.BatchType == BatchTypeEnum.REPLENISH_LOT).ToList();

        int activeMainQty = mainBatches.Sum(b => b.BatchQty);
        int issuedRepQty = replenishBatches.Sum(b => b.BatchQty);
        int missingQty = Math.Max(0, orderItem.OrderedQty - (activeMainQty + issuedRepQty));

        if (missingQty <= 0)
        {
            throw new DomainException($"Đơn hàng đã đủ sản lượng ({activeMainQty + issuedRepQty}/{orderItem.OrderedQty} SP), không có thiếu hụt để chấp nhận.", "NO_SHORTFALL_TO_ABSORB");
        }

        // Chặn nếu dòng sản phẩm này đã được chấp nhận thiếu hụt trước đó
        var existingAbsorbLogs = await _bomRepository.GetAuditLogsAsync("OrderItem", new[] { orderItemId.ToString() }, "SCRAP_REPLENISHMENT_ABSORBED", cancellationToken);
        if (existingAbsorbLogs.Any())
        {
            throw new DomainException(
                "Dòng sản phẩm đơn hàng này đã được Chủ xưởng chấp nhận thiếu hụt phế phẩm trước đó.",
                "SHORTFALL_ALREADY_ABSORBED");
        }

        // Chặn nếu đã có yêu cầu cấp bù đang giữ chỗ vật tư trong kho (tránh treo tồn kho giữ chỗ)
        var bom = orderItem.BomHeader ?? await _bomRepository.GetByOrderItemIdAsync(orderItemId);
        if (bom != null)
        {
            var (_, pendingRequestedQty, _) = CalculateScrapQuantities(orderItem, bom, batches);
            if (pendingRequestedQty > 0)
            {
                throw new DomainException(
                    "Đã có yêu cầu cấp bù vật tư đang giữ chỗ trong kho. Vui lòng giải phóng giữ chỗ hoặc phát hành lô bù trước khi chấp nhận thiếu hụt.",
                    "SCRAP_REPLENISHMENT_ALREADY_REQUESTED");
            }
        }

        decimal tolerancePcnt = 5.0m;
        int maxAbsorbAllowed = (int)Math.Ceiling(orderItem.OrderedQty * (tolerancePcnt / 100m));
        if (missingQty > maxAbsorbAllowed)
        {
            throw new DomainException(
                $"Số lượng thiếu hụt ({missingQty} SP, tương đương {(missingQty * 100m / orderItem.OrderedQty):0.##}%) vượt quá dung sai hợp đồng cho phép tối đa ({tolerancePcnt}%, tức {maxAbsorbAllowed} SP). Bắt buộc phải sản xuất bù hoặc thỏa thuận lại hợp đồng.",
                "SHORTFALL_EXCEEDS_TOLERANCE");
        }

        decimal shortfallPct = Math.Round(missingQty * 100m / orderItem.OrderedQty, 2);

        // Ghi AuditLog bất biến ghi nhận quyết định chấp nhận thiếu hụt của Chủ xưởng (GBR-05, UC075.1)
        await _bomRepository.AddAuditLogAsync(new AuditLog
        {
            UserId = currentUserId,
            ActionType = "SCRAP_REPLENISHMENT_ABSORBED",
            EntityName = "OrderItem",
            EntityId = orderItemId.ToString(),
            BeforeState = JsonSerializer.Serialize(new { OrderedQty = orderItem.OrderedQty, MissingQty = missingQty }),
            AfterState = JsonSerializer.Serialize(new
            {
                OrderItemId = orderItemId,
                AbsorbedShortfallQty = missingQty,
                ShortfallPercentage = shortfallPct,
                Reason = request.Reason.Trim(),
                DecidedBy = currentUserId
            }),
            Reason = $"Chủ xưởng chấp nhận thiếu hụt {missingQty}/{orderItem.OrderedQty} SP ({shortfallPct}%, trong dung sai {tolerancePcnt}%). Không phát hành lô bù. Lý do: {request.Reason.Trim()}",
            CreatedAt = DateTime.UtcNow
        });

        // Đóng luồng: Nếu tất cả các lô hiện tại của OrderItem đều DONE thì hoàn thành OrderItem và đơn hàng
        var existingBatches = orderItem.ProductionBatches.ToList();
        bool allBatchesDone = existingBatches.Count > 0 && existingBatches.All(b => b.BatchStatus == BatchStatusEnum.DONE);
        if (allBatchesDone)
        {
            orderItem.Status = OrderItemStatusEnum.DONE;
            await _orderItemRepository.UpdateAsync(orderItem);

            var order = orderItem.CustomerOrder ?? await _orderItemRepository.GetOrderByIdAsync(orderItem.OrderId, cancellationToken);
            if (order != null)
            {
                var orderWithItems = await _orderItemRepository.GetOrderWithFullProgressGraphAsync(order.OrderId, cancellationToken);
                if (orderWithItems != null && orderWithItems.OrderItems.All(oi => oi.OrderItemId == orderItem.OrderItemId ? true : oi.Status == OrderItemStatusEnum.DONE))
                {
                    order.Status = OrderStatusEnum.COMPLETED;
                    await _orderItemRepository.UpdateOrderAsync(order);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AbsorbScrapShortfallResultDto
        {
            OrderItemId = orderItemId,
            PoNumber = orderItem.CustomerOrder?.PoNumber ?? "PO-DEFAULT",
            ProductSku = orderItem.Product?.ProductSku ?? string.Empty,
            OrderedQty = orderItem.OrderedQty,
            AbsorbedShortfallQty = missingQty,
            ShortfallPercentage = shortfallPct,
            Message = $"Chủ xưởng đã xác nhận chấp nhận thiếu hụt {missingQty} SP ({shortfallPct}%) cho đơn hàng {orderItem.CustomerOrder?.PoNumber}. Đã ghi nhận nhật ký kiểm toán đóng luồng cấp bù.",
            DecidedAt = DateTime.UtcNow
        };
    }

    /// <inheritdoc />
    public async Task<ScrapReplenishmentResultDto> ApproveScrapReplenishmentAsync(
        long orderItemId,
        ApproveScrapReplenishmentRequestDto? request,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var orderItem = await _orderItemRepository.GetByIdWithFullMrpGraphAsync(orderItemId, cancellationToken);
        if (orderItem == null)
        {
            throw new NotFoundException("Không tìm thấy sản phẩm tương ứng trong đơn hàng.");
        }

        var bom = orderItem.BomHeader ?? await _bomRepository.GetByOrderItemIdAsync(orderItemId);
        if (bom == null)
        {
            throw new DomainException($"Sản phẩm '{orderItem.Product?.ProductName}' chưa được thiết lập định mức BOM.", "BOM_NOT_CONFIGURED");
        }

        var batches = orderItem.ProductionBatches.ToList();
        var (missingQty, pendingRequestedQty, unrequestedMissingQty) = CalculateScrapQuantities(orderItem, bom, batches);

        if (missingQty <= 0)
        {
            throw new DomainException("Dòng sản phẩm không phát sinh phế phẩm cần phê duyệt cấp bù.", "NO_SCRAP_DETECTED");
        }

        ScrapReplenishmentResultDto result;
        if (unrequestedMissingQty > 0)
        {
            // Chỉ gửi yêu cầu giữ chỗ cho phần sản lượng phế phẩm chưa được yêu cầu trước đó
            var scrapReq = new ScrapReplenishmentRequestDto
            {
                OrderItemId = orderItemId,
                ScrappedQty = unrequestedMissingQty,
                Reason = request?.ApprovalNote ?? "Chủ xưởng phê duyệt cấp bù nguyên vật liệu cho phế phẩm sản xuất.",
                AutoCreatePrIfShortage = request?.AutoCreatePrIfShortage ?? true
            };
            result = await RequestScrapReplenishmentAsync(scrapReq, currentUserId, cancellationToken);
        }
        else
        {
            // Quản đốc/Tổ trưởng đã gửi yêu cầu cấp bù đủ trước đó: Chủ xưởng phê duyệt không lặp lại giữ chỗ vật tư
            var detailsDtoList = new List<ScrapMaterialAllocationDetailDto>();
            decimal totalAllocated = 0m;
            decimal totalShortage = 0m;

            foreach (var g in bom.BomDetails.GroupBy(d => d.MaterialId))
            {
                var alloc = orderItem.MaterialAllocations
                    .FirstOrDefault(ma => ma.MaterialId == g.Key && ma.AllocationStatus != AllocationStatusEnum.RELEASED);
                var baseGross = g.Sum(d => CalculateDemandForQuantity(d, orderItem.OrderedQty, bom.ScrapRatePercent));
                var extraGross = alloc != null ? Math.Max(0m, alloc.RequiredQty - baseGross) : 0m;
                var extraReserved = alloc != null ? Math.Min(alloc.ReservedQty, extraGross) : 0m;
                var extraShortage = Math.Max(0m, extraGross - extraReserved);

                totalAllocated += extraReserved;
                totalShortage += extraShortage;

                detailsDtoList.Add(new ScrapMaterialAllocationDetailDto
                {
                    MaterialId = g.Key,
                    MaterialCode = g.First().Material?.MaterialCode ?? string.Empty,
                    MaterialName = g.First().Material?.MaterialName ?? string.Empty,
                    MaterialGroup = g.First().Material?.MaterialGroup.ToString() ?? string.Empty,
                    UnitOfMeasure = g.First().Material?.UnitOfMeasure ?? string.Empty,
                    GrossRequiredQty = extraGross,
                    AllocatedFromStockQty = extraReserved,
                    ShortageQty = extraShortage,
                    AllocationStatus = alloc?.AllocationStatus.ToString() ?? "RESERVED"
                });
            }

            result = new ScrapReplenishmentResultDto
            {
                OrderItemId = orderItem.OrderItemId,
                PoNumber = orderItem.CustomerOrder?.PoNumber ?? $"PO-{orderItem.OrderId}",
                ProductSku = orderItem.Product?.ProductSku ?? string.Empty,
                ProductName = orderItem.Product?.ProductName ?? string.Empty,
                ScrappedQty = missingQty,
                IsFullyCoveredByStock = totalShortage == 0m,
                TotalAllocatedFromStock = totalAllocated,
                TotalShortageQty = totalShortage,
                Message = "Chủ xưởng đã phê duyệt yêu cầu cấp bù vật tư phế phẩm đã lập trước đó.",
                Allocations = detailsDtoList
            };
        }

        // Ghi AuditLog xác nhận phê duyệt từ Chủ xưởng (GBR-05)
        await _bomRepository.AddAuditLogAsync(new AuditLog
        {
            UserId = currentUserId,
            ActionType = "SCRAP_REPLENISHMENT_APPROVED_BY_OWNER",
            EntityName = "OrderItem",
            EntityId = orderItemId.ToString(),
            BeforeState = null,
            AfterState = JsonSerializer.Serialize(new
            {
                OrderItemId = orderItemId,
                ScrappedQty = missingQty,
                result.IsFullyCoveredByStock,
                result.CreatedPurchaseRequestId,
                result.PurchaseRequestCode,
                result.TotalAllocatedFromStock,
                result.TotalShortageQty,
                Note = request?.ApprovalNote
            }),
            Reason = $"Chủ xưởng phê duyệt cấp bù vật tư cho {missingQty} SP phế phẩm. Ghi chú: {request?.ApprovalNote ?? "Đồng ý cấp bù"}",
            CreatedAt = DateTime.UtcNow
        });
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return result;
    }

    // =========================================================================
    // PRIVATE CORE ENGINE METHODS (MRP & AUTO-ALLOCATION & DAILY PR QUEUEING)
    // =========================================================================

    private async Task<AutoAllocationResultDto> ExecuteCoreAutoAllocationAsync(
        List<OrderItem> orderItems,
        CustomerOrder? order,
        OrderSpace? space,
        AutoAllocationRequestDto options,
        long currentUserId,
        CancellationToken cancellationToken)
    {
        var itemsWithBom = orderItems
            .Where(oi => (oi.CustomerOrder == null || oi.CustomerOrder.Status is not (OrderStatusEnum.CANCELLED or OrderStatusEnum.COMPLETED))
                         && oi.BomHeader != null
                         && oi.BomHeader.BomDetails.Count > 0
                         && oi.BomHeader.BomStatus != BomStatusEnum.DRAFT)
            .OrderBy(oi => oi.CustomerOrder?.EtdDate ?? DateTime.MaxValue)
            .ThenBy(oi => oi.OrderItemId)
            .ToList();

        if (itemsWithBom.Count == 0)
        {
            throw new AppException("Chưa có định mức nguyên vật liệu (BOM) nào được thiết lập cho đơn hàng này để chạy tính toán nhu cầu vật tư và phân bổ tồn kho tự động.", 400, MessageCodes.Msg13ValidationError);
        }

        var allMaterialIds = itemsWithBom
            .SelectMany(oi => oi.BomHeader!.BomDetails.Select(bd => bd.MaterialId))
            .Distinct()
            .ToList();

        var stockMap = await _materialStockRepository.GetByMaterialIdsAsync(allMaterialIds, cancellationToken);
        var now = DateTime.UtcNow;

        int fullyAllocatedItemsCount = 0;
        int waitingMaterialItemsCount = 0;
        int totalMaterialAllocationsUpdated = 0;
        var allocationDtos = new List<MaterialOrderAllocationDto>();

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            foreach (var item in itemsWithBom)
            {
                var bom = item.BomHeader!;

                // Gộp các dòng BomDetail có cùng MaterialId trên cùng 1 OrderItem để tránh tạo trùng bản ghi giữ chỗ
                var groupedBomLines = bom.BomDetails
                    .GroupBy(d => d.MaterialId)
                    .Select(g => new
                    {
                        MaterialId = g.Key,
                        Material = g.First().Material,
                        TotalNetPerUnit = g.Sum(x => x.NetQtyPerUnit),
                        TotalGrossDemand = Math.Round(g.Sum(x => ResolveGrossDemand(x, item.OrderedQty, bom.ScrapRatePercent)), 4)
                    })
                    .ToList();

                bool itemHasShortage = false;

                foreach (var line in groupedBomLines)
                {
                    if (!stockMap.TryGetValue(line.MaterialId, out var stock))
                    {
                        stock = await _materialStockRepository.GetOrCreateByMaterialIdAsync(line.MaterialId, cancellationToken);
                        stockMap[line.MaterialId] = stock;
                    }

                    var existingAlloc = item.MaterialAllocations
                        .FirstOrDefault(ma => ma.MaterialId == line.MaterialId && ma.AllocationStatus != AllocationStatusEnum.RELEASED);

                    var alreadyReservedOrIssued = existingAlloc != null
                        ? existingAlloc.ReservedQty + existingAlloc.IssuedQty
                        : 0m;

                    var effectiveRequired = existingAlloc != null
                        ? Math.Max(line.TotalGrossDemand, existingAlloc.RequiredQty)
                        : line.TotalGrossDemand;

                    var stillNeeded = Math.Max(0m, Math.Round(effectiveRequired - alreadyReservedOrIssued, 4));
                    decimal newlyReserved = 0m;

                    // BR-INV-01 & BR-INV-02: Chỉ khóa phân bổ mềm từ AvailableQty (đã loại trừ phần giữ chỗ của đơn khác)
                    if (stillNeeded > 0m && stock.AvailableQty > 0m)
                    {
                        newlyReserved = stock.AllocateUpToAvailable(stillNeeded);
                        if (newlyReserved > 0m)
                        {
                            await _materialStockRepository.UpdateAsync(stock);
                        }
                    }

                    if (existingAlloc == null)
                    {
                        existingAlloc = new MaterialAllocation
                        {
                            OrderItemId = item.OrderItemId,
                            OrderItem = item,
                            MaterialId = line.MaterialId,
                            Material = line.Material,
                            RequiredQty = effectiveRequired,
                            ReservedQty = newlyReserved,
                            IssuedQty = 0m,
                            AllocationStatus = AllocationStatusEnum.RESERVED,
                            AllocatedAt = now
                        };
                        item.MaterialAllocations.Add(existingAlloc);
                        await _materialAllocationRepository.AddAsync(existingAlloc, cancellationToken);
                    }
                    else
                    {
                        existingAlloc.RequiredQty = effectiveRequired;
                        existingAlloc.ReservedQty = Math.Round(existingAlloc.ReservedQty + newlyReserved, 4);
                        existingAlloc.AllocatedAt = now;
                        await _materialAllocationRepository.UpdateAsync(existingAlloc);
                    }

                    totalMaterialAllocationsUpdated++;
                    allocationDtos.Add(MapAllocationDto(existingAlloc, item));

                    var remainingLineShortage = Math.Max(0m, Math.Round(existingAlloc.RequiredQty - (existingAlloc.ReservedQty + existingAlloc.IssuedQty), 4));
                    if (remainingLineShortage > 0m)
                    {
                        itemHasShortage = true;
                    }
                }

                // Cập nhật trạng thái OrderItem & BomHeader theo kết quả phân bổ tồn kho (SC-14)
                if (itemHasShortage)
                {
                    waitingMaterialItemsCount++;
                    if (item.Status != OrderItemStatusEnum.IN_PROD && item.Status != OrderItemStatusEnum.DONE)
                    {
                        item.Status = OrderItemStatusEnum.WAITING_MAT;
                    }
                    bom.BomStatus = BomStatusEnum.WAITING_FOR_MATERIALS;
                }
                else
                {
                    fullyAllocatedItemsCount++;
                    if (item.Status != OrderItemStatusEnum.IN_PROD && item.Status != OrderItemStatusEnum.DONE)
                    {
                        item.Status = OrderItemStatusEnum.READY_PROD;
                    }
                    bom.BomStatus = BomStatusEnum.READY_FOR_PRODUCTION;
                }

                bom.UpdatedAt = now;
                await _bomRepository.UpdateAsync(bom);
                await _orderItemRepository.UpdateAsync(item);
            }

            if (order != null && order.Status == OrderStatusEnum.CONFIRMED)
            {
                order.Status = OrderStatusEnum.IN_PLANNING;
                order.UpdatedAt = now;
                await _orderItemRepository.UpdateOrderAsync(order);
            }

            var warningDays = (int)await _systemConfigRepository.GetDecimalConfigAsync(SystemConfigKeys.EtdWarningDays, 7m);
            var postAllocationAnalysis = BuildMrpAnalysisResult(
                scope: space != null ? "ORDER_SPACE" : "ORDER",
                orderItems: itemsWithBom,
                order: order,
                space: space,
                etdWarningDays: warningDays);

            var remainingShortages = postAllocationAnalysis.MaterialRequirements
                .Where(m => m.NetShortageQty > 0m)
                .ToList();

            long? consolidatedPrId = null;
            string? consolidatedPrCode = null;
            int queuedShortageCount = 0;

            // BR-PUR-01: Tự động gom các vật tư thiếu hụt (NetShortageQty > 0) vào Phiếu PR Tổng hợp trong ngày (consolidated_prs)
            if (options.AutoQueueShortageToDailyPr && remainingShortages.Count > 0)
            {
                var (prId, prCode, count) = await UpsertDailyConsolidatedPrAsync(
                    remainingShortages,
                    currentUserId > 0 ? currentUserId : 1,
                    now,
                    cancellationToken);

                consolidatedPrId = prId;
                consolidatedPrCode = prCode;
                queuedShortageCount = count;
            }

            var overallReadiness = waitingMaterialItemsCount == 0
                ? "READY_TO_PRODUCE"
                : "WAITING_FOR_MATERIAL";

            // Ghi vết kiểm toán bất biến GBR-05
            await _bomRepository.AddAuditLogAsync(new AuditLog
            {
                UserId = currentUserId > 0 ? currentUserId : 1,
                ActionType = AuditActionConstants.InventoryAutoAllocated,
                EntityName = space != null ? "OrderSpace" : "CustomerOrder",
                EntityId = (space?.SpaceId ?? order?.OrderId ?? itemsWithBom[0].OrderId).ToString(),
                BeforeState = null,
                AfterState = JsonSerializer.Serialize(new
                {
                    OrderId = order?.OrderId,
                    PoNumber = order?.PoNumber,
                    SpaceKey = space?.SpaceKey,
                    OverallReadinessStatus = overallReadiness,
                    FullyAllocatedItemsCount = fullyAllocatedItemsCount,
                    WaitingMaterialItemsCount = waitingMaterialItemsCount,
                    TotalMaterialAllocationsUpdated = totalMaterialAllocationsUpdated,
                    ConsolidatedPrCode = consolidatedPrCode,
                    QueuedShortageMaterialsCount = queuedShortageCount
                }),
                Reason = $"Thực thi tính toán nhu cầu vật tư & phân bổ tồn kho tự động: {(waitingMaterialItemsCount == 0 ? "Đã đủ vật tư sẵn sàng sản xuất" : "Đang chờ mua bổ sung vật tư")}, đã giữ chỗ đủ cho {fullyAllocatedItemsCount}/{itemsWithBom.Count} dòng sản phẩm" +
                         (consolidatedPrCode != null ? $", đã gom phần thiếu hụt vào phiếu PR tổng hợp {consolidatedPrCode}." : "."),
                CreatedAt = now
            });

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync();

            return new AutoAllocationResultDto
            {
                OrderId = order?.OrderId,
                PoNumber = order?.PoNumber,
                SpaceId = space?.SpaceId,
                SpaceKey = space?.SpaceKey,
                ExecutedAt = now,
                OverallReadinessStatus = overallReadiness,
                TotalOrderItemsProcessed = itemsWithBom.Count,
                FullyAllocatedItemsCount = fullyAllocatedItemsCount,
                WaitingMaterialItemsCount = waitingMaterialItemsCount,
                TotalMaterialAllocationsUpdated = totalMaterialAllocationsUpdated,
                ConsolidatedPrId = consolidatedPrId,
                ConsolidatedPrCode = consolidatedPrCode,
                QueuedShortageMaterialsCount = queuedShortageCount,
                Allocations = allocationDtos,
                RemainingShortages = remainingShortages
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    private async Task<(long? PrId, string? PrCode, int QueuedMaterialsCount)> UpsertDailyConsolidatedPrAsync(
        List<MrpMaterialRequirementLineDto> shortageLines,
        long currentUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // Chống mua trùng (Double Purchasing): chỉ trừ lượng thiếu hụt của (MaterialId, OrderItemId) đang nằm trong các PR ở trạng thái APPROVED.
        var coveredOrderItemMaterials = await LoadApprovedPrCoverageAsync(cancellationToken);

        // Lọc ra các dòng thực sự còn thiếu chưa nằm trong PR đã duyệt / đã phát hành PO
        var linesToQueue = new List<(MrpMaterialRequirementLineDto Line, List<object> SourceOrders, decimal UncoveredShortage)>();
        foreach (var line in shortageLines)
        {
            var uncoveredSources = new List<object>();
            decimal uncoveredTotal = 0m;

            foreach (var b in line.ItemBreakdowns.Where(x => x.UnfulfilledDemandQty > 0m))
            {
                var alreadyOrderedQty = coveredOrderItemMaterials.GetValueOrDefault((line.MaterialId, b.OrderItemId), 0m);
                var netUncovered = Math.Max(0m, Math.Round(b.UnfulfilledDemandQty - alreadyOrderedQty, 4));
                if (netUncovered <= 0m) continue;

                uncoveredTotal += netUncovered;
                uncoveredSources.Add(new
                {
                    b.OrderId,
                    b.PoNumber,
                    b.SpaceKey,
                    b.OrderItemId,
                    b.ProductSku,
                    EtdDate = b.EtdDate.ToString("yyyy-MM-dd"),
                    ShortageQty = netUncovered
                });
            }

            if (uncoveredTotal > 0m)
            {
                linesToQueue.Add((line, uncoveredSources, Math.Round(uncoveredTotal, 4)));
            }
        }

        if (linesToQueue.Count == 0)
        {
            return (null, null, 0);
        }

        var today = now.Date;
        var dailyPr = await _purchaseRequestRepository.GetPendingPrByDateAsync(today, cancellationToken);

        if (dailyPr == null)
        {
            var seq = await _purchaseRequestRepository.CountByDateAsync(today, cancellationToken) + 1;
            var prCode = $"PR-{today:yyyyMMdd}-{seq:D3}";
            while (await _purchaseRequestRepository.GetByCodeAsync(prCode, cancellationToken) != null)
            {
                seq++;
                prCode = $"PR-{today:yyyyMMdd}-{seq:D3}";
            }

            dailyPr = new PurchaseRequest
            {
                PrCode = prCode,
                AggregationDate = today,
                CreatedBy = currentUserId,
                PrStatus = PrStatusEnum.PENDING_APPROVAL,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _purchaseRequestRepository.AddAsync(dailyPr, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        foreach (var (line, sourceOrders, uncoveredShortage) in linesToQueue)
        {
            var existingItem = dailyPr.Items.FirstOrDefault(i => i.MaterialId == line.MaterialId);
            var sourceJson = JsonSerializer.Serialize(sourceOrders);
            var safetyTopUp = Math.Max(0m, line.SuggestedPrOrderQty - line.NetShortageQty);

            if (existingItem == null)
            {
                var newItem = new PurchaseRequestItem
                {
                    PrId = dailyPr.PrId,
                    MaterialId = line.MaterialId,
                    TotalShortageQty = uncoveredShortage,
                    SuggestedOrderQty = Math.Round(uncoveredShortage + safetyTopUp, 4),
                    SourceOrderIdsJson = sourceJson
                };
                dailyPr.Items.Add(newItem);
                await _purchaseRequestRepository.AddItemAsync(newItem, cancellationToken);
            }
            else
            {
                // BR-PUR-01 / UC-44: Hợp nhất danh sách đơn nguồn theo từng OrderItemId rồi cộng tổng thiếu hụt thực tế (không cộng trùng) và giữ bù tồn kho an toàn (Safety Stock)
                var mergedJson = MergeSourceOrderJson(existingItem.SourceOrderIdsJson, sourceJson);
                var aggregatedShortage = SumShortageFromSourceOrderJson(mergedJson, uncoveredShortage);
                var existingTopUp = Math.Max(0m, existingItem.SuggestedOrderQty - existingItem.TotalShortageQty);
                existingItem.SourceOrderIdsJson = mergedJson;
                existingItem.TotalShortageQty = aggregatedShortage;
                existingItem.SuggestedOrderQty = Math.Round(aggregatedShortage + Math.Max(safetyTopUp, existingTopUp), 4);
            }
        }

        dailyPr.UpdatedAt = now;
        await _purchaseRequestRepository.UpdateAsync(dailyPr);

        return (dailyPr.PrId, dailyPr.PrCode, linesToQueue.Count);
    }

    /// <summary>
    /// Tính tổng lượng thiếu hụt (<c>ShortageQty</c>) từ danh sách đơn hàng nguồn đã hợp nhất và khử trùng theo <c>OrderItemId</c> (BR-PUR-01).
    /// </summary>
    private static decimal SumShortageFromSourceOrderJson(string mergedJson, decimal fallbackQty)
    {
        if (string.IsNullOrWhiteSpace(mergedJson)) return fallbackQty;
        try
        {
            using var doc = JsonDocument.Parse(mergedJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return fallbackQty;

            decimal total = 0m;
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.TryGetProperty("ShortageQty", out var prop) && prop.TryGetDecimal(out var qty) && qty > 0m)
                {
                    total += qty;
                }
            }

            return total > 0m ? Math.Round(total, 4) : fallbackQty;
        }
        catch
        {
            return fallbackQty;
        }
    }

    private static string MergeSourceOrderJson(string? existingJson, string newJson)
    {
        if (string.IsNullOrWhiteSpace(existingJson)) return newJson;
        try
        {
            using var doc1 = JsonDocument.Parse(existingJson);
            using var doc2 = JsonDocument.Parse(newJson);
            var dict = new Dictionary<string, JsonElement>();

            if (doc1.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc1.RootElement.EnumerateArray())
                {
                    var key = el.TryGetProperty("OrderItemId", out var p) ? p.ToString() : Guid.NewGuid().ToString();
                    dict[key] = el.Clone();
                }
            }

            if (doc2.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc2.RootElement.EnumerateArray())
                {
                    var key = el.TryGetProperty("OrderItemId", out var p) ? p.ToString() : Guid.NewGuid().ToString();
                    dict[key] = el.Clone();
                }
            }

            return JsonSerializer.Serialize(dict.Values);
        }
        catch
        {
            return newJson;
        }
    }

    private static MrpAnalysisResultDto BuildMrpAnalysisResult(
        string scope,
        List<OrderItem> orderItems,
        CustomerOrder? order,
        OrderSpace? space,
        int etdWarningDays)
    {
        var materialGroups = new Dictionary<long, (Material Material, List<MrpOrderItemBreakdownDto> Breakdowns)>();

        int readyItems = 0;
        int waitingItems = 0;

        foreach (var item in orderItems)
        {
            if (item.BomHeader == null || item.BomHeader.BomDetails.Count == 0)
            {
                waitingItems++;
                continue;
            }

            var bom = item.BomHeader;
            bool itemShortage = false;

            // Nhóm theo MaterialId trong phạm vi 1 OrderItem
            var itemMaterialLines = bom.BomDetails
                .GroupBy(bd => bd.MaterialId)
                .Select(g => new
                {
                    MaterialId = g.Key,
                    Material = g.First().Material,
                    NetQtyPerUnit = g.Sum(x => x.NetQtyPerUnit),
                    GrossDemand = Math.Round(g.Sum(x => ResolveGrossDemand(x, item.OrderedQty, bom.ScrapRatePercent)), 4)
                })
                .ToList();

            foreach (var line in itemMaterialLines)
            {
                var alloc = item.MaterialAllocations
                    .FirstOrDefault(ma => ma.MaterialId == line.MaterialId && ma.AllocationStatus != AllocationStatusEnum.RELEASED);

                var effectiveGrossDemand = alloc != null
                    ? Math.Max(line.GrossDemand, alloc.RequiredQty)
                    : line.GrossDemand;
                var reservedQty = alloc?.ReservedQty ?? 0m;
                var issuedQty = alloc?.IssuedQty ?? 0m;
                var unfulfilled = Math.Max(0m, Math.Round(effectiveGrossDemand - (reservedQty + issuedQty), 4));

                if (unfulfilled > 0m)
                {
                    itemShortage = true;
                }

                if (!materialGroups.TryGetValue(line.MaterialId, out var entry))
                {
                    entry = (line.Material, new List<MrpOrderItemBreakdownDto>());
                    materialGroups[line.MaterialId] = entry;
                }

                entry.Breakdowns.Add(new MrpOrderItemBreakdownDto
                {
                    OrderId = item.OrderId,
                    PoNumber = item.CustomerOrder?.PoNumber ?? string.Empty,
                    SpaceId = item.CustomerOrder?.OrderSpace?.SpaceId,
                    SpaceKey = item.CustomerOrder?.OrderSpace?.SpaceKey ?? $"SPACE-ORDER-{item.OrderId}",
                    OrderItemId = item.OrderItemId,
                    ProductId = item.ProductId,
                    ProductSku = item.Product?.ProductSku ?? string.Empty,
                    ProductName = item.Product?.ProductName ?? string.Empty,
                    OrderedQty = item.OrderedQty,
                    ScrapRatePercent = bom.ScrapRatePercent,
                    NetQtyPerUnit = line.NetQtyPerUnit,
                    GrossDemandQty = effectiveGrossDemand,
                    ReservedQty = reservedQty,
                    IssuedQty = issuedQty,
                    UnfulfilledDemandQty = unfulfilled,
                    EtdDate = item.CustomerOrder?.EtdDate ?? DateTime.UtcNow,
                    OrderItemStatus = item.Status.ToString()
                });
            }

            if (itemShortage)
            {
                waitingItems++;
            }
            else
            {
                readyItems++;
            }
        }

        var materialLines = new List<MrpMaterialRequirementLineDto>();
        foreach (var kvp in materialGroups.OrderBy(k => k.Key))
        {
            var mat = kvp.Value.Material;
            var breakdowns = kvp.Value.Breakdowns
                .OrderBy(b => b.EtdDate)
                .ThenBy(b => b.OrderItemId)
                .ToList();

            var totalNetBomQty = Math.Round(breakdowns.Sum(b => b.NetQtyPerUnit * b.OrderedQty), 4);
            var totalGrossDemand = Math.Round(breakdowns.Sum(b => b.GrossDemandQty), 4);
            var alreadyReservedForTarget = Math.Round(breakdowns.Sum(b => b.ReservedQty + b.IssuedQty), 4);

            var physical = mat.MaterialStock?.PhysicalQty ?? 0m;
            var totalAllocated = mat.MaterialStock?.AllocatedQty ?? 0m;
            var available = mat.MaterialStock != null
                ? mat.MaterialStock.AvailableQty
                : Math.Max(0m, Math.Round(physical - totalAllocated, 4));

            // BR-INV-01: Tồn kho đang giữ chỗ (ReservedQty) của các đơn hàng khác (không trừ IssuedQty đã xuất kho khỏi TotalAllocated)
            var reservedOnlyForTarget = Math.Round(breakdowns.Sum(b => b.ReservedQty), 4);
            var allocatedToOtherOrders = Math.Max(0m, Math.Round(totalAllocated - reservedOnlyForTarget, 4));

            var stillNeededForTarget = Math.Max(0m, Math.Round(totalGrossDemand - alreadyReservedForTarget, 4));
            var allocatableNow = Math.Min(stillNeededForTarget, available);
            var netShortage = Math.Max(0m, Math.Round(stillNeededForTarget - allocatableNow, 4));

            var projectedRemainingAvailable = Math.Max(0m, Math.Round(available - allocatableNow, 4));
            var safetyDeficit = Math.Max(0m, Math.Round(mat.SafetyStockQty - projectedRemainingAvailable, 4));
            var suggestedPrOrderQty = netShortage > 0m
                ? Math.Round(netShortage + safetyDeficit, 4)
                : 0m;

            var (supplyStatus, supplyStatusLabel) =
                totalGrossDemand > 0m && alreadyReservedForTarget >= totalGrossDemand
                    ? ("RESERVED", "Đã giữ chỗ")
                    : netShortage <= 0m
                        ? ("SUFFICIENT", "Đủ tồn kho")
                        : alreadyReservedForTarget + allocatableNow > 0m
                            ? ("PARTIAL_SHORTAGE", "Chưa đủ nguyên liệu")
                            : ("CRITICAL_SHORTAGE", "Chưa đủ nguyên liệu");

            materialLines.Add(new MrpMaterialRequirementLineDto
            {
                MaterialId = mat.MaterialId,
                MaterialCode = mat.MaterialCode,
                MaterialName = mat.MaterialName,
                MaterialGroup = mat.MaterialGroup.ToString(),
                UnitOfMeasure = mat.UnitOfMeasure,
                DimensionSpec = mat.DimensionSpec,
                FscCertified = mat.FscCertified,
                SafetyStockQty = mat.SafetyStockQty,
                TotalNetBomQty = totalNetBomQty,
                TotalGrossDemandQty = totalGrossDemand,
                PhysicalStockQty = physical,
                AllocatedToOtherOrdersQty = allocatedToOtherOrders,
                AlreadyReservedForTargetQty = alreadyReservedForTarget,
                AvailableStockQty = available,
                AllocatableNowQty = allocatableNow,
                NetShortageQty = netShortage,
                SuggestedPrOrderQty = suggestedPrOrderQty,
                SupplyStatus = supplyStatus,
                SupplyStatusLabel = supplyStatusLabel,
                ItemBreakdowns = breakdowns
            });
        }

        int shortageMaterialsCount = materialLines.Count(m => m.NetShortageQty > 0m);
        int sufficientMaterialsCount = materialLines.Count - shortageMaterialsCount;

        // Xác định lại số dòng OrderItem có thể đáp ứng đủ nếu chạy Auto-Allocation ngay lúc này
        bool canAllBeFulfilled = orderItems.Count > 0 &&
                                 orderItems.All(oi => oi.BomHeader != null && oi.BomHeader.BomDetails.Count > 0) &&
                                 shortageMaterialsCount == 0;

        return new MrpAnalysisResultDto
        {
            Scope = scope,
            OrderId = order?.OrderId,
            PoNumber = order?.PoNumber,
            SpaceId = space?.SpaceId ?? order?.OrderSpace?.SpaceId,
            SpaceKey = space?.SpaceKey ?? order?.OrderSpace?.SpaceKey,
            CustomerName = order?.Customer?.CompanyName,
            EtdDate = order?.EtdDate,
            IsEtdAtRisk = order != null && order.IsEtdAtRisk(etdWarningDays),
            AnalyzedAt = DateTime.UtcNow,
            TotalOrderItemsCount = orderItems.Count,
            ReadyOrderItemsCount = readyItems,
            WaitingMaterialItemsCount = waitingItems,
            TotalDistinctMaterialsCount = materialLines.Count,
            SufficientMaterialsCount = sufficientMaterialsCount,
            ShortageMaterialsCount = shortageMaterialsCount,
            OverallReadinessStatus = canAllBeFulfilled ? "READY_TO_PRODUCE" : "WAITING_FOR_MATERIAL",
            MaterialRequirements = materialLines
        };
    }

    /// <summary>
    /// Tải lượng thiếu hụt (<c>ShortageQty</c>) theo từng cặp (VậtTư, OrderItem) đang nằm trong các PR trạng thái APPROVED (đã duyệt, chờ phát hành PO)
    /// để chống mua trùng (BR-PUR-01). Không tính PR PO_ISSUED lịch sử vì PO có thể đã nhập/xuất kho xong hoặc đã bị hủy.
    /// Dùng chung cho luồng gom PR tự động và luồng cấp bù phế phẩm (SC-14b).
    /// </summary>
    private async Task<Dictionary<(long MaterialId, long OrderItemId), decimal>> LoadApprovedPrCoverageAsync(CancellationToken cancellationToken)
    {
        var approvedPrs = await _purchaseRequestRepository.GetAllAsync(PrStatusEnum.APPROVED, cancellationToken);
        var covered = new Dictionary<(long MaterialId, long OrderItemId), decimal>();

        foreach (var pr in approvedPrs)
        {
            foreach (var prItem in pr.Items)
            {
                if (string.IsNullOrWhiteSpace(prItem.SourceOrderIdsJson)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(prItem.SourceOrderIdsJson);
                    if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.TryGetProperty("OrderItemId", out var oidProp) && oidProp.TryGetInt64(out var oid))
                        {
                            decimal qty = 0m;
                            if (el.TryGetProperty("ShortageQty", out var sqProp) && sqProp.TryGetDecimal(out var sq))
                            {
                                qty = sq;
                            }
                            var key = (prItem.MaterialId, oid);
                            covered[key] = covered.GetValueOrDefault(key, 0m) + qty;
                        }
                    }
                }
                catch
                {
                    // Bỏ qua JSON không chuẩn định dạng
                }
            }
        }

        return covered;
    }

    private static bool IsOrderItemFullyAllocated(OrderItem item)
    {
        if (item.BomHeader == null || item.BomHeader.BomDetails.Count == 0)
        {
            return false;
        }

        var requiredByMaterial = item.BomHeader.BomDetails
            .GroupBy(bd => bd.MaterialId)
            .Select(g => new
            {
                MaterialId = g.Key,
                RequiredGross = Math.Round(g.Sum(x => ResolveGrossDemand(x, item.OrderedQty, item.BomHeader.ScrapRatePercent)), 4)
            });

        foreach (var req in requiredByMaterial)
        {
            var alloc = item.MaterialAllocations
                .FirstOrDefault(ma => ma.MaterialId == req.MaterialId && ma.AllocationStatus != AllocationStatusEnum.RELEASED);

            var fulfilled = alloc != null ? alloc.ReservedQty + alloc.IssuedQty : 0m;
            // Nhu cầu hiệu lực = max(nhu cầu BOM gốc, RequiredQty đã cộng dồn từ cấp bù phế phẩm) để không bỏ sót phần cấp bù (C2)
            var effectiveRequired = alloc != null ? Math.Max(req.RequiredGross, alloc.RequiredQty) : req.RequiredGross;
            if (fulfilled + 0.0001m < effectiveRequired)
            {
                return false;
            }
        }

        return true;
    }

    private static decimal ResolveGrossDemand(BomDetail detail, int orderedQty, decimal scrapRatePercent)
    {
        if (detail.GrossTotalDemand > 0m)
        {
            return Math.Round(detail.GrossTotalDemand, 4);
        }

        return CalculateDemandForQuantity(detail, orderedQty, scrapRatePercent);
    }

    /// <summary>
    /// Tính trực tiếp nhu cầu vật tư gộp (có hao hụt BOM) cho một số lượng sản phẩm cụ thể (dùng cho cấp bù phế phẩm ScrappedQty, không lấy nhầm GrossTotalDemand của cả đơn).
    /// </summary>
    private static decimal CalculateDemandForQuantity(BomDetail detail, int quantity, decimal scrapRatePercent)
    {
        if (quantity <= 0) return 0m;
        return Math.Round((detail.NetQtyPerUnit * quantity) * (1m + (scrapRatePercent / 100m)), 4);
    }

    private static MaterialOrderAllocationDto MapAllocationDto(MaterialAllocation a, OrderItem item)
    {
        var co = item.CustomerOrder;
        return new MaterialOrderAllocationDto
        {
            AllocationId = a.AllocationId,
            OrderId = item.OrderId,
            PoNumber = co?.PoNumber ?? string.Empty,
            SpaceKey = co?.OrderSpace?.SpaceKey ?? $"SPACE-ORDER-{item.OrderId}",
            CustomerName = co?.Customer?.CompanyName ?? string.Empty,
            OrderItemId = item.OrderItemId,
            ProductSku = item.Product?.ProductSku ?? string.Empty,
            ProductName = item.Product?.ProductName ?? string.Empty,
            MaterialId = a.MaterialId,
            MaterialCode = a.Material?.MaterialCode ?? string.Empty,
            MaterialName = a.Material?.MaterialName ?? string.Empty,
            UnitOfMeasure = a.Material?.UnitOfMeasure ?? string.Empty,
            RequiredQty = a.RequiredQty,
            ReservedQty = a.ReservedQty,
            IssuedQty = a.IssuedQty,
            ShortageQty = Math.Max(0m, Math.Round(a.RequiredQty - (a.ReservedQty + a.IssuedQty), 4)),
            AllocationStatus = a.AllocationStatus.ToString(),
            EtdDate = co?.EtdDate ?? DateTime.UtcNow,
            AllocatedAt = a.AllocatedAt
        };
    }
}
