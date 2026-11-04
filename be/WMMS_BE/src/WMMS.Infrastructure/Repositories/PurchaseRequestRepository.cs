using Microsoft.EntityFrameworkCore;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Domain.Entities.InventoryPr;
using WMMS.Domain.Enums;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Repositories;

/// <summary>
/// Thực thi kho lưu trữ Phiếu Yêu cầu Mua hàng Tổng hợp (<c>consolidated_prs</c>, <c>consolidated_pr_items</c>)
/// phục vụ BR-PUR-01 và SC-15.
/// </summary>
public class PurchaseRequestRepository : IPurchaseRequestRepository
{
    private readonly WmmsDbContext _context;

    public PurchaseRequestRepository(WmmsDbContext context)
    {
        _context = context;
    }

    public async Task<PurchaseRequest?> GetByIdAsync(long prId, CancellationToken cancellationToken = default)
    {
        return await BuildFullPrQuery()
            .FirstOrDefaultAsync(pr => pr.PrId == prId, cancellationToken);
    }

    public async Task<PurchaseRequest?> GetByCodeAsync(string prCode, CancellationToken cancellationToken = default)
    {
        return await BuildFullPrQuery()
            .FirstOrDefaultAsync(pr => pr.PrCode == prCode, cancellationToken);
    }

    public async Task<PurchaseRequest?> GetPendingPrByDateAsync(DateTime aggregationDate, CancellationToken cancellationToken = default)
    {
        var dateOnly = aggregationDate.Date;
        return await BuildFullPrQuery()
            .Where(pr => pr.AggregationDate == dateOnly && pr.PrStatus == PrStatusEnum.PENDING_APPROVAL)
            .OrderByDescending(pr => pr.PrId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<PurchaseRequest>> GetAllAsync(PrStatusEnum? status = null, CancellationToken cancellationToken = default)
    {
        var query = BuildFullPrQuery();
        if (status.HasValue)
        {
            query = query.Where(pr => pr.PrStatus == status.Value);
        }

        return await query
            .OrderByDescending(pr => pr.AggregationDate)
            .ThenByDescending(pr => pr.PrId)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountByDateAsync(DateTime aggregationDate, CancellationToken cancellationToken = default)
    {
        var dateOnly = aggregationDate.Date;
        return await _context.PurchaseRequests.CountAsync(pr => pr.AggregationDate == dateOnly, cancellationToken);
    }

    public async Task AddAsync(PurchaseRequest pr, CancellationToken cancellationToken = default)
    {
        await _context.PurchaseRequests.AddAsync(pr, cancellationToken);
    }

    public Task UpdateAsync(PurchaseRequest pr)
    {
        _context.PurchaseRequests.Update(pr);
        return Task.CompletedTask;
    }

    public async Task AddItemAsync(PurchaseRequestItem item, CancellationToken cancellationToken = default)
    {
        await _context.PurchaseRequestItems.AddAsync(item, cancellationToken);
    }

    public async Task AddItemsRangeAsync(IEnumerable<PurchaseRequestItem> items, CancellationToken cancellationToken = default)
    {
        await _context.PurchaseRequestItems.AddRangeAsync(items, cancellationToken);
    }

    public Task RemoveItemsRangeAsync(IEnumerable<PurchaseRequestItem> items)
    {
        _context.PurchaseRequestItems.RemoveRange(items);
        return Task.CompletedTask;
    }

    private IQueryable<PurchaseRequest> BuildFullPrQuery()
    {
        return _context.PurchaseRequests
            .Include(pr => pr.Creator)
            .Include(pr => pr.Approver)
            .Include(pr => pr.Items)
                .ThenInclude(pri => pri.Material)
                    .ThenInclude(m => m.MaterialStock)
            .Include(pr => pr.PurchaseOrders);
    }
}
