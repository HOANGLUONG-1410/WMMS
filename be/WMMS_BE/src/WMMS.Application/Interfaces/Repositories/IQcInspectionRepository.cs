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

    /// <summary>
    /// Truy vấn toàn bộ nhật ký lỗi chất lượng (<c>defect_logs</c>) kèm đồ thị liên kết trạm công đoạn, lô sản xuất, sản phẩm, loại gỗ và danh mục lỗi phục vụ báo cáo phân tích chất lượng (Iteration 3).
    /// </summary>
    Task<List<DefectLog>> GetAllDefectLogsWithAnalyticsGraphAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? orderId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Truy vấn toàn bộ nhật ký phá khóa điểm dừng Hold Point (<c>overdrive_logs</c>) kèm người yêu cầu, người phê duyệt, thẻ lô và đơn hàng phục vụ thống kê tần suất Overdriver (SC-20, Iteration 3).
    /// </summary>
    Task<List<OverdriveLog>> GetAllOverdriveLogsWithAnalyticsGraphAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? orderId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Truy vấn toàn bộ biên bản kiểm định KCS từng công đoạn (<c>qc_inspections</c>) kèm người kiểm định, công đoạn, thẻ lô, sản phẩm, đơn hàng và danh sách lỗi phục vụ xuất Phiếu Kiểm định Chất lượng Excel (.xlsx).
    /// </summary>
    Task<List<QcInspection>> GetAllInspectionsWithFullGraphAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? orderId = null,
        CancellationToken cancellationToken = default);

    Task AddInspectionAsync(QcInspection inspection, CancellationToken cancellationToken = default);
    Task AddDefectLogsRangeAsync(IEnumerable<DefectLog> defectLogs, CancellationToken cancellationToken = default);
    Task AddOverdriveLogAsync(OverdriveLog overdriveLog, CancellationToken cancellationToken = default);
}
