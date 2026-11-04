using WMMS.Application.DTOs.InventoryPr;

namespace WMMS.Application.Interfaces.Services;

/// <summary>
/// Giao diện Dịch vụ Lõi Tính toán Nhu cầu Nguyên vật liệu (MRP) &amp; Phân bổ Tồn kho Tự động (SC-14).
/// Tuân thủ đầy đủ các quy tắc nghiệp vụ:
/// - BR-BOM-02: Tính Gross Demand có cộng hệ số hao hụt tự nhiên.
/// - BR-INV-01: Loại trừ phần tồn kho đã giữ chỗ (Allocated) của đơn hàng khác khi tính Available Stock.
/// - BR-INV-02: Tự động khóa mềm (Soft-Allocate) tồn kho khả dụng vào bảng <c>material_allocations</c>.
/// - BR-PUR-01: Tự động gom lượng vật tư thiếu hụt (Net Shortage) vào Phiếu PR Tổng hợp trong ngày (<c>consolidated_prs</c>).
/// - FIFO by ETD: Tự động phân bổ tồn kho mới nhập cho các đơn hàng đang chờ vật tư theo thứ tự ưu tiên ETD gần nhất.
/// </summary>
public interface IMrpAllocationService
{
    /// <summary>
    /// SC-14: Phân tích MRP chi tiết (Gross Demand, Available Stock loại trừ giữ chỗ đơn khác, Net Shortage) cho một Đơn hàng (<c>sales_orders</c>).
    /// </summary>
    Task<MrpAnalysisResultDto> AnalyzeOrderMrpAsync(
        long orderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// SC-14: Phân tích MRP chi tiết cho một Không gian Đơn hàng (<c>order_spaces</c>).
    /// </summary>
    Task<MrpAnalysisResultDto> AnalyzeOrderSpaceMrpAsync(
        long spaceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// SC-14: Phân tích MRP tổng thể trên toàn bộ các đơn hàng đang hoạt động trong nhà máy.
    /// </summary>
    Task<MrpAnalysisResultDto> AnalyzeFactoryMrpAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// SC-14: Thực thi thuật toán Phân bổ tồn kho tự động (Auto-Allocation) cho toàn bộ dòng sản phẩm của một Đơn hàng (<c>sales_orders</c>).
    /// Tự động khóa tồn kho khả dụng, chuyển trạng thái sang READY_PROD (nếu đủ 100%) hoặc WAITING_MAT (nếu thiếu),
    /// và tự động gom phần thiếu hụt vào PR tổng hợp trong ngày (BR-PUR-01).
    /// </summary>
    Task<AutoAllocationResultDto> ExecuteAutoAllocationForOrderAsync(
        long orderId,
        AutoAllocationRequestDto? request,
        long currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// SC-14: Thực thi thuật toán Phân bổ tồn kho tự động (Auto-Allocation) cho một Không gian Đơn hàng (<c>order_spaces</c>).
    /// </summary>
    Task<AutoAllocationResultDto> ExecuteAutoAllocationForOrderSpaceAsync(
        long spaceId,
        AutoAllocationRequestDto? request,
        long currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// SC-14: Thực thi thuật toán Phân bổ tồn kho tự động cho một dòng sản phẩm đơn hàng (<c>order_items</c>) cụ thể.
    /// </summary>
    Task<AutoAllocationResultDto> ExecuteAutoAllocationForOrderItemAsync(
        long orderItemId,
        AutoAllocationRequestDto? request,
        long currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// SC-14 &amp; SC-17: Tự động phân bổ lại tồn kho khả dụng (Available Stock) cho các đơn hàng đang thiếu vật tư
    /// theo thứ tự ưu tiên hạn giao hàng ETD sớm nhất (FIFO by Order ETD).
    /// </summary>
    Task<ReallocateByEtdResultDto> ReallocateAvailableStockByEtdAsync(
        ReallocateByEtdRequestDto request,
        long currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Giải phóng (Release) phần vật tư đang giữ chỗ (RESERVED) của một đơn hàng về lại tồn kho khả dụng (Available Stock).
    /// </summary>
    Task<List<MaterialOrderAllocationDto>> ReleaseOrderAllocationsAsync(
        long orderId,
        string reason,
        long currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách chi tiết các bản ghi giữ chỗ vật tư (<c>material_allocations</c>) của một Đơn hàng.
    /// </summary>
    Task<List<MaterialOrderAllocationDto>> GetAllocationsByOrderIdAsync(
        long orderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách chi tiết các bản ghi giữ chỗ vật tư (<c>material_allocations</c>) của một Không gian Đơn hàng (<c>OrderSpace</c>).
    /// </summary>
    Task<List<MaterialOrderAllocationDto>> GetAllocationsBySpaceIdAsync(
        long spaceId,
        CancellationToken cancellationToken = default);
}
