using WMMS.Application.DTOs.ProductionTracking;

namespace WMMS.Application.Interfaces.External;

/// <summary>
/// Giao diện bộ xuất báo cáo Excel chuẩn xưởng gỗ (<c>.xlsx</c>) bằng thư viện EPPlus (Iteration 3):
/// 1. Mẫu Báo cáo Thống kê Sản xuất &amp; Tổng kết Tiến độ Đơn hàng + Đối chiếu Hao hụt BOM.
/// 2. Mẫu Phiếu Kiểm định Chất lượng KCS &amp; Bảng Nhật ký Kiểm định Từng Công đoạn phục vụ lưu trữ nghiệm thu.
/// 3. Mẫu Lịch sử Điều độ Phân xưởng, Lộ trình Thẻ Lô Kanban &amp; Nhật ký Phá khóa Ngoại lệ Hold Point.
/// 4. Sổ tay Nghiệm thu Tổng hợp Toàn Xưởng (All-in-One Workbook).
/// </summary>
public interface IProductionReportExcelExporter
{
    /// <summary>
    /// Xuất tệp Excel (.xlsx) Báo cáo Thống kê Sản xuất, Tổng kết Tiến độ Đơn hàng &amp; Đối chiếu Hao hụt BOM.
    /// </summary>
    ExcelExportFileDto ExportProductionStatisticsExcel(
        ProductionOverviewDashboardDto overview,
        IReadOnlyList<OrderProductionProgressDto> orderProgressList,
        ScrapVarianceReportDto scrapVarianceReport);

    /// <summary>
    /// Xuất tệp Excel (.xlsx) Phiếu Kiểm định Chất lượng KCS &amp; Bảng Nhật ký Kiểm định Từng Công đoạn phục vụ lưu trữ nghiệm thu.
    /// </summary>
    ExcelExportFileDto ExportQualityInspectionSheetExcel(
        IReadOnlyList<QcInspectionStageExportRowDto> inspectionRows,
        IReadOnlyList<DefectLogExportRowDto> defectRows,
        DefectAnalyticsReportDto defectAnalytics);

    /// <summary>
    /// Xuất tệp Excel (.xlsx) Lịch sử Điều độ Phân xưởng, Lộ trình Thẻ Lô Kanban &amp; Thống kê Phá khóa Hold Point (Overdriver).
    /// </summary>
    ExcelExportFileDto ExportWorkshopDispatchHistoryExcel(
        IReadOnlyList<WorkshopDispatchExportRowDto> dispatchRows,
        OverdriveAnalyticsReportDto overdriveAnalytics);

    /// <summary>
    /// Xuất Sổ tay Nghiệm thu Tổng hợp Toàn Xưởng (.xlsx) bao gồm tất cả các sheet: Tiến độ Đơn hàng, Đối chiếu Hao hụt, Phiếu Kiểm định KCS, Phân tích Lỗi, Lịch sử Điều độ Kanban &amp; Nhật ký Overdriver.
    /// </summary>
    ExcelExportFileDto ExportComprehensiveWorkshopWorkbookExcel(
        ProductionOverviewDashboardDto overview,
        IReadOnlyList<OrderProductionProgressDto> orderProgressList,
        ScrapVarianceReportDto scrapVarianceReport,
        IReadOnlyList<QcInspectionStageExportRowDto> inspectionRows,
        IReadOnlyList<DefectLogExportRowDto> defectRows,
        DefectAnalyticsReportDto defectAnalytics,
        IReadOnlyList<WorkshopDispatchExportRowDto> dispatchRows,
        OverdriveAnalyticsReportDto overdriveAnalytics);
}
