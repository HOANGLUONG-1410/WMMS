using WMMS.Application.DTOs.ProductionTracking;

namespace WMMS.Application.Interfaces.Services;

/// <summary>
/// Giao diện dịch vụ Quản lý Tiến độ Đơn hàng &amp; Tính toán Tỷ lệ Hoàn thành Tự động (Order Progress Tracking Engine - UC-64).
/// Tổng hợp tiến độ thực tế từ các thẻ/lô Kanban (`MAIN_CARD`, `REWORK_SUB_CARD`, `REPLENISH_LOT`) lên cấp `OrderItem` và `CustomerOrder`.
/// </summary>
public interface IOrderProgressService
{
    /// <summary>
    /// Truy vấn chi tiết tiến độ sản xuất của một đơn hàng cụ thể theo mã định danh đơn hàng (`orderId`).
    /// </summary>
    Task<OrderProductionProgressDto> GetOrderProgressByIdAsync(
        long orderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Truy vấn danh sách tiến độ sản xuất của toàn bộ các đơn hàng đang hoạt động trong xưởng, sắp xếp ưu tiên theo hạn giao hàng ETD.
    /// </summary>
    Task<List<OrderProductionProgressDto>> GetAllOrdersProgressAsync(
        string? statusFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đồng bộ hóa tự động trạng thái của dòng sản phẩm (`OrderItem`) và đơn hàng (`CustomerOrder`) dựa trên sản lượng thành phẩm thực tế của các lô `DONE`.
    /// </summary>
    Task<OrderProductionProgressDto> SynchronizeOrderProgressAsync(
        long orderId,
        long currentUserId,
        CancellationToken cancellationToken = default);
}
