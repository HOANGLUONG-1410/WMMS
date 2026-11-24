using Microsoft.EntityFrameworkCore;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Domain.Entities.KanbanQc;
using WMMS.Domain.Enums;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Repositories;

/// <summary>
/// Thực thi kho lưu trữ Thẻ Sản xuất Kanban (<c>production_batches</c>) và Công đoạn Thực thi (<c>batch_routing_steps</c>)
/// phục vụ SC-18, SC-19 và BR-PRD-01 (Tách thẻ nhánh 97-3).
/// </summary>
public class ProductionBatchRepository : IProductionBatchRepository
{
    private readonly WmmsDbContext _context;

    public ProductionBatchRepository(WmmsDbContext context)
    {
        _context = context;
    }

    public async Task<ProductionBatch?> GetByIdAsync(long batchId, CancellationToken cancellationToken = default)
    {
        return await BuildFullBatchQuery()
            .FirstOrDefaultAsync(pb => pb.BatchId == batchId, cancellationToken);
    }

    public async Task<ProductionBatch?> GetByCodeAsync(string batchCode, CancellationToken cancellationToken = default)
    {
        return await BuildFullBatchQuery()
            .FirstOrDefaultAsync(pb => pb.BatchCode == batchCode, cancellationToken);
    }

    public async Task<List<ProductionBatch>> GetByOrderItemIdAsync(long orderItemId, CancellationToken cancellationToken = default)
    {
        return await BuildFullBatchQuery()
            .Where(pb => pb.OrderItemId == orderItemId)
            .OrderBy(pb => pb.BatchId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ProductionBatch>> GetByOrderIdAsync(long orderId, CancellationToken cancellationToken = default)
    {
        return await BuildFullBatchQuery()
            .Where(pb => pb.OrderItem.OrderId == orderId)
            .OrderBy(pb => pb.BatchId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ProductionBatch>> GetBySpaceIdAsync(long spaceId, CancellationToken cancellationToken = default)
    {
        return await BuildFullBatchQuery()
            .Where(pb => pb.OrderItem.CustomerOrder.OrderSpace != null &&
                         pb.OrderItem.CustomerOrder.OrderSpace.SpaceId == spaceId)
            .OrderBy(pb => pb.BatchId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ProductionBatch>> GetBySpaceKeyAsync(string spaceKey, CancellationToken cancellationToken = default)
    {
        var upper = (spaceKey ?? string.Empty).Trim().ToUpper();
        return await BuildFullBatchQuery()
            .Where(pb => pb.OrderItem.CustomerOrder.OrderSpace != null &&
                         (pb.OrderItem.CustomerOrder.OrderSpace.SpaceKey.ToUpper() == upper ||
                          pb.OrderItem.CustomerOrder.PoNumber.ToUpper() == upper))
            .OrderBy(pb => pb.BatchId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Lấy danh sách các thẻ sản xuất Kanban đang hoạt động (tùy chọn lọc theo trạng thái).
    /// Sắp xếp trong SQL theo khóa chính <c>BatchId</c> và sắp xếp theo <c>EtdDate</c> trên bộ nhớ ứng dụng
    /// để tránh toán tử Sort chiếm Memory Grant lớn (<c>RESOURCE_SEMAPHORE</c>) trên SQL Server.
    /// </summary>
    public async Task<List<ProductionBatch>> GetAllActiveBatchesAsync(
        BatchStatusEnum? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildFullBatchQuery(includeBomDetails: false);
        if (status.HasValue)
        {
            query = query.Where(pb => pb.BatchStatus == status.Value);
        }

        var batches = await query
            .OrderBy(pb => pb.BatchId)
            .ToListAsync(cancellationToken);

        return batches
            .OrderBy(pb => pb.OrderItem?.CustomerOrder?.EtdDate ?? DateTime.MaxValue)
            .ThenBy(pb => pb.BatchId)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<int> CountBatchesForAnalyticsAsync(
        long? orderId = null,
        string? woodType = null,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = _context.ProductionBatches.AsNoTracking();
        if (orderId.HasValue)
        {
            baseQuery = baseQuery.Where(pb => pb.OrderItem.OrderId == orderId.Value);
        }

        if (string.IsNullOrWhiteSpace(woodType))
        {
            return await baseQuery.CountAsync(cancellationToken);
        }

        var keyword = woodType.Trim();
        var batchRows = await baseQuery
            .Select(pb => new
            {
                pb.OrderItemId,
                WoodSpecies = pb.OrderItem != null && pb.OrderItem.Product != null
                    ? pb.OrderItem.Product.WoodSpecies
                    : null
            })
            .ToListAsync(cancellationToken);

        var orderItemIdsNeedingBomFallback = batchRows
            .Where(r => string.IsNullOrWhiteSpace(r.WoodSpecies))
            .Select(r => r.OrderItemId)
            .Distinct()
            .ToList();

        var lumberByOrderItemId = new Dictionary<long, string>();
        if (orderItemIdsNeedingBomFallback.Count > 0)
        {
            var bomLumberRows = await _context.BomHeaders
                .AsNoTracking()
                .Where(bh => orderItemIdsNeedingBomFallback.Contains(bh.OrderItemId))
                .SelectMany(bh => bh.BomDetails
                    .Where(bd => bd.Material.MaterialGroup == MaterialGroupEnum.LUMBER)
                    .Select(bd => new
                    {
                        bh.OrderItemId,
                        bd.Material.MaterialName
                    }))
                .ToListAsync(cancellationToken);

            foreach (var row in bomLumberRows)
            {
                if (!string.IsNullOrWhiteSpace(row.MaterialName) &&
                    !lumberByOrderItemId.ContainsKey(row.OrderItemId))
                {
                    lumberByOrderItemId[row.OrderItemId] = row.MaterialName.Trim();
                }
            }
        }

        return batchRows.Count(r =>
        {
            var resolvedWoodType = !string.IsNullOrWhiteSpace(r.WoodSpecies)
                ? r.WoodSpecies!.Trim()
                : (lumberByOrderItemId.TryGetValue(r.OrderItemId, out var lumberName)
                    ? lumberName
                    : "Gỗ tự nhiên tiêu chuẩn");

            return resolvedWoodType.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <inheritdoc />
    public async Task<List<ProductionBatch>> GetBatchesForDispatchExportAsync(
        long? orderId = null,
        bool includeBomDetails = false,
        CancellationToken cancellationToken = default)
    {
        var query = BuildFullBatchQuery(includeBomDetails).AsNoTracking();
        if (orderId.HasValue)
        {
            query = query.Where(pb => pb.OrderItem.OrderId == orderId.Value);
        }

        var batches = await query
            .OrderBy(pb => pb.BatchId)
            .ToListAsync(cancellationToken);

        return batches
            .OrderBy(pb => pb.OrderItem?.CustomerOrder?.EtdDate ?? DateTime.MaxValue)
            .ThenBy(pb => pb.BatchId)
            .ToList();
    }

    public async Task<BatchRoutingStep?> GetStepByIdAsync(long batchStepId, CancellationToken cancellationToken = default)
    {
        return await _context.BatchRoutingSteps
            .Include(brs => brs.ProductionBatch)
                .ThenInclude(pb => pb.OrderItem)
                    .ThenInclude(oi => oi.Product)
            .Include(brs => brs.QcInspections)
                .ThenInclude(qi => qi.DefectLogs)
            .FirstOrDefaultAsync(brs => brs.BatchStepId == batchStepId, cancellationToken);
    }

    public async Task AddAsync(ProductionBatch batch, CancellationToken cancellationToken = default)
    {
        await _context.ProductionBatches.AddAsync(batch, cancellationToken);
    }

    public Task UpdateAsync(ProductionBatch batch)
    {
        _context.ProductionBatches.Update(batch);
        return Task.CompletedTask;
    }

    public async Task AddStepsRangeAsync(IEnumerable<BatchRoutingStep> steps, CancellationToken cancellationToken = default)
    {
        await _context.BatchRoutingSteps.AddRangeAsync(steps, cancellationToken);
    }

    public Task UpdateStepAsync(BatchRoutingStep step)
    {
        _context.BatchRoutingSteps.Update(step);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Xây dựng truy vấn nạp đồ thị Thẻ Sản xuất Kanban (<c>production_batches</c>), sản phẩm, đơn hàng,
    /// công đoạn, kiểm định KCS và nhật ký ngoại lệ Overdrive phục vụ SC-20, SC-20b và Iter 3 (<c>UC-64</c>, <c>BR-PRD-01</c>).
    /// Chỉ nối nhánh <c>BomHeader -&gt; BomDetails -&gt; Material</c> khi <paramref name="includeBomDetails"/> là <c>true</c>
    /// để tránh cấp phát Memory Grant thừa trên SQL Server, đồng thời áp dụng <c>AsSplitQuery()</c>.
    /// </summary>
    private IQueryable<ProductionBatch> BuildFullBatchQuery(bool includeBomDetails = false)
    {
        IQueryable<ProductionBatch> query = _context.ProductionBatches
            .Include(pb => pb.OrderItem)
                .ThenInclude(oi => oi.Product)
            .Include(pb => pb.OrderItem)
                .ThenInclude(oi => oi.CustomerOrder)
                    .ThenInclude(co => co.Customer)
            .Include(pb => pb.OrderItem)
                .ThenInclude(oi => oi.CustomerOrder)
                    .ThenInclude(co => co.OrderSpace)
            .Include(pb => pb.ParentBatch)
            .Include(pb => pb.SubBatches)
            .Include(pb => pb.BatchRoutingSteps)
                .ThenInclude(brs => brs.QcInspections)
                    .ThenInclude(qi => qi.DefectLogs)
            .Include(pb => pb.OverdriveLogs)
                .ThenInclude(ol => ol.Approver);

        if (includeBomDetails)
        {
            query = query
                .Include(pb => pb.OrderItem)
                    .ThenInclude(oi => oi.BomHeader)
                        .ThenInclude(bh => bh!.BomDetails)
                            .ThenInclude(bd => bd.Material);
        }

        return query.AsSplitQuery();
    }
}
