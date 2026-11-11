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
}
