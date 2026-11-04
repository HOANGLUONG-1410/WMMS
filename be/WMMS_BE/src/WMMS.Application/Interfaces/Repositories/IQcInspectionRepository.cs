using WMMS.Domain.Entities.KanbanQc;

namespace WMMS.Application.Interfaces.Repositories;

/// <summary>
/// Giao diện kho lưu trữ Kiểm định Chất lượng QC (<c>qc_inspections</c>), Danh mục lỗi (<c>defect_catalogs</c>),
/// Nhật ký lỗi (<c>defect_logs</c>) và Nhật ký Phá khóa Hold Point (<c>overdrive_logs</c>) phục vụ SC-20 &amp; Iteration 3.
/// </summary>
public interface IQcInspectionRepository
{
    Task<QcInspection?> GetByIdAsync(long qcId, CancellationToken cancellationToken = default);
    Task<List<QcInspection>> GetByBatchIdAsync(long batchId, CancellationToken cancellationToken = default);
    Task<List<QcInspection>> GetByBatchStepIdAsync(long batchStepId, CancellationToken cancellationToken = default);
    Task<List<DefectCatalog>> GetAllDefectCatalogsAsync(CancellationToken cancellationToken = default);
    Task<DefectCatalog?> GetDefectCatalogByCodeAsync(string defectCode, CancellationToken cancellationToken = default);
    Task<List<OverdriveLog>> GetOverdriveLogsByBatchIdAsync(long batchId, CancellationToken cancellationToken = default);
    Task AddInspectionAsync(QcInspection inspection, CancellationToken cancellationToken = default);
    Task AddDefectLogsRangeAsync(IEnumerable<DefectLog> defectLogs, CancellationToken cancellationToken = default);
    Task AddOverdriveLogAsync(OverdriveLog overdriveLog, CancellationToken cancellationToken = default);
}
