namespace WMMS.Application.DTOs.InventoryPr;

/// <summary>
/// Bộ lọc danh sách Phiếu Yêu cầu Mua hàng gộp (UC-45, SCR-18).
/// </summary>
public class PurchaseRequestFilterDto
{
    /// <summary>Trạng thái PR: PENDING_APPROVAL, APPROVED, REJECTED, PO_ISSUED (bỏ trống = tất cả).</summary>
    public string? Status { get; set; }

    /// <summary>Từ ngày tổng hợp (AggregationDate, gồm cả ngày này).</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Đến ngày tổng hợp (AggregationDate, gồm cả ngày này).</summary>
    public DateTime? ToDate { get; set; }

    /// <summary>Từ khóa tìm theo mã PR hoặc mã/tên vật tư trong dòng PR.</summary>
    public string? SearchKeyword { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>Dòng tóm tắt một PR trong danh sách (UC-45).</summary>
public class PurchaseRequestListItemDto
{
    public long PrId { get; set; }
    public string PrCode { get; set; } = string.Empty;
    public DateTime AggregationDate { get; set; }
    public string PrStatus { get; set; } = string.Empty;
    public int ItemsCount { get; set; }
    public decimal TotalShortageQty { get; set; }
    public decimal TotalSuggestedOrderQty { get; set; }
    public long CreatedBy { get; set; }
    public string? CreatorName { get; set; }
    public long? ApprovedBy { get; set; }
    public string? ApproverName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Đơn hàng/dòng đơn hàng đóng góp lượng thiếu cho một dòng PR (truy vết nguồn, UC-46).</summary>
public class PurchaseRequestSourceOrderDto
{
    public long? OrderId { get; set; }
    public string? PoNumber { get; set; }
    public string? SpaceKey { get; set; }
    public long? OrderItemId { get; set; }
    public string? ProductSku { get; set; }
    public string? EtdDate { get; set; }
    public decimal ShortageQty { get; set; }
}

/// <summary>Một dòng vật tư trong PR gộp (UC-46, SCR-18 Field #4).</summary>
public class PurchaseRequestItemDto
{
    public long PrItemId { get; set; }
    public long MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal TotalShortageQty { get; set; }
    public decimal SuggestedOrderQty { get; set; }
    public decimal AvailableStockQty { get; set; }
    public string? EarliestEtd { get; set; }
    public List<PurchaseRequestSourceOrderDto> SourceOrders { get; set; } = new();
}

/// <summary>Chi tiết đầy đủ một PR gộp ngày (UC-46, SCR-18).</summary>
public class PurchaseRequestDetailDto : PurchaseRequestListItemDto
{
    public string? RejectionReason { get; set; }
    public int PurchaseOrdersCount { get; set; }

    /// <summary>Danh sách Order Space đóng góp nhu cầu cho PR (SCR-18 Field #3).</summary>
    public List<string> ContributingSpaceKeys { get; set; } = new();

    public List<PurchaseRequestItemDto> Items { get; set; } = new();
}

/// <summary>Yêu cầu phê duyệt PR (UC-47): ghi chú phê duyệt là tùy chọn.</summary>
public class ApprovePurchaseRequestDto
{
    public string? Notes { get; set; }
}

/// <summary>Yêu cầu từ chối PR (UC-48): lý do từ chối là bắt buộc.</summary>
public class RejectPurchaseRequestDto
{
    public string Reason { get; set; } = string.Empty;
}
