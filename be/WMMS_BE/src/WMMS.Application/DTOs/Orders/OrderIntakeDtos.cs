namespace WMMS.Application.DTOs.Orders;

/// <summary>
/// DTO Khách hàng đặt hàng sản xuất (UC-30..UC-35, SCR-ORD-01).
/// </summary>
public class CustomerDto
{
    public long CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Country { get; set; }
}

/// <summary>
/// DTO Lỗi kiểm tra định dạng trên từng ô của bảng Staging Grid (UC-30, UC-31, UC-32, BR-ORD-02).
/// Dùng để Frontend tô đỏ ô lỗi (<c>Red Cells = Syntax / Format Error</c>) và hiển thị tooltip hướng dẫn sửa.
/// </summary>
public class StagingCellErrorDto
{
    public string FieldName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// DTO Một dòng sản phẩm trên bảng Safe Excel PO Staging Grid (UC-30, UC-31, UC-32, SCR-ORD-02).
/// Đã loại bỏ các trường thương mại nhạy cảm (Container No, Shipping Port, PI, Unit Prices) theo BR-ORD-01.
/// </summary>
public class StagingOrderRowDto
{
    public int RowNumber { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductDescription { get; set; } = string.Empty;
    public string DimensionsMm { get; set; } = string.Empty;
    public decimal? LengthMm { get; set; }
    public decimal? WidthMm { get; set; }
    public decimal? HeightMm { get; set; }
    public string FinishColor { get; set; } = string.Empty;
    public int OrderQty { get; set; }
    public string TargetEtd { get; set; } = string.Empty;
    public bool FscRequired { get; set; } = true;
    public string? TechnicalDrawingName { get; set; }
    public string? TechnicalDrawingUrl { get; set; }

    /// <summary>ID sản phẩm khớp trong Visual Catalog (nếu có).</summary>
    public long? MatchedProductId { get; set; }

    /// <summary>Cờ đánh dấu mã SKU mới chưa có trong Visual Catalog (<c>[New Product]</c> - UC-30).</summary>
    public bool IsNewProduct { get; set; }

    /// <summary>Trạng thái hợp lệ của toàn bộ dòng (true khi không còn ô lỗi màu đỏ).</summary>
    public bool IsRowValid { get; set; }

    /// <summary>Danh sách các ô bị lỗi cú pháp / định dạng trên dòng này (BR-ORD-02).</summary>
    public List<StagingCellErrorDto> CellErrors { get; set; } = new();
}

/// <summary>
/// DTO Kết quả phân tích &amp; kiểm tra bảng Safe Excel PO Staging Grid (UC-30, UC-31, UC-32, SCR-ORD-02).
/// </summary>
public class StagingOrderGridResponseDto
{
    public string FileName { get; set; } = string.Empty;
    public string SheetName { get; set; } = "OrderDetails";
    public string PoNumber { get; set; } = string.Empty;
    public long? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public List<string> StrippedCommercialFields { get; set; } = new()
    {
        "Container No",
        "Shipping Port",
        "Proforma Invoice (PI)",
        "Unit Prices (CBD)"
    };
    public int TotalRows { get; set; }
    public int MatchedCatalogCount { get; set; }
    public int NewProductCount { get; set; }
    public int ErrorCellCount { get; set; }
    public int InvalidRowCount { get; set; }

    /// <summary>Chỉ khi <c>CanConfirm == true</c> (0 lỗi đỏ) thì nút "Confirm &amp; Create Order Space" mới được bật (BR-ORD-02).</summary>
    public bool CanConfirm { get; set; }
    public string SummaryMessage { get; set; } = string.Empty;
    public List<StagingOrderRowDto> Rows { get; set; } = new();
}

/// <summary>
/// DTO Yêu cầu kiểm tra lại toàn bộ lưới Staging sau khi Sales sửa inline các ô lỗi (UC-32: Correct Staging Order Cell, nút "Validate Grid Again").
/// </summary>
public class ValidateStagingGridRequestDto
{
    public string? FileName { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public long? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public List<StagingOrderRowDto> Rows { get; set; } = new();
}

/// <summary>
/// DTO Yêu cầu xác nhận đơn hàng sạch từ Staging Grid và khởi tạo không gian làm việc Order Space độc lập (UC-33: Confirm Sales Order, BR-ORD-03).
/// </summary>
public class ConfirmSalesOrderRequestDto
{
    public string PoNumber { get; set; } = string.Empty;
    public long? CustomerId { get; set; }
    public string? CustomerCode { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerCountry { get; set; }
    public DateTime? OrderDate { get; set; }
    public DateTime? TargetEtd { get; set; }
    public string? DrawingFolderUrl { get; set; }
    public string? Notes { get; set; }
    public List<StagingOrderRowDto> Rows { get; set; } = new();
}

/// <summary>
/// DTO Bộ lọc truy vấn danh sách đơn hàng bán B2B (UC-34: View Sales Order List, SCR-ORD-01).
/// </summary>
public class SalesOrderFilterDto
{
    public string? Status { get; set; }
    public string? SearchKeyword { get; set; }
    public bool? UrgentOnly { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// DTO Thống kê tổng quan trên đầu màn hình Sales Orders Directory (SCR-ORD-01) kèm danh sách phân trang.
/// </summary>
public class SalesOrderListSummaryDto
{
    public int ActiveOrdersCount { get; set; }
    public int InProductionCount { get; set; }
    public int BomSettingCount { get; set; }
    public int NewlyReceivedCount { get; set; }
    public int UrgentEtdCount { get; set; }
    public int TotalUnitsOnFloor { get; set; }
    public List<SalesOrderListItemDto> Items { get; set; } = new();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

/// <summary>
/// DTO Một dòng đơn hàng trên bảng Official Orders &amp; Production Spaces (UC-34, SCR-ORD-01).
/// </summary>
public class SalesOrderListItemDto
{
    public long OrderId { get; set; }
    public long SpaceId { get; set; }
    public string OrderSpaceKey { get; set; } = string.Empty;
    public string SpaceName { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerCountry { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime EtdDate { get; set; }
    public int DaysRemaining { get; set; }
    public bool IsUrgentEtd { get; set; }
    public int TotalOrderedUnits { get; set; }
    public string LineItemsVolumeSummary { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public string LifecycleStatusBadge { get; set; } = string.Empty;
    public decimal OverallProgressPct { get; set; }
}

/// <summary>
/// DTO Chi tiết dòng sản phẩm đặt hàng bên trong Order Space Detail Dashboard (UC-35, SCR-ORD-03).
/// </summary>
public class OrderSpaceLineItemDto
{
    public long OrderItemId { get; set; }
    public long ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string DimensionsSpec { get; set; } = string.Empty;
    public string WoodSpecies { get; set; } = string.Empty;
    public string FinishSpec { get; set; } = string.Empty;
    public int OrderedQty { get; set; }
    public bool FscCertifiedReq { get; set; }
    public string? AttachedCadFileName { get; set; }
    public string? AttachedCadUrl { get; set; }
    public long? BomId { get; set; }
    public decimal? ScrapRatePct { get; set; }
    public string ItemStatus { get; set; } = string.Empty;
    public string BomLifecycleBadge { get; set; } = string.Empty;
}

/// <summary>
/// DTO Mốc hoạt động lịch sử trong Order Space (Recent Space Activity &amp; Milestones - SCR-ORD-03).
/// </summary>
public class OrderSpaceMilestoneDto
{
    public string Title { get; set; } = string.Empty;
    public string ActorAndTimestamp { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DotColor { get; set; } = "blue";
    public DateTime OccurredAt { get; set; }
}

/// <summary>
/// DTO Chi tiết Không gian Đơn hàng (Order Space Detail Dashboard - UC-35, SCR-ORD-03).
/// </summary>
public class OrderSpaceDetailDto
{
    public long SpaceId { get; set; }
    public string SpaceKey { get; set; } = string.Empty;
    public string SpaceName { get; set; } = string.Empty;
    public long OrderId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerCountry { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime EtdDate { get; set; }
    public int DaysRemaining { get; set; }
    public bool IsUrgentEtd { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public string LifecycleStatusBadge { get; set; } = string.Empty;
    public bool FscCertified100 { get; set; }
    public string? DrawingFolderUrl { get; set; }

    // 4 KPI Cards trên màn hình SCR-ORD-03
    public int TotalOrderedUnits { get; set; }
    public string TotalOrderedSubtitle { get; set; } = string.Empty;
    public string BomStatusTitle { get; set; } = string.Empty;
    public string BomStatusSubtitle { get; set; } = string.Empty;
    public string MaterialAllocationTitle { get; set; } = string.Empty;
    public string MaterialAllocationSubtitle { get; set; } = string.Empty;
    public decimal FloorProgressPct { get; set; }
    public string FloorProgressSubtitle { get; set; } = string.Empty;

    /// <summary>BR-ORD-04: True nếu người dùng hiện tại chỉ có quyền Xem (ví dụ Material Planning Staff).</summary>
    public bool IsViewOnlyForCurrentUser { get; set; }

    public List<OrderSpaceLineItemDto> LineItems { get; set; } = new();
    public List<OrderSpaceMilestoneDto> Milestones { get; set; } = new();
}

/// <summary>
/// DTO Cập nhật đính kèm bản vẽ kỹ thuật CAD / PDF cho Order Space hoặc từng dòng Order Item (UC Diagrams 4.3.5: Attach Technical Drawings).
/// </summary>
public class UpdateOrderSpaceDrawingDto
{
    public long? OrderItemId { get; set; }
    public string DrawingUrl { get; set; } = string.Empty;
}
