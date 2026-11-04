using Microsoft.EntityFrameworkCore;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Domain.Entities.WarehousePo;
using WMMS.Domain.Enums;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Repositories;

/// <summary>
/// Thực thi kho lưu trữ Đơn đặt hàng Nhà cung cấp (<c>purchase_orders</c>, <c>purchase_order_details</c>)
/// và Phiếu nhập kho (<c>goods_receipt_notes</c>) phục vụ SC-16 và SC-17.
/// </summary>
public class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly WmmsDbContext _context;

    public PurchaseOrderRepository(WmmsDbContext context)
    {
        _context = context;
    }

    public async Task<PurchaseOrder?> GetByIdAsync(long poId, CancellationToken cancellationToken = default)
    {
        return await BuildFullPoQuery()
            .FirstOrDefaultAsync(po => po.PoId == poId, cancellationToken);
    }

    public async Task<PurchaseOrder?> GetByCodeAsync(string poCode, CancellationToken cancellationToken = default)
    {
        return await BuildFullPoQuery()
            .FirstOrDefaultAsync(po => po.PoCode == poCode, cancellationToken);
    }

    public async Task<List<PurchaseOrder>> GetByPrIdAsync(long prId, CancellationToken cancellationToken = default)
    {
        return await BuildFullPoQuery()
            .Where(po => po.PrId == prId)
            .OrderByDescending(po => po.IssueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PurchaseOrder>> GetBySupplierIdAsync(long supplierId, CancellationToken cancellationToken = default)
    {
        return await BuildFullPoQuery()
            .Where(po => po.SupplierId == supplierId)
            .OrderByDescending(po => po.IssueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PurchaseOrder>> GetAllAsync(PoStatusEnum? status = null, CancellationToken cancellationToken = default)
    {
        var query = BuildFullPoQuery();
        if (status.HasValue)
        {
            query = query.Where(po => po.PoStatus == status.Value);
        }

        return await query
            .OrderByDescending(po => po.IssueDate)
            .ThenByDescending(po => po.PoId)
            .ToListAsync(cancellationToken);
    }

    public async Task<(List<PurchaseOrder> Items, int TotalCount)> SearchAsync(
        long? supplierId,
        PoStatusEnum? status,
        DateTime? fromEta,
        DateTime? toEta,
        DateTime? overdueBefore,
        DateTime? dueSoonFrom,
        DateTime? dueSoonTo,
        string? keyword,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildFullPoQuery().AsNoTracking();

        if (supplierId.HasValue)
        {
            query = query.Where(po => po.SupplierId == supplierId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(po => po.PoStatus == status.Value);
        }

        if (fromEta.HasValue)
        {
            var from = fromEta.Value.Date;
            query = query.Where(po => po.ExpectedArrivalDate >= from);
        }

        if (toEta.HasValue)
        {
            var to = toEta.Value.Date.AddDays(1);
            query = query.Where(po => po.ExpectedArrivalDate < to);
        }

        if (overdueBefore.HasValue)
        {
            var limit = overdueBefore.Value.Date;
            query = query.Where(po => po.ExpectedArrivalDate < limit &&
                                      (po.PoStatus == PoStatusEnum.ISSUED || po.PoStatus == PoStatusEnum.PARTIALLY_RECEIVED));
        }

        if (dueSoonFrom.HasValue && dueSoonTo.HasValue)
        {
            var from = dueSoonFrom.Value.Date;
            var to = dueSoonTo.Value.Date.AddDays(1);
            query = query.Where(po => po.ExpectedArrivalDate >= from && po.ExpectedArrivalDate < to &&
                                      (po.PoStatus == PoStatusEnum.ISSUED || po.PoStatus == PoStatusEnum.PARTIALLY_RECEIVED));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(po =>
                po.PoCode.ToLower().Contains(kw) ||
                po.Supplier.SupplierName.ToLower().Contains(kw) ||
                po.PurchaseRequest.PrCode.ToLower().Contains(kw) ||
                po.Details.Any(d => d.Material.MaterialCode.ToLower().Contains(kw) ||
                                    d.Material.MaterialName.ToLower().Contains(kw)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(po => po.ExpectedArrivalDate)
            .ThenByDescending(po => po.PoId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<(int Issued, int Partial, int Fulfilled, int Cancelled, int Overdue, int DueSoon)> GetStatusCountsAsync(
        DateTime today,
        DateTime dueSoonTo,
        CancellationToken cancellationToken = default)
    {
        var day = today.Date;
        var soonEnd = dueSoonTo.Date.AddDays(1);

        var issued = await _context.PurchaseOrders.CountAsync(po => po.PoStatus == PoStatusEnum.ISSUED, cancellationToken);
        var partial = await _context.PurchaseOrders.CountAsync(po => po.PoStatus == PoStatusEnum.PARTIALLY_RECEIVED, cancellationToken);
        var fulfilled = await _context.PurchaseOrders.CountAsync(po => po.PoStatus == PoStatusEnum.FULFILLED, cancellationToken);
        var cancelled = await _context.PurchaseOrders.CountAsync(po => po.PoStatus == PoStatusEnum.CANCELLED, cancellationToken);

        var open = _context.PurchaseOrders.Where(po =>
            po.PoStatus == PoStatusEnum.ISSUED || po.PoStatus == PoStatusEnum.PARTIALLY_RECEIVED);
        var overdue = await open.CountAsync(po => po.ExpectedArrivalDate < day, cancellationToken);
        var dueSoon = await open.CountAsync(po => po.ExpectedArrivalDate >= day && po.ExpectedArrivalDate < soonEnd, cancellationToken);

        return (issued, partial, fulfilled, cancelled, overdue, dueSoon);
    }

    public async Task<Dictionary<(long PrId, long MaterialId), decimal>> GetOrderedQtyByPrAsync(
        IReadOnlyCollection<long> prIds,
        CancellationToken cancellationToken = default)
    {
        if (prIds.Count == 0)
        {
            return new Dictionary<(long, long), decimal>();
        }

        var rows = await _context.PurchaseOrderDetails
            .AsNoTracking()
            .Where(d => prIds.Contains(d.PurchaseOrder.PrId) && d.PurchaseOrder.PoStatus != PoStatusEnum.CANCELLED)
            .Select(d => new { d.PurchaseOrder.PrId, d.MaterialId, d.OrderedQty })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => (r.PrId, r.MaterialId))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.OrderedQty));
    }

    public async Task<int> CountByCodePrefixAsync(string codePrefix, CancellationToken cancellationToken = default)
    {
        return await _context.PurchaseOrders.CountAsync(po => po.PoCode.StartsWith(codePrefix), cancellationToken);
    }

    public async Task<Dictionary<long, Supplier>> GetSuppliersByIdsAsync(
        IReadOnlyCollection<long> supplierIds,
        CancellationToken cancellationToken = default)
    {
        return await _context.Suppliers
            .AsNoTracking()
            .Where(s => supplierIds.Contains(s.SupplierId))
            .ToDictionaryAsync(s => s.SupplierId, cancellationToken);
    }

    public async Task AddAsync(PurchaseOrder po, CancellationToken cancellationToken = default)
    {
        await _context.PurchaseOrders.AddAsync(po, cancellationToken);
    }

    public Task UpdateAsync(PurchaseOrder po)
    {
        _context.PurchaseOrders.Update(po);
        return Task.CompletedTask;
    }

    public async Task AddDetailsRangeAsync(IEnumerable<PurchaseOrderDetail> details, CancellationToken cancellationToken = default)
    {
        await _context.PurchaseOrderDetails.AddRangeAsync(details, cancellationToken);
    }

    public async Task<GoodsReceipt?> GetGoodsReceiptByIdAsync(long grnId, CancellationToken cancellationToken = default)
    {
        return await _context.GoodsReceipts
            .Include(gr => gr.PurchaseOrder)
                .ThenInclude(po => po.Supplier)
            .Include(gr => gr.PurchaseOrder)
                .ThenInclude(po => po.Details)
                    .ThenInclude(d => d.Material)
            .Include(gr => gr.WarehouseKeeper)
            .FirstOrDefaultAsync(gr => gr.GrnId == grnId, cancellationToken);
    }

    public async Task<List<GoodsReceipt>> GetGoodsReceiptsByPoIdAsync(long poId, CancellationToken cancellationToken = default)
    {
        return await _context.GoodsReceipts
            .Include(gr => gr.WarehouseKeeper)
            .Where(gr => gr.PoId == poId)
            .OrderByDescending(gr => gr.CommittedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddGoodsReceiptAsync(GoodsReceipt grn, CancellationToken cancellationToken = default)
    {
        await _context.GoodsReceipts.AddAsync(grn, cancellationToken);
    }

    private IQueryable<PurchaseOrder> BuildFullPoQuery()
    {
        return _context.PurchaseOrders
            .Include(po => po.PurchaseRequest)
            .Include(po => po.Supplier)
            .Include(po => po.Creator)
            .Include(po => po.Details)
                .ThenInclude(d => d.Material)
                    .ThenInclude(m => m.MaterialStock)
            .Include(po => po.GoodsReceipts)
                .ThenInclude(gr => gr.WarehouseKeeper);
    }
}
