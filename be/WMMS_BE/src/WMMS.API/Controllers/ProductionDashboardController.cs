using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.ProductionTracking;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;

namespace WMMS.API.Controllers;

/// <summary>
/// API Controller cung cấp Dashboard Tổng quan Điều hành Sản xuất cho Chủ xưởng &amp; Báo cáo Đối chiếu Hao hụt Gỗ Thực tế so với Định mức BOM (Iteration 3).
/// </summary>
[ApiController]
[Route("api/v1/production-dashboard")]
[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Leader},{RoleConstants.Qc}")]
public class ProductionDashboardController : ControllerBase
{
    private readonly IProductionDashboardService _productionDashboardService;

    public ProductionDashboardController(IProductionDashboardService productionDashboardService)
    {
        _productionDashboardService = productionDashboardService;
    }

    /// <summary>
    /// Tổng hợp bức tranh điều hành sản xuất toàn xưởng: số đơn hàng đang chạy, cảnh báo trễ ETD, mật độ WIP theo từng trạm công đoạn, số lô bị chặn Hold Point và tỷ lệ đạt chuẩn.
    /// </summary>
    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<ProductionOverviewDashboardDto>>> GetOverviewDashboard(
        CancellationToken cancellationToken = default)
    {
        var result = await _productionDashboardService.GetProductionOverviewDashboardAsync(cancellationToken);
        return Ok(ApiResponse<ProductionOverviewDashboardDto>.Ok(
            result,
            "Truy vấn dữ liệu Dashboard tổng quan điều hành sản xuất thành công."));
    }

    /// <summary>
    /// Phân tích và đối chiếu tỷ lệ hao hụt thực tế (từ nhật ký lỗi QC và tách nhánh 97-3) so với định mức hao hụt tự nhiên đã duyệt trong BOM (`ScrapRate`).
    /// </summary>
    [HttpGet("scrap-variance")]
    public async Task<ActionResult<ApiResponse<ScrapVarianceReportDto>>> GetScrapVarianceReport(
        [FromQuery] long? orderId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _productionDashboardService.GetScrapVarianceReportAsync(orderId, cancellationToken);
        return Ok(ApiResponse<ScrapVarianceReportDto>.Ok(
            result,
            "Truy vấn báo cáo đối chiếu tỷ lệ hao hụt thực tế so với định mức BOM thành công."));
    }

    /// <summary>
    /// Báo cáo Phân tích Lỗi Chất lượng theo Trạm máy / Công đoạn và theo Loại gỗ (Defect Analytics by Station / Wood Type - Iteration 3, QT-06, KBN01).
    /// Tổng hợp từ <c>defect_logs</c> để thống kê trạm máy phát sinh lỗi nhiều nhất (vd: trạm Xẻ gỗ phôi nứt, trạm Sơn màu bọt khí) và loại gỗ nhạy cảm lỗi nhất.
    /// </summary>
    [HttpGet("defect-analytics")]
    public async Task<ActionResult<ApiResponse<DefectAnalyticsReportDto>>> GetDefectAnalyticsReport(
        [FromQuery] QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _productionDashboardService.GetDefectAnalyticsReportAsync(filter, cancellationToken);
        return Ok(ApiResponse<DefectAnalyticsReportDto>.Ok(
            result,
            "Truy vấn báo cáo phân tích lỗi chất lượng theo trạm máy và loại gỗ thành công."));
    }

    /// <summary>
    /// Báo cáo Thống kê Tần suất Phá khóa Ngoại lệ Overdriver tại các trạm Hold Point (SC-20, KBN02, GBR-05).
    /// Tổng hợp từ <c>overdrive_logs</c> để thống kê số lần Quản lý / Chủ xưởng phải duyệt phá khóa Hold Point theo người duyệt, người yêu cầu và trạm dừng.
    /// </summary>
    [HttpGet("overdrive-analytics")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Leader}")]
    public async Task<ActionResult<ApiResponse<OverdriveAnalyticsReportDto>>> GetOverdriveAnalyticsReport(
        [FromQuery] QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _productionDashboardService.GetOverdriveAnalyticsReportAsync(filter, cancellationToken);
        return Ok(ApiResponse<OverdriveAnalyticsReportDto>.Ok(
            result,
            "Truy vấn báo cáo thống kê tần suất phá khóa ngoại lệ Overdriver thành công."));
    }

    /// <summary>
    /// Báo cáo Tổng hợp Phân tích Lỗi Chất lượng &amp; Thống kê Tần suất Phá khóa Ngoại lệ Overdriver trên một màn hình điều hành hợp nhất.
    /// </summary>
    [HttpGet("quality-and-overdrive-summary")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner}")]
    public async Task<ActionResult<ApiResponse<QualityAndOverdriveAnalyticsDto>>> GetQualityAndOverdriveSummary(
        [FromQuery] QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _productionDashboardService.GetQualityAndOverdriveSummaryAsync(filter, cancellationToken);
        return Ok(ApiResponse<QualityAndOverdriveAnalyticsDto>.Ok(
            result,
            "Truy vấn báo cáo tổng hợp phân tích chất lượng và tần suất phá khóa Overdriver thành công."));
    }

    /// <summary>
    /// Xuất tệp Excel (.xlsx) Báo cáo Thống kê Sản xuất, Tổng kết Tiến độ Đơn hàng &amp; Đối chiếu Hao hụt BOM chuẩn xưởng gỗ (EPPlus).
    /// </summary>
    [HttpGet("export/production-statistics-excel")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Leader}")]
    public async Task<IActionResult> ExportProductionStatisticsExcel(
        [FromQuery] QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var file = await _productionDashboardService.ExportProductionStatisticsExcelAsync(filter, cancellationToken);
        return File(file.FileContents, file.ContentType, file.FileName);
    }

    /// <summary>
    /// Xuất tệp Excel (.xlsx) Phiếu Kiểm định Chất lượng KCS &amp; Bảng Nhật ký Kiểm định Từng Công đoạn phục vụ lưu trữ nghiệm thu (EPPlus).
    /// </summary>
    [HttpGet("export/quality-inspection-excel")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Leader},{RoleConstants.Qc}")]
    public async Task<IActionResult> ExportQualityInspectionExcel(
        [FromQuery] QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var file = await _productionDashboardService.ExportQualityInspectionExcelAsync(filter, cancellationToken);
        return File(file.FileContents, file.ContentType, file.FileName);
    }

    /// <summary>
    /// Xuất tệp Excel (.xlsx) Lịch sử Điều độ Phân xưởng, Lộ trình Thẻ Lô Kanban &amp; Nhật ký Phá khóa Ngoại lệ Hold Point (EPPlus).
    /// </summary>
    [HttpGet("export/workshop-dispatch-history-excel")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Leader}")]
    public async Task<IActionResult> ExportWorkshopDispatchHistoryExcel(
        [FromQuery] QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var file = await _productionDashboardService.ExportWorkshopDispatchHistoryExcelAsync(filter, cancellationToken);
        return File(file.FileContents, file.ContentType, file.FileName);
    }

    /// <summary>
    /// Xuất Sổ tay Nghiệm thu Tổng hợp Toàn Xưởng (.xlsx) gồm đầy đủ 6 sheets: Tiến độ Đơn hàng, Đối chiếu Hao hụt, Phiếu KCS, Phân tích Lỗi, Điều độ Kanban &amp; Overdriver (EPPlus).
    /// </summary>
    [HttpGet("export/comprehensive-workbook-excel")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner}")]
    public async Task<IActionResult> ExportComprehensiveWorkshopWorkbookExcel(
        [FromQuery] QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var file = await _productionDashboardService.ExportComprehensiveWorkshopWorkbookExcelAsync(filter, cancellationToken);
        return File(file.FileContents, file.ContentType, file.FileName);
    }
}
