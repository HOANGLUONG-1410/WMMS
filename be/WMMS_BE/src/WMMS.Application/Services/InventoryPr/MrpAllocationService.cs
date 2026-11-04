using System.Text.Json;
using WMMS.Application.DTOs.InventoryPr;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;
using WMMS.Domain.Entities.CatalogBom;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Entities.InventoryPr;
using WMMS.Domain.Entities.Orders;
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
            ?? throw AppException.NotFound($"Không tìm thấy đơn hàng với ID = {orderId}.");

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
            ?? throw AppException.NotFound($"Không tìm thấy Không gian đơn hàng (OrderSpace) với ID = {spaceId}.");

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
            ?? throw AppException.NotFound($"Không tìm thấy đơn hàng với ID = {orderId}.");

        var orderItems = await _orderItemRepository.GetByOrderIdWithFullMrpGraphAsync(orderId, cancellationToken);
        if (orderItems.Count == 0)
        {
            throw new AppException($"Đơn hàng {order.PoNumber} chưa có dòng sản phẩm (OrderItem) nào để phân bổ tồn kho.");
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
            ?? throw AppException.NotFound($"Không tìm thấy Không gian đơn hàng (OrderSpace) với ID = {spaceId}.");

        var orderItems = await _orderItemRepository.GetBySpaceIdWithFullMrpGraphAsync(spaceId, cancellationToken);
        if (orderItems.Count == 0)
        {
            throw new AppException($"Không gian đơn hàng {space.SpaceKey} chưa có dòng sản phẩm nào để phân bổ tồn kho.");
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
            ?? throw AppException.NotFound($"Không tìm thấy dòng sản phẩm đơn hàng (OrderItem) với ID = {orderItemId}.");

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

                foreach (var detail in item.BomHeader.BomDetails)
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

                    var requiredGross = ResolveGrossDemand(detail, item.OrderedQty, item.BomHeader.ScrapRatePercent);
                    var existingAlloc = item.MaterialAllocations
                        .FirstOrDefault(ma => ma.MaterialId == detail.MaterialId && ma.AllocationStatus != AllocationStatusEnum.RELEASED);

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
                    Reason = $"Tự động phân bổ lại tồn kho khả dụng theo thứ tự ưu tiên ETD gần nhất (FIFO by ETD, SC-14/SC-17). Đã mở khóa {unlockedOrderItemIds.Count} dòng đơn hàng sang READY_PROD.",
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

        var materialIds = activeReserved.Select(a => a.MaterialId).Distinct().ToList();
        var stockMap = await _materialStockRepository.GetByMaterialIdsAsync(materialIds, cancellationToken);
        var now = DateTime.UtcNow;
        var releasedDtos = new List<MaterialOrderAllocationDto>();

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            foreach (var alloc in activeReserved)
            {
                var qtyToRelease = alloc.ReservedQty;
                if (stockMap.TryGetValue(alloc.MaterialId, out var stock))
                {
                    stock.ReleaseAllocation(qtyToRelease);
                    await _materialStockRepository.UpdateAsync(stock);
                }

                alloc.ReservedQty = 0m;
                alloc.AllocationStatus = AllocationStatusEnum.RELEASED;
                await _materialAllocationRepository.UpdateAsync(alloc);

                releasedDtos.Add(MapAllocationDto(alloc, alloc.OrderItem));
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
            .Where(oi => oi.BomHeader != null && oi.BomHeader.BomDetails.Count > 0)
            .OrderBy(oi => oi.CustomerOrder.EtdDate)
            .ThenBy(oi => oi.OrderItemId)
            .ToList();

        if (itemsWithBom.Count == 0)
        {
            throw new AppException("Chưa có định mức nguyên vật liệu (BOM) nào được thiết lập cho đơn hàng này để chạy tính toán MRP & Phân bổ tồn kho tự động (SC-14).");
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

                    var stillNeeded = Math.Max(0m, Math.Round(line.TotalGrossDemand - alreadyReservedOrIssued, 4));
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
                            RequiredQty = line.TotalGrossDemand,
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
                        existingAlloc.RequiredQty = line.TotalGrossDemand;
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
                Reason = $"Thực thi tính toán MRP & Phân bổ tồn kho tự động (SC-14): Trạng thái = {overallReadiness}, Đủ vật tư = {fullyAllocatedItemsCount}/{itemsWithBom.Count} dòng" +
                         (consolidatedPrCode != null ? $", Đã gom thiếu hụt vào PR tổng hợp {consolidatedPrCode}." : "."),
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

    private async Task<(long PrId, string PrCode, int QueuedMaterialsCount)> UpsertDailyConsolidatedPrAsync(
        List<MrpMaterialRequirementLineDto> shortageLines,
        long currentUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var today = now.Date;
        var dailyPr = await _purchaseRequestRepository.GetPendingPrByDateAsync(today, cancellationToken);

        if (dailyPr == null)
        {
            var countToday = await _purchaseRequestRepository.CountByDateAsync(today, cancellationToken);
            var prCode = $"PR-{today:yyyyMMdd}-{countToday + 1:D3}";

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

        foreach (var line in shortageLines)
        {
            var existingItem = dailyPr.Items.FirstOrDefault(i => i.MaterialId == line.MaterialId);

            // Hợp nhất danh sách đơn hàng nguồn gây thiếu hụt vào SourceOrderIdsJson để truy vết nhiều-nhiều (BR-PUR-01)
            var sourceOrders = line.ItemBreakdowns
                .Where(b => b.UnfulfilledDemandQty > 0m)
                .Select(b => new
                {
                    b.OrderId,
                    b.PoNumber,
                    b.SpaceKey,
                    b.OrderItemId,
                    b.ProductSku,
                    EtdDate = b.EtdDate.ToString("yyyy-MM-dd"),
                    ShortageQty = b.UnfulfilledDemandQty
                })
                .ToList();

            var sourceJson = JsonSerializer.Serialize(sourceOrders);

            if (existingItem == null)
            {
                var newItem = new PurchaseRequestItem
                {
                    PrId = dailyPr.PrId,
                    MaterialId = line.MaterialId,
                    TotalShortageQty = line.NetShortageQty,
                    SuggestedOrderQty = line.SuggestedPrOrderQty,
                    SourceOrderIdsJson = sourceJson
                };
                dailyPr.Items.Add(newItem);
                await _purchaseRequestRepository.AddItemAsync(newItem, cancellationToken);
            }
            else
            {
                // Cập nhật số lượng thiếu hụt tổng hợp mới nhất
                existingItem.TotalShortageQty = Math.Max(existingItem.TotalShortageQty, line.NetShortageQty);
                existingItem.SuggestedOrderQty = Math.Max(existingItem.SuggestedOrderQty, line.SuggestedPrOrderQty);
                existingItem.SourceOrderIdsJson = MergeSourceOrderJson(existingItem.SourceOrderIdsJson, sourceJson);
            }
        }

        dailyPr.UpdatedAt = now;
        await _purchaseRequestRepository.UpdateAsync(dailyPr);

        return (dailyPr.PrId, dailyPr.PrCode, shortageLines.Count);
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

                var reservedQty = alloc?.ReservedQty ?? 0m;
                var issuedQty = alloc?.IssuedQty ?? 0m;
                var unfulfilled = Math.Max(0m, Math.Round(line.GrossDemand - (reservedQty + issuedQty), 4));

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
                    GrossDemandQty = line.GrossDemand,
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

            // BR-INV-01: Tồn kho đã giữ chỗ của các đơn hàng khác (bắt buộc loại trừ)
            var allocatedToOtherOrders = Math.Max(0m, Math.Round(totalAllocated - alreadyReservedForTarget, 4));

            var stillNeededForTarget = Math.Max(0m, Math.Round(totalGrossDemand - alreadyReservedForTarget, 4));
            var allocatableNow = Math.Min(stillNeededForTarget, available);
            var netShortage = Math.Max(0m, Math.Round(stillNeededForTarget - allocatableNow, 4));

            var projectedRemainingAvailable = Math.Max(0m, Math.Round(available - allocatableNow, 4));
            var safetyDeficit = Math.Max(0m, Math.Round(mat.SafetyStockQty - projectedRemainingAvailable, 4));
            var suggestedPrOrderQty = netShortage > 0m
                ? Math.Round(netShortage + safetyDeficit, 4)
                : 0m;

            string supplyStatus = netShortage <= 0m
                ? "SUFFICIENT"
                : (alreadyReservedForTarget + allocatableNow > 0m ? "PARTIAL_SHORTAGE" : "CRITICAL_SHORTAGE");

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
            if (fulfilled + 0.0001m < req.RequiredGross)
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

        return Math.Round((detail.NetQtyPerUnit * orderedQty) * (1m + (scrapRatePercent / 100m)), 4);
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
