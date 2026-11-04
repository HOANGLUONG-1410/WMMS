namespace WMMS.Application.DTOs.Warehouse;

// =============================================================================
// DTO Nhập kho theo đơn mua hàng PO (GRN) & Phân bổ tự động vào Staging (SC-17, UC-56, UC-57, BR-WHS-01)
// =============================================================================

/// <summary>Một dòng vật tư thực nhận trong phiếu nhập kho (GRN).</summary>
public class GoodsReceiptLineRequestDto
{
    /// <summary>Mã vật tư (phải thuộc đơn PO).</summary>
    public long MaterialId { get; set; }

    /// <summary>Số lượng thực nhận (ReceivedQty). Không được âm hoặc để trống (MSG-WHS-02).</summary>
    public decimal ReceivedQty { get; set; }
}

/// <summary>Yêu cầu Commit phiếu nhập kho theo PO (POST /api/v1/warehouse/goods-receipts).</summary>
public class CommitGoodsReceiptRequestDto
{
    public long PoId { get; set; }
    public List<GoodsReceiptLineRequestDto> Lines { get; set; } = new();

    /// <summary>Ghi chú / số hóa đơn giao hàng của NCC (tùy chọn).</summary>
    public string? Note { get; set; }
}

/// <summary>Kết quả đối chiếu số lượng thực nhận với đơn PO cho từng dòng.</summary>
public class GoodsReceiptLineResultDto
{
    public long MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal OrderedQty { get; set; }
    public decimal PreviouslyReceivedQty { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal TotalReceivedQty { get; set; }
    public decimal RemainingQty { get; set; }
    public decimal PhysicalQtyAfter { get; set; }
    public decimal AllocatedQtyAfter { get; set; }
    public decimal AvailableQtyAfter { get; set; }
}

/// <summary>Một lượt phân bổ tự động vật tư mới về vào khu vực Staging cho đơn hàng đang chờ.</summary>
public class AutoAllocationResultDto
{
    public long AllocationId { get; set; }
    public long OrderId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string SpaceKey { get; set; } = string.Empty;
    public DateTime EtdDate { get; set; }
    public long OrderItemId { get; set; }
    public long MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public decimal AllocatedNowQty { get; set; }
    public decimal RequiredQty { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal ShortageQty { get; set; }
}

/// <summary>Kết quả Commit GRN: đối chiếu PO, tồn kho mới, danh sách phân bổ tự động.</summary>
public class GoodsReceiptResultDto
{
    public long GrnId { get; set; }
    public string GrnCode { get; set; } = string.Empty;
    public long PoId { get; set; }
    public string PoCode { get; set; } = string.Empty;
    public string PoStatus { get; set; } = string.Empty;
    public string GrnStatus { get; set; } = string.Empty;
    public DateTime CommittedAt { get; set; }
    public List<GoodsReceiptLineResultDto> Lines { get; set; } = new();
    public List<AutoAllocationResultDto> Allocations { get; set; } = new();

    /// <summary>Số đơn hàng (order item) được phân bổ vật tư mới về.</summary>
    public int AllocatedOrderCount { get; set; }

    /// <summary>Danh sách mã đơn hàng đã đủ 100% vật tư → BOM chuyển READY_FOR_PRODUCTION.</summary>
    public List<string> ReadyOrderPoNumbers { get; set; } = new();
}

/// <summary>Dòng chi tiết đơn PO còn chờ nhận hàng.</summary>
public class OpenPurchaseOrderLineDto
{
    public long MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal OrderedQty { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal RemainingQty { get; set; }
}

/// <summary>Đơn PO đang mở (ISSUED / PARTIALLY_RECEIVED) để Thủ kho tra cứu khi nhập kho.</summary>
public class OpenPurchaseOrderDto
{
    public long PoId { get; set; }
    public string PoCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string PoStatus { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime ExpectedArrivalDate { get; set; }
    public List<OpenPurchaseOrderLineDto> Lines { get; set; } = new();
}
