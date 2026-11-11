using Microsoft.EntityFrameworkCore;
using WMMS.Application.DTOs.Warehouse;
using WMMS.Application.Interfaces.External;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;
using WMMS.Domain.Entities.CatalogBom;
using WMMS.Domain.Entities.InventoryPr;
using WMMS.Domain.Entities.WarehousePo;
using WMMS.Domain.Enums;
using WMMS.Domain.Exceptions;
using WMMS.Infrastructure.Persistence;
using WMMS.Infrastructure.Services.MasterData;

namespace WMMS.Infrastructure.Services.Warehouse;

/// <summary>
/// Nhập kho theo đơn mua hàng PO (GRN) & Phân bổ tự động vật tư mới về vào Staging (SC-17, UC-56, UC-57, BR-005, BR-WHS-01).
/// Toàn bộ nghiệp vụ chạy trong MỘT giao dịch nguyên tử; xung đột RowVersion kho được thử lại tối đa 3 lần (UC057.E1).
/// </summary>
public class GoodsReceiptService : IGoodsReceiptService
{
    /// <summary>Ngưỡng cho phép giao vượt đơn PO (UC056.E1: &gt; 110% phải được Purchasing xác nhận).</summary>
    public const decimal OverDeliveryTolerance = 1.10m;

    private const int MaxConcurrencyRetries = 3;

    private readonly WmmsDbContext _context;
    private readonly INotificationHubService? _notificationHubService;

    public GoodsReceiptService(
        WmmsDbContext context,
        INotificationHubService? notificationHubService = null)
    {
        _context = context;
        _notificationHubService = notificationHubService;
    }

    /// <inheritdoc />
    public async Task<GoodsReceiptResultDto> CommitGoodsReceiptAsync(
        CommitGoodsReceiptRequestDto request,
        long warehouseKeeperId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        for (var attempt = 1; ; attempt++)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await ExecuteCommitAsync(request, warehouseKeeperId, ipAddress, cancellationToken);
                await tx.CommitAsync(cancellationToken);

                if (_notificationHubService != null)
                {
                    try
                    {
                        await _notificationHubService.NotifyGoodsReceiptCommittedAsync(
                            result.GrnCode,
                            result.PoCode,
                            result.AllocatedOrderCount,
                            result.ReadyOrderPoNumbers,
                            cancellationToken);
                    }
                    catch
                    {
                        // SignalR notification errors should not abort a successfully committed database transaction
                    }
                }

                return result;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                await tx.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                throw AppException.Conflict("Tồn kho đang được cập nhật đồng thời bởi người dùng khác. Vui lòng thử lại.");
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                throw;
            }
        }
    }

    private async Task<GoodsReceiptResultDto> ExecuteCommitAsync(
        CommitGoodsReceiptRequestDto request,
        long warehouseKeeperId,
        string? ipAddress,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // 1. Đối chiếu với đơn PO
        var po = await _context.PurchaseOrders
            .Include(p => p.Details).ThenInclude(d => d.Material)
            .FirstOrDefaultAsync(p => p.PoId == request.PoId, ct)
            ?? throw AppException.NotFound($"Không tìm thấy đơn mua hàng PO ID = {request.PoId}.");

        if (po.PoStatus != PoStatusEnum.ISSUED && po.PoStatus != PoStatusEnum.PARTIALLY_RECEIVED)
        {
            throw AppException.Conflict(
                $"Đơn PO '{po.PoCode}' đang ở trạng thái {po.PoStatus}, chỉ nhận hàng khi ISSUED hoặc PARTIALLY_RECEIVED (BR-WHS-01).");
        }

        var lines = request.Lines
            .Where(l => l.ReceivedQty > 0)
            .GroupBy(l => l.MaterialId)
            .Select(g => (MaterialId: g.Key, Qty: g.Sum(x => x.ReceivedQty)))
            .ToList();

        var lineResults = new List<GoodsReceiptLineResultDto>();
        foreach (var (materialId, qty) in lines)
        {
            var detail = po.Details.FirstOrDefault(d => d.MaterialId == materialId)
                ?? throw new AppException($"Vật tư ID = {materialId} không thuộc đơn PO '{po.PoCode}'.");

            var total = detail.ReceivedQty + qty;
            if (total > detail.OrderedQty * OverDeliveryTolerance)
            {
                throw new AppException(
                    $"Delivered quantity exceeds PO threshold. Requires Purchasing confirmation " +
                    $"(vật tư {detail.Material.MaterialCode}: đặt {detail.OrderedQty}, đã nhận {detail.ReceivedQty}, lần này {qty}).");
            }
        }

        // 2. Tạo phiếu GRN
        var grn = new GoodsReceipt
        {
            GrnCode = $"GRN-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            PoId = po.PoId,
            WarehouseKeeperId = warehouseKeeperId,
            CommittedAt = now,
            Status = GrnStatusEnum.COMMITTED
        };
        _context.GoodsReceipts.Add(grn);

        // 3. Cộng tồn vật lý + ghi sổ kho INBOUND + cập nhật ReceivedQty của PO
        var stocks = new Dictionary<long, MaterialStock>();
        foreach (var (materialId, qty) in lines)
        {
            var detail = po.Details.First(d => d.MaterialId == materialId);
            var previous = detail.ReceivedQty;
            detail.ReceivedQty += qty;

            var stock = await _context.MaterialStocks.FirstOrDefaultAsync(s => s.MaterialId == materialId, ct);
            if (stock == null)
            {
                stock = new MaterialStock { MaterialId = materialId, UpdatedAt = now };
                _context.MaterialStocks.Add(stock);
            }
            stock.PhysicalQty += qty;
            stock.AvailableQty = Math.Max(0m, stock.PhysicalQty - stock.AllocatedQty);
            stock.UpdatedAt = now;
            stocks[materialId] = stock;

            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                MaterialId = materialId,
                TxType = InventoryTransactionTypeEnum.IN_RECEIPT,
                Quantity = qty,
                ReferenceCode = grn.GrnCode,
                CreatedAt = now
            });

            lineResults.Add(new GoodsReceiptLineResultDto
            {
                MaterialId = materialId,
                MaterialCode = detail.Material.MaterialCode,
                MaterialName = detail.Material.MaterialName,
                UnitOfMeasure = detail.Material.UnitOfMeasure,
                OrderedQty = detail.OrderedQty,
                PreviouslyReceivedQty = previous,
                ReceivedQty = qty,
                TotalReceivedQty = detail.ReceivedQty,
                RemainingQty = Math.Max(0m, detail.OrderedQty - detail.ReceivedQty)
            });
        }

        var beforePoStatus = po.PoStatus;
        po.PoStatus = po.Details.All(d => d.ReceivedQty >= d.OrderedQty)
            ? PoStatusEnum.FULFILLED
            : PoStatusEnum.PARTIALLY_RECEIVED;
        po.UpdatedAt = now;

        await _context.SaveChangesAsync(ct);

        // 4. Auto-Allocation theo ETD tăng dần (BR-005, BR-WHS-01)
        var allocationResults = new List<AutoAllocationResultDto>();
        foreach (var (materialId, _) in lines)
        {
            var stock = stocks[materialId];
            var material = po.Details.First(d => d.MaterialId == materialId).Material;
            allocationResults.AddRange(await AllocateWaitingDemandAsync(stock, material, now, ct));
        }

        var readyPoNumbers = new List<string>();
        if (allocationResults.Count > 0)
        {
            grn.Status = GrnStatusEnum.AUTO_ALLOCATED;
            await _context.SaveChangesAsync(ct);
            readyPoNumbers = await PromoteReadyOrdersAsync(
                allocationResults.Select(a => a.OrderItemId).Distinct().ToList(), ct);
        }

        foreach (var lr in lineResults)
        {
            var s = stocks[lr.MaterialId];
            lr.PhysicalQtyAfter = s.PhysicalQty;
            lr.AllocatedQtyAfter = s.AllocatedQty;
            lr.AvailableQtyAfter = s.AvailableQty;
        }

        // 5. Audit trail (GBR-05)
        MasterDataAudit.Add(_context, warehouseKeeperId, AuditActionConstants.CommitGrn, "GoodsReceipt",
            grn.GrnId.ToString(),
            new { PoStatus = beforePoStatus.ToString() },
            new
            {
                grn.GrnCode,
                PoCode = po.PoCode,
                PoStatus = po.PoStatus.ToString(),
                GrnStatus = grn.Status.ToString(),
                Lines = lineResults.Select(l => new { l.MaterialCode, l.ReceivedQty }),
                AllocatedOrders = allocationResults.Count
            },
            ipAddress);
        await _context.SaveChangesAsync(ct);

        return new GoodsReceiptResultDto
        {
            GrnId = grn.GrnId,
            GrnCode = grn.GrnCode,
            PoId = po.PoId,
            PoCode = po.PoCode,
            PoStatus = po.PoStatus.ToString(),
            GrnStatus = grn.Status.ToString(),
            CommittedAt = grn.CommittedAt,
            Lines = lineResults,
            Allocations = allocationResults,
            AllocatedOrderCount = allocationResults.Select(a => a.OrderItemId).Distinct().Count(),
            ReadyOrderPoNumbers = readyPoNumbers
        };
    }

    /// <summary>
    /// Gom nhu cầu đang chờ vật tư (MaterialAllocation còn thiếu + dòng BOM WAITING_FOR_MATERIALS chưa có allocation),
    /// sắp xếp ETD ASC rồi CreatedAt ASC và phân bổ phần tồn khả dụng mới về.
    /// </summary>
    private async Task<List<AutoAllocationResultDto>> AllocateWaitingDemandAsync(
        MaterialStock stock, Material material, DateTime now, CancellationToken ct)
    {
        var materialId = material.MaterialId;

        var waiting = await _context.MaterialAllocations
            .Include(a => a.OrderItem).ThenInclude(oi => oi.CustomerOrder).ThenInclude(co => co.OrderSpace)
            .Where(a => a.MaterialId == materialId
                        && a.AllocationStatus == AllocationStatusEnum.RESERVED
                        && a.RequiredQty > a.ReservedQty
                        && a.OrderItem.CustomerOrder.Status != OrderStatusEnum.CANCELLED)
            .ToListAsync(ct);

        var coveredItemIds = (await _context.MaterialAllocations
                .Where(a => a.MaterialId == materialId)
                .Select(a => a.OrderItemId)
                .ToListAsync(ct))
            .ToHashSet();

        var bomDemand = await _context.BomDetails
            .Include(bd => bd.BomHeader).ThenInclude(bh => bh.OrderItem)
                .ThenInclude(oi => oi.CustomerOrder).ThenInclude(co => co.OrderSpace)
            .Where(bd => bd.MaterialId == materialId
                         && (bd.BomHeader.BomStatus == BomStatusEnum.WAITING_FOR_MATERIALS
                             || bd.BomHeader.OrderItem.Status == OrderItemStatusEnum.WAITING_MAT)
                         && bd.BomHeader.OrderItem.CustomerOrder.Status != OrderStatusEnum.CANCELLED)
            .ToListAsync(ct);

        foreach (var bd in bomDemand.Where(bd => !coveredItemIds.Contains(bd.BomHeader.OrderItemId)))
        {
            var created = new MaterialAllocation
            {
                OrderItemId = bd.BomHeader.OrderItemId,
                OrderItem = bd.BomHeader.OrderItem,
                MaterialId = materialId,
                RequiredQty = bd.GrossTotalDemand,
                ReservedQty = 0m,
                AllocationStatus = AllocationStatusEnum.RESERVED,
                AllocatedAt = now
            };
            _context.MaterialAllocations.Add(created);
            waiting.Add(created);

            // Đồng bộ khởi tạo MaterialAllocation cho TOÀN BỘ các dòng vật tư khác trong cùng BOM của OrderItem này
            // để bảo đảm không bị thiếu hụt dữ liệu phân bổ của các vật tư đi kèm
            var otherBomDetails = await _context.BomDetails
                .Where(other => other.BomId == bd.BomId && other.MaterialId != materialId)
                .ToListAsync(ct);

            foreach (var other in otherBomDetails)
            {
                var existingOtherAlloc = await _context.MaterialAllocations
                    .FirstOrDefaultAsync(a => a.OrderItemId == bd.BomHeader.OrderItemId && a.MaterialId == other.MaterialId, ct);

                if (existingOtherAlloc == null)
                {
                    var otherStock = await _context.MaterialStocks.FirstOrDefaultAsync(s => s.MaterialId == other.MaterialId, ct);
                    decimal otherReserved = 0m;
                    if (otherStock != null && otherStock.AvailableQty > 0m)
                    {
                        otherReserved = Math.Min(otherStock.AvailableQty, other.GrossTotalDemand);
                        otherStock.AllocatedQty += otherReserved;
                        otherStock.AvailableQty = Math.Max(0m, otherStock.PhysicalQty - otherStock.AllocatedQty);
                        otherStock.UpdatedAt = now;
                    }

                    var otherCreated = new MaterialAllocation
                    {
                        OrderItemId = bd.BomHeader.OrderItemId,
                        OrderItem = bd.BomHeader.OrderItem,
                        MaterialId = other.MaterialId,
                        RequiredQty = other.GrossTotalDemand,
                        ReservedQty = otherReserved,
                        AllocationStatus = AllocationStatusEnum.RESERVED,
                        AllocatedAt = now
                    };
                    _context.MaterialAllocations.Add(otherCreated);
                }
            }
        }

        var ordered = waiting
            .OrderBy(a => a.OrderItem.CustomerOrder.EtdDate)
            .ThenBy(a => a.OrderItem.CustomerOrder.CreatedAt)
            .ThenBy(a => a.OrderItem.OrderItemId)
            .ToList();

        var results = new List<AutoAllocationResultDto>();
        var available = stock.AvailableQty;
        foreach (var alloc in ordered)
        {
            if (available <= 0m) break;

            var shortage = alloc.RequiredQty - alloc.ReservedQty;
            var give = Math.Min(available, shortage);
            if (give <= 0m) continue;

            alloc.ReservedQty += give;
            alloc.AllocatedAt = now;
            available -= give;
            stock.AllocatedQty += give;

            var co = alloc.OrderItem.CustomerOrder;
            results.Add(new AutoAllocationResultDto
            {
                AllocationId = alloc.AllocationId,
                OrderId = co.OrderId,
                PoNumber = co.PoNumber,
                SpaceKey = co.OrderSpace?.SpaceKey ?? string.Empty,
                EtdDate = co.EtdDate,
                OrderItemId = alloc.OrderItemId,
                MaterialId = materialId,
                MaterialCode = material.MaterialCode,
                AllocatedNowQty = give,
                RequiredQty = alloc.RequiredQty,
                ReservedQty = alloc.ReservedQty,
                ShortageQty = Math.Max(0m, alloc.RequiredQty - alloc.ReservedQty)
            });
        }

        if (results.Count > 0)
        {
            stock.AvailableQty = Math.Max(0m, stock.PhysicalQty - stock.AllocatedQty);
            stock.UpdatedAt = now;
        }

        return results;
    }

    /// <summary>Đơn hàng đủ 100% vật tư theo BOM → BOM = READY_FOR_PRODUCTION, OrderItem = READY_PROD.</summary>
    private async Task<List<string>> PromoteReadyOrdersAsync(List<long> orderItemIds, CancellationToken ct)
    {
        var ready = new List<string>();

        var boms = await _context.BomHeaders
            .Include(b => b.BomDetails)
            .Include(b => b.OrderItem).ThenInclude(oi => oi.CustomerOrder)
            .Where(b => orderItemIds.Contains(b.OrderItemId) 
                        && (b.BomStatus == BomStatusEnum.WAITING_FOR_MATERIALS || b.OrderItem.Status == OrderItemStatusEnum.WAITING_MAT))
            .ToListAsync(ct);

        foreach (var bom in boms)
        {
            var allocations = await _context.MaterialAllocations
                .Where(a => a.OrderItemId == bom.OrderItemId)
                .ToListAsync(ct);

            var fullyCovered = bom.BomDetails.Count > 0 && bom.BomDetails.All(bd =>
                allocations.Any(a => a.MaterialId == bd.MaterialId && a.ReservedQty >= bd.GrossTotalDemand));

            if (!fullyCovered) continue;

            bom.BomStatus = BomStatusEnum.READY_FOR_PRODUCTION;
            bom.UpdatedAt = DateTime.UtcNow;
            bom.OrderItem.Status = OrderItemStatusEnum.READY_PROD;
            ready.Add(bom.OrderItem.CustomerOrder.PoNumber);
        }

        if (ready.Count > 0)
        {
            await _context.SaveChangesAsync(ct);
        }
        return ready.Distinct().ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OpenPurchaseOrderDto>> GetOpenPurchaseOrdersAsync(
        string? search, CancellationToken cancellationToken = default)
    {
        var query = _context.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Details).ThenInclude(d => d.Material)
            .Where(p => p.PoStatus == PoStatusEnum.ISSUED || p.PoStatus == PoStatusEnum.PARTIALLY_RECEIVED);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim().ToLower();
            query = query.Where(p => p.PoCode.ToLower().Contains(kw) || p.Supplier.SupplierName.ToLower().Contains(kw));
        }

        var pos = await query.OrderBy(p => p.ExpectedArrivalDate).ToListAsync(cancellationToken);

        return pos.Select(p => new OpenPurchaseOrderDto
        {
            PoId = p.PoId,
            PoCode = p.PoCode,
            SupplierName = p.Supplier.SupplierName,
            PoStatus = p.PoStatus.ToString(),
            IssueDate = p.IssueDate,
            ExpectedArrivalDate = p.ExpectedArrivalDate,
            Lines = p.Details.Select(d => new OpenPurchaseOrderLineDto
            {
                MaterialId = d.MaterialId,
                MaterialCode = d.Material.MaterialCode,
                MaterialName = d.Material.MaterialName,
                UnitOfMeasure = d.Material.UnitOfMeasure,
                OrderedQty = d.OrderedQty,
                ReceivedQty = d.ReceivedQty,
                RemainingQty = Math.Max(0m, d.OrderedQty - d.ReceivedQty)
            }).ToList()
        }).ToList();
    }

    private static void ValidateRequest(CommitGoodsReceiptRequestDto request)
    {
        if (request.PoId <= 0)
        {
            throw new AppException("PoId là bắt buộc (MSG01).", messageCode: MessageCodes.Msg01RequiredField);
        }
        if (request.Lines == null || request.Lines.Count == 0)
        {
            throw new AppException("Phiếu nhập kho phải có ít nhất một dòng vật tư (MSG01).", messageCode: MessageCodes.Msg01RequiredField);
        }
        if (request.Lines.Any(l => l.ReceivedQty < 0))
        {
            throw new AppException("Received quantity cannot be negative or blank. (MSG-WHS-02)");
        }
        if (request.Lines.All(l => l.ReceivedQty == 0))
        {
            throw new AppException("Phải có ít nhất một dòng thực nhận lớn hơn 0 (MSG-WHS-02).");
        }
    }
}
