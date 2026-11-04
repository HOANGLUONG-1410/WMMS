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
