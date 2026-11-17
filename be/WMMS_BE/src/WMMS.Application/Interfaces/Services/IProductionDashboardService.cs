using WMMS.Application.DTOs.ProductionTracking;

namespace WMMS.Application.Interfaces.Services;

/// <summary>
/// Giao diện dịch vụ Dashboard Tổng quan Sản xuất cho Chủ xưởng &amp; Báo cáo Đối chiếu Hao hụt Gỗ Thực tế so với Định mức BOM (Iteration 3).
/// </summary>
public interface IProductionDashboardService
{
    /// <summary>
    /// Tổng hợp bức tranh điều hành sản xuất toàn xưởng: số đơn hàng đang chạy, cảnh báo trễ ETD, WIP theo từng trạm công đoạn, số lô bị chặn Hold Point và tỷ lệ đạt chuẩn.
    /// </summary>
    Task<ProductionOverviewDashboardDto> GetProductionOverviewDashboardAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phân tích và đối chiếu tỷ lệ hao hụt thực tế (từ nhật ký lỗi QC và tách nhánh 97-3) so với định mức hao hụt tự nhiên đã duyệt trong BOM (`ScrapRate`).
    /// </summary>
    Task<ScrapVarianceReportDto> GetScrapVarianceReportAsync(
        long? orderId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Báo cáo Phân tích Lỗi Chất lượng theo Trạm máy / Công đoạn và theo Loại gỗ (Defect Analytics by Station / Wood Type - Iteration 3, QT-06, KBN01).
    /// Tổng hợp từ <c>defect_logs</c> để chỉ ra trạm máy phát sinh lỗi nhiều nhất (vd: trạm Xẻ gỗ phôi nứt, trạm Sơn màu bọt khí) và loại gỗ nhạy cảm lỗi nhất.
    /// </summary>
    Task<DefectAnalyticsReportDto> GetDefectAnalyticsReportAsync(
        QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Báo cáo Thống kê Tần suất Phá khóa Ngoại lệ Overdriver tại các trạm Hold Point (SC-20, KBN02, GBR-05).
    /// Tổng hợp từ <c>overdrive_logs</c> để thống kê số lần Quản lý / Chủ xưởng phải duyệt phá khóa Hold Point, phân nhóm theo người duyệt, người yêu cầu và trạm dừng.
    /// </summary>
    Task<OverdriveAnalyticsReportDto> GetOverdriveAnalyticsReportAsync(
        QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Báo cáo Tổng hợp Phân tích Lỗi Chất lượng &amp; Thống kê Tần suất Phá khóa Ngoại lệ Overdriver trên một màn hình điều hành hợp nhất.
    /// </summary>
    Task<QualityAndOverdriveAnalyticsDto> GetQualityAndOverdriveSummaryAsync(
        QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xuất tệp Excel (.xlsx) Báo cáo Thống kê Sản xuất, Tổng kết Tiến độ Đơn hàng &amp; Đối chiếu Hao hụt BOM chuẩn xưởng gỗ.
    /// </summary>
    Task<ExcelExportFileDto> ExportProductionStatisticsExcelAsync(
        QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xuất tệp Excel (.xlsx) Phiếu Kiểm định Chất lượng KCS &amp; Bảng Nhật ký Kiểm định Từng Công đoạn phục vụ lưu trữ nghiệm thu.
    /// </summary>
    Task<ExcelExportFileDto> ExportQualityInspectionExcelAsync(
        QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xuất tệp Excel (.xlsx) Lịch sử Điều độ Phân xưởng, Lộ trình Thẻ Lô Kanban &amp; Nhật ký Phá khóa Hold Point (Overdriver).
    /// </summary>
    Task<ExcelExportFileDto> ExportWorkshopDispatchHistoryExcelAsync(
        QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xuất Sổ tay Nghiệm thu Tổng hợp Toàn Xưởng (.xlsx) gồm đầy đủ 6 sheets (Tiến độ Đơn hàng, Đối chiếu Hao hụt, Phiếu KCS, Phân tích Lỗi, Điều độ Kanban &amp; Overdriver).
    /// </summary>
    Task<ExcelExportFileDto> ExportComprehensiveWorkshopWorkbookExcelAsync(
        QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default);
}
