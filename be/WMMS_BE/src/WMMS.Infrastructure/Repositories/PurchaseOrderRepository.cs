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
