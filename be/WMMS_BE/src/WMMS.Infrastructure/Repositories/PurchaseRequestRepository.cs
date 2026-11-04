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

    public async Task<(List<PurchaseRequest> Items, int TotalCount)> SearchAsync(
        IReadOnlyCollection<PrStatusEnum>? statuses,
        DateTime? fromDate,
        DateTime? toDate,
        string? keyword,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildFullPrQuery().AsNoTracking();

        if (statuses is { Count: > 0 })
        {
            query = query.Where(pr => statuses.Contains(pr.PrStatus));
        }

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(pr => pr.AggregationDate >= from);
        }

        if (toDate.HasValue)
        {
            var to = toDate.Value.Date;
            query = query.Where(pr => pr.AggregationDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(pr =>
                pr.PrCode.ToLower().Contains(kw) ||
                pr.Items.Any(i => i.Material.MaterialCode.ToLower().Contains(kw) ||
                                  i.Material.MaterialName.ToLower().Contains(kw)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(pr => pr.AggregationDate)
            .ThenByDescending(pr => pr.PrId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<bool> TryTransitionStatusAsync(
        long prId,
        PrStatusEnum expectedStatus,
        PrStatusEnum newStatus,
        long reviewerUserId,
        string? rejectionReason,
        DateTime reviewedAt,
        CancellationToken cancellationToken = default)
    {
        var affected = await _context.PurchaseRequests
            .Where(pr => pr.PrId == prId && pr.PrStatus == expectedStatus)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(pr => pr.PrStatus, newStatus)
                .SetProperty(pr => pr.ApprovedBy, reviewerUserId)
                .SetProperty(pr => pr.ReviewedAt, reviewedAt)
                .SetProperty(pr => pr.RejectionReason, rejectionReason)
                .SetProperty(pr => pr.UpdatedAt, reviewedAt),
                cancellationToken);

        if (affected == 0)
        {
            return false;
        }

        // Đồng bộ lại thực thể đang được theo dõi (nếu có) để tránh dữ liệu cũ sau khi UPDATE trực tiếp
        var tracked = _context.ChangeTracker.Entries<PurchaseRequest>().FirstOrDefault(e => e.Entity.PrId == prId);
        if (tracked != null)
        {
            await tracked.ReloadAsync(cancellationToken);
        }

        return true;
    }

    public async Task<bool> TryMarkPoIssuedAsync(long prId, DateTime updatedAt, CancellationToken cancellationToken = default)
    {
        var affected = await _context.PurchaseRequests
            .Where(pr => pr.PrId == prId && pr.PrStatus == PrStatusEnum.APPROVED)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(pr => pr.PrStatus, PrStatusEnum.PO_ISSUED)
                .SetProperty(pr => pr.UpdatedAt, updatedAt),
                cancellationToken);

        if (affected == 0)
        {
            return false;
        }

        var tracked = _context.ChangeTracker.Entries<PurchaseRequest>().FirstOrDefault(e => e.Entity.PrId == prId);
        if (tracked != null)
        {
            await tracked.ReloadAsync(cancellationToken);
        }

        return true;
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
