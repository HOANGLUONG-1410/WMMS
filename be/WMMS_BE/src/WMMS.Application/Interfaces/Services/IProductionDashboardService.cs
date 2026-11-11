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
}
