using WMMS.Application.DTOs.Orders;

namespace WMMS.Application.Interfaces.Services;

/// <summary>
/// Giao diện Dịch vụ Tiếp nhận Đơn hàng, Kiểm tra lưới Staging &amp; Khởi tạo Không gian Đơn hàng (Phân hệ ORD - BF-01, UC-30 đến UC-35, Report 4 Class Diagram <c>IOrderStagingService</c>).
/// </summary>
public interface IOrderIntakeService
{
    /// <summary>
    /// UC-30 &amp; UC-31: Đọc file Excel PO (.xlsx) hoặc nhận dữ liệu mẫu, tự động lọc bỏ trường vận tải/giá nhạy cảm (BR-ORD-01),
    /// kiểm tra định dạng từng ô (BR-ORD-02) và đối chiếu SKU với Visual Product Catalog.
    /// </summary>
    Task<StagingOrderGridResponseDto> ParseExcelPoToStagingGridAsync(
        Stream? excelStream,
        string fileName,
        string? poNumberOverride,
        long? customerIdOverride,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-32: Kiểm tra lại toàn bộ các dòng trên Staging Grid sau khi Sales sửa lỗi trực tiếp trên lưới (Inline Cell Editing - BR-ORD-02).
    /// </summary>
    Task<StagingOrderGridResponseDto> ValidateStagingGridAsync(
        ValidateStagingGridRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-33: Xác nhận đơn hàng đã sạch lỗi (0 red cells), lưu giao dịch nguyên tử vào <c>sales_orders</c>, <c>order_items</c>,
    /// tự động khởi tạo <c>order_spaces</c> độc lập và ghi <c>audit_logs</c> (BR-ORD-03).
    /// </summary>
    Task<OrderSpaceDetailDto> ConfirmSalesOrderAndInitSpaceAsync(
        ConfirmSalesOrderRequestDto request,
        long salesUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-34: Truy vấn danh sách đơn hàng bán và không gian sản xuất kèm các chỉ số thống kê KPI trên màn hình SCR-ORD-01.
    /// </summary>
    Task<SalesOrderListSummaryDto> GetSalesOrdersAsync(
        SalesOrderFilterDto filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-35: Xem chi tiết Order Space Dashboard (SCR-ORD-03) theo <c>spaceId</c>, <c>orderId</c>, hoặc mã <c>spaceKey</c>.
    /// </summary>
    Task<OrderSpaceDetailDto> GetOrderSpaceDetailAsync(
        string idOrKey,
        bool isViewOnlyRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách khách hàng B2B để hiển thị dropdown chọn khách hàng khi nạp PO.
    /// </summary>
    Task<IReadOnlyList<CustomerDto>> GetCustomersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-35 / 4.3.5: Đính kèm hoặc cập nhật đường dẫn bản vẽ kỹ thuật CAD/PDF cho Order Space hoặc dòng sản phẩm.
    /// </summary>
    Task<OrderSpaceDetailDto> AttachTechnicalDrawingAsync(
        string spaceIdOrKey,
        UpdateOrderSpaceDrawingDto dto,
        long actorUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}
