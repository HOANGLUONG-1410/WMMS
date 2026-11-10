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

    public async Task<List<ProductionBatch>> GetAllActiveBatchesAsync(
        BatchStatusEnum? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildFullBatchQuery();
        if (status.HasValue)
        {
            query = query.Where(pb => pb.BatchStatus == status.Value);
        }

        return await query
            .OrderBy(pb => pb.OrderItem.CustomerOrder.EtdDate)
            .ThenBy(pb => pb.BatchId)
            .ToListAsync(cancellationToken);
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

    private IQueryable<ProductionBatch> BuildFullBatchQuery()
    {
        return _context.ProductionBatches
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
    }
}
