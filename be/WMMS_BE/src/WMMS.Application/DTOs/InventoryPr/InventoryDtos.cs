namespace WMMS.Application.DTOs.InventoryPr;

// =============================================================================
// DTO phân hệ Quản lý Tồn kho 3 cấp & Sổ kho điện tử (SC-12, SC-13)
// Khớp 100% với 14 bản ghi số dư kho ban đầu trong WMMS_DB_Test_Iteration1.sql.
// =============================================================================

/// <summary>
/// Bộ lọc &amp; phân trang danh sách tồn kho 3 cấp (SC-12: GET /api/v1/inventory/stocks).
/// Hỗ trợ lọc theo nhóm vật tư: LUMBER (Gỗ), HARDWARE (Phụ kiện), FINISH_PAINT (Sơn), PACKAGING (Đóng gói).
/// </summary>
public class InventoryStockFilterDto
{
    /// <summary>Từ khóa tìm theo mã vật tư (MaterialCode) hoặc tên vật tư (MaterialName).</summary>
    public string? Search { get; set; }

    /// <summary>Nhóm vật tư: LUMBER / Gỗ, HARDWARE / Phụ kiện, FINISH_PAINT / Sơn, PACKAGING / Đóng gói.</summary>
    public string? Category { get; set; }

    /// <summary>Alias cho Category nếu Frontend truyền group.</summary>
    public string? Group { get; set; }

    /// <summary>Chỉ lọc các vật tư đang cảnh báo dưới mức tồn kho an toàn (AvailableQty &lt; SafetyStockQty).</summary>
    public bool? BelowSafetyStock { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// DTO thông tin tồn kho 3 cấp của một vật tư (SC-12).
/// Phương trình cân đối: AvailableQty = max(0, PhysicalQty - AllocatedQty).
/// </summary>
public class InventoryStockDto
{
    public long StockId { get; set; }
    public long MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialGroup { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string? DimensionSpec { get; set; }
    public bool FscCertified { get; set; }
    public decimal SafetyStockQty { get; set; }
    public decimal PhysicalQty { get; set; }
    public decimal AllocatedQty { get; set; }
    public decimal AvailableQty { get; set; }
    public bool IsBelowSafetyStock => AvailableQty < SafetyStockQty;
    public string Status { get; set; } = "ACTIVE";
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Thông tin một đơn hàng đang giữ chỗ (Soft-Allocated / Reserved) vật tư trong kho (SC-12 Detail).
/// </summary>
public class MaterialOrderAllocationDto
{
    public long AllocationId { get; set; }
    public long OrderId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string SpaceKey { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public long OrderItemId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal RequiredQty { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal IssuedQty { get; set; }
    public decimal ShortageQty { get; set; }
    public string AllocationStatus { get; set; } = string.Empty;
    public DateTime EtdDate { get; set; }
    public DateTime AllocatedAt { get; set; }
}

/// <summary>
/// Chi tiết tồn kho 3 cấp của một vật tư kèm danh sách các đơn hàng đang giữ chỗ và lịch sử giao dịch gần nhất (SC-12).
/// </summary>
public class InventoryStockDetailDto : InventoryStockDto
{
    public List<MaterialOrderAllocationDto> Allocations { get; set; } = new();
    public List<InventoryTransactionDto> RecentTransactions { get; set; } = new();
}

/// <summary>
/// Bộ lọc &amp; phân trang truy vấn lịch sử giao dịch sổ kho (SC-13: GET /api/v1/inventory/ledger).
/// </summary>
public class InventoryLedgerFilterDto
{
    /// <summary>Lọc theo ID vật tư cụ thể.</summary>
    public long? MaterialId { get; set; }

    /// <summary>Loại giao dịch: INITIAL_IMPORT, IN_RECEIPT, OUT_ISSUE, ADJUST.</summary>
    public string? TxType { get; set; }

    /// <summary>Từ khóa tìm theo mã tham chiếu (ReferenceCode), mã vật tư hoặc tên vật tư.</summary>
    public string? Search { get; set; }

    /// <summary>Từ ngày (UTC / Local date).</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Đến ngày (UTC / Local date).</summary>
    public DateTime? EndDate { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// DTO bản ghi giao dịch sổ kho điện tử từ bảng <c>inventory_transactions</c> (SC-13).
/// </summary>
public class InventoryTransactionDto
{
    public long TransactionId { get; set; }
    public long MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialGroup { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string TxType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? ReferenceCode { get; set; }
    public DateTime CreatedAt { get; set; }
}
