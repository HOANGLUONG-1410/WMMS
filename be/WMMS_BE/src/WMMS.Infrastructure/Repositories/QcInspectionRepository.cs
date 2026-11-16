using Microsoft.EntityFrameworkCore;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Domain.Entities.KanbanQc;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Repositories;

/// <summary>
/// Thực thi kho lưu trữ Kiểm định Chất lượng QC (<c>qc_inspections</c>), Danh mục lỗi (<c>defect_catalogs</c>),
/// Nhật ký lỗi (<c>defect_logs</c>) và Nhật ký Phá khóa Hold Point (<c>overdrive_logs</c>) phục vụ SC-20 &amp; Iteration 3.
/// </summary>
public class QcInspectionRepository : IQcInspectionRepository
{
    private readonly WmmsDbContext _context;

    public QcInspectionRepository(WmmsDbContext context)
    {
        _context = context;
    }

    public async Task<QcInspection?> GetByIdAsync(long qcId, CancellationToken cancellationToken = default)
    {
        return await _context.QcInspections
            .Include(qi => qi.Inspector)
            .Include(qi => qi.BatchRoutingStep)
                .ThenInclude(brs => brs.ProductionBatch)
            .Include(qi => qi.DefectLogs)
                .ThenInclude(dl => dl.DefectCatalog)
            .Include(qi => qi.DefectLogs)
                .ThenInclude(dl => dl.SpawnedSubBatch)
            .FirstOrDefaultAsync(qi => qi.QcId == qcId, cancellationToken);
    }

    public async Task<List<QcInspection>> GetByBatchIdAsync(long batchId, CancellationToken cancellationToken = default)
    {
        return await _context.QcInspections
            .Include(qi => qi.Inspector)
            .Include(qi => qi.BatchRoutingStep)
            .Include(qi => qi.DefectLogs)
                .ThenInclude(dl => dl.SpawnedSubBatch)
            .Where(qi => qi.BatchRoutingStep.BatchId == batchId)
            .OrderByDescending(qi => qi.InspectedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<QcInspection>> GetByBatchStepIdAsync(long batchStepId, CancellationToken cancellationToken = default)
    {
        return await _context.QcInspections
            .Include(qi => qi.Inspector)
            .Include(qi => qi.DefectLogs)
                .ThenInclude(dl => dl.SpawnedSubBatch)
            .Where(qi => qi.BatchStepId == batchStepId)
            .OrderByDescending(qi => qi.InspectedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<DefectCatalog>> GetAllDefectCatalogsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.DefectCatalogs
            .AsNoTracking()
            .OrderBy(dc => dc.DefectCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<DefectCatalog?> GetDefectCatalogByCodeAsync(string defectCode, CancellationToken cancellationToken = default)
    {
        return await _context.DefectCatalogs
            .FirstOrDefaultAsync(dc => dc.DefectCode == defectCode, cancellationToken);
    }

    public async Task<List<OverdriveLog>> GetOverdriveLogsByBatchIdAsync(long batchId, CancellationToken cancellationToken = default)
    {
        return await _context.OverdriveLogs
            .Include(ol => ol.Requester)
            .Include(ol => ol.Approver)
            .Where(ol => ol.BatchId == batchId)
            .OrderByDescending(ol => ol.ApprovedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<DefectLog>> GetAllDefectLogsWithAnalyticsGraphAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? orderId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.DefectLogs
            .AsNoTracking()
            .Include(dl => dl.DefectCatalog)
            .Include(dl => dl.SpawnedSubBatch)
            .Include(dl => dl.QcInspection)
                .ThenInclude(qi => qi.Inspector)
            .Include(dl => dl.QcInspection)
                .ThenInclude(qi => qi.BatchRoutingStep)
                    .ThenInclude(brs => brs.ProductionBatch)
                        .ThenInclude(pb => pb.OrderItem)
                            .ThenInclude(oi => oi.Product)
            .Include(dl => dl.QcInspection)
                .ThenInclude(qi => qi.BatchRoutingStep)
                    .ThenInclude(brs => brs.ProductionBatch)
                        .ThenInclude(pb => pb.OrderItem)
                            .ThenInclude(oi => oi.CustomerOrder)
            .Include(dl => dl.QcInspection)
                .ThenInclude(qi => qi.BatchRoutingStep)
                    .ThenInclude(brs => brs.ProductionBatch)
                        .ThenInclude(pb => pb.OrderItem)
                            .ThenInclude(oi => oi.BomHeader)
                                .ThenInclude(bh => bh!.BomDetails)
                                    .ThenInclude(bd => bd.Material)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(dl => dl.QcInspection.InspectedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(dl => dl.QcInspection.InspectedAt <= toDate.Value);
        }

        if (orderId.HasValue)
        {
            query = query.Where(dl => dl.QcInspection.BatchRoutingStep.ProductionBatch.OrderItem.OrderId == orderId.Value);
        }

        return await query
            .OrderByDescending(dl => dl.QcInspection.InspectedAt)
            .ThenByDescending(dl => dl.DefectLogId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<OverdriveLog>> GetAllOverdriveLogsWithAnalyticsGraphAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? orderId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.OverdriveLogs
            .AsNoTracking()
            .Include(ol => ol.Requester)
            .Include(ol => ol.Approver)
            .Include(ol => ol.ProductionBatch)
                .ThenInclude(pb => pb.BatchRoutingSteps)
            .Include(ol => ol.ProductionBatch)
                .ThenInclude(pb => pb.OrderItem)
                    .ThenInclude(oi => oi.Product)
            .Include(ol => ol.ProductionBatch)
                .ThenInclude(pb => pb.OrderItem)
                    .ThenInclude(oi => oi.CustomerOrder)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(ol => ol.ApprovedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(ol => ol.ApprovedAt <= toDate.Value);
        }

        if (orderId.HasValue)
        {
            query = query.Where(ol => ol.ProductionBatch.OrderItem.OrderId == orderId.Value);
        }

        return await query
            .OrderByDescending(ol => ol.ApprovedAt)
            .ThenByDescending(ol => ol.OverdriveId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<QcInspection>> GetAllInspectionsWithFullGraphAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? orderId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.QcInspections
            .AsNoTracking()
            .Include(qi => qi.Inspector)
            .Include(qi => qi.DefectLogs)
                .ThenInclude(dl => dl.DefectCatalog)
            .Include(qi => qi.DefectLogs)
                .ThenInclude(dl => dl.SpawnedSubBatch)
            .Include(qi => qi.BatchRoutingStep)
                .ThenInclude(brs => brs.ProductionBatch)
                    .ThenInclude(pb => pb.OrderItem)
                        .ThenInclude(oi => oi.Product)
            .Include(qi => qi.BatchRoutingStep)
                .ThenInclude(brs => brs.ProductionBatch)
                    .ThenInclude(pb => pb.OrderItem)
                        .ThenInclude(oi => oi.CustomerOrder)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(qi => qi.InspectedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(qi => qi.InspectedAt <= toDate.Value);
        }

        if (orderId.HasValue)
        {
            query = query.Where(qi => qi.BatchRoutingStep.ProductionBatch.OrderItem.OrderId == orderId.Value);
        }

        return await query
            .OrderByDescending(qi => qi.InspectedAt)
            .ThenByDescending(qi => qi.QcId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddInspectionAsync(QcInspection inspection, CancellationToken cancellationToken = default)
    {
        await _context.QcInspections.AddAsync(inspection, cancellationToken);
    }

    public async Task AddDefectLogsRangeAsync(IEnumerable<DefectLog> defectLogs, CancellationToken cancellationToken = default)
    {
        await _context.DefectLogs.AddRangeAsync(defectLogs, cancellationToken);
    }

    public async Task AddOverdriveLogAsync(OverdriveLog overdriveLog, CancellationToken cancellationToken = default)
    {
        await _context.OverdriveLogs.AddAsync(overdriveLog, cancellationToken);
    }
}
