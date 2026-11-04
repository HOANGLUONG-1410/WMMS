namespace WMMS.Application.DTOs.InventoryPr;

// =============================================================================
// DTO phân hệ Tính toán Nhu cầu Nguyên vật liệu (MRP) & Phân bổ Tồn kho Tự động (SC-14)
// Tuân thủ BR-BOM-02 (Hao hụt), BR-INV-01 (Loại trừ tồn đã giữ chỗ),
// BR-INV-02 (Khóa phân bổ mềm), BR-PUR-01 (Gom PR tổng hợp theo ngày) & FIFO theo ETD.
// =============================================================================

/// <summary>
/// Chi tiết nhu cầu và trạng thái giữ chỗ của từng dòng sản phẩm đơn hàng (<c>OrderItem</c>) đối với một mã vật tư.
/// </summary>
public class MrpOrderItemBreakdownDto
{
    public long OrderId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public long? SpaceId { get; set; }
    public string SpaceKey { get; set; } = string.Empty;
    public long OrderItemId { get; set; }
    public long ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int OrderedQty { get; set; }
    public decimal ScrapRatePercent { get; set; }
    public decimal NetQtyPerUnit { get; set; }
    public decimal GrossDemandQty { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal IssuedQty { get; set; }
    public decimal UnfulfilledDemandQty { get; set; }
    public DateTime EtdDate { get; set; }
    public string OrderItemStatus { get; set; } = string.Empty;
}

/// <summary>
/// Kết quả phân tích MRP cho một mã nguyên vật liệu trên một Đơn hàng / Order Space hoặc toàn xưởng (SC-14).
/// </summary>
public class MrpMaterialRequirementLineDto
{
    public long MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialGroup { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string? DimensionSpec { get; set; }
    public bool FscCertified { get; set; }
    public decimal SafetyStockQty { get; set; }

    /// <summary>Tổng định mức tinh chưa tính hao hụt = Sum(NetQtyPerUnit * OrderedQty).</summary>
    public decimal TotalNetBomQty { get; set; }

    /// <summary>Tổng nhu cầu gộp có tính hao hụt = Sum(GrossTotalDemand).</summary>
    public decimal TotalGrossDemandQty { get; set; }

    /// <summary>Tổng tồn kho thực tế tại xưởng (Physical / On-Hand).</summary>
    public decimal PhysicalStockQty { get; set; }

    /// <summary>Lượng tồn kho đã bị khóa/giữ chỗ cho các đơn hàng khác (BR-INV-01: Bắt buộc loại trừ).</summary>
    public decimal AllocatedToOtherOrdersQty { get; set; }

    /// <summary>Lượng tồn kho đã được phân bổ mềm (Reserved + Issued) cho chính đơn hàng đang xét.</summary>
    public decimal AlreadyReservedForTargetQty { get; set; }

    /// <summary>Lượng tồn kho khả dụng tự do hiện tại (Available = max(0, Physical - Allocated)).</summary>
    public decimal AvailableStockQty { get; set; }

    /// <summary>Lượng có thể tự động phân bổ thêm ngay lập tức từ AvailableStockQty.</summary>
    public decimal AllocatableNowQty { get; set; }

    /// <summary>Lượng thiếu hụt ròng (Net Shortage) = max(0, TotalGrossDemandQty - AlreadyReservedForTargetQty - AllocatableNowQty).</summary>
    public decimal NetShortageQty { get; set; }

    /// <summary>Số lượng đề xuất đặt mua trên PR tổng hợp (NetShortageQty + bù mức tồn kho an toàn SafetyStockQty nếu thiếu).</summary>
    public decimal SuggestedPrOrderQty { get; set; }

    public bool IsShortage => NetShortageQty > 0m;

    /// <summary>Trạng thái đáp ứng vật tư: SUFFICIENT | PARTIAL_SHORTAGE | CRITICAL_SHORTAGE.</summary>
    public string SupplyStatus { get; set; } = "SUFFICIENT";

    public List<MrpOrderItemBreakdownDto> ItemBreakdowns { get; set; } = new();
}

/// <summary>
/// Báo cáo tổng hợp kết quả chạy thuật toán MRP (Material Requirements Planning) cho Đơn hàng / Order Space / Toàn nhà máy (SC-14).
/// </summary>
public class MrpAnalysisResultDto
{
    public string Scope { get; set; } = "ORDER"; // ORDER | ORDER_SPACE | ORDER_ITEM | FACTORY_ALL
    public long? OrderId { get; set; }
    public string? PoNumber { get; set; }
    public long? SpaceId { get; set; }
    public string? SpaceKey { get; set; }
    public string? CustomerName { get; set; }
    public DateTime? EtdDate { get; set; }
    public bool IsEtdAtRisk { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

    public int TotalOrderItemsCount { get; set; }
    public int ReadyOrderItemsCount { get; set; }
    public int WaitingMaterialItemsCount { get; set; }

    public int TotalDistinctMaterialsCount { get; set; }
    public int SufficientMaterialsCount { get; set; }
    public int ShortageMaterialsCount { get; set; }

    /// <summary>READY_TO_PRODUCE (Đủ 100% vật tư) hoặc WAITING_FOR_MATERIAL (Thiếu vật tư cần gom PR).</summary>
    public string OverallReadinessStatus { get; set; } = "WAITING_FOR_MATERIAL";

    public List<MrpMaterialRequirementLineDto> MaterialRequirements { get; set; } = new();
}

/// <summary>
/// Tùy chọn thực thi Phân bổ tồn kho tự động (Auto-Allocation Engine - SC-14).
/// </summary>
public class AutoAllocationRequestDto
{
    /// <summary>Tự động gom các vật tư thiếu hụt (NetShortageQty &gt; 0) vào Phiếu Yêu cầu Mua hàng Tổng hợp (Consolidated Daily PR) trong ngày (BR-PUR-01).</summary>
    public bool AutoQueueShortageToDailyPr { get; set; } = true;

    /// <summary>Ghi chú lý do chạy phân bổ tồn kho tự động.</summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Kết quả sau khi thực thi thuật toán Phân bổ tồn kho tự động (SC-14) cho Đơn hàng / Order Space / OrderItem.
/// </summary>
public class AutoAllocationResultDto
{
    public long? OrderId { get; set; }
    public string? PoNumber { get; set; }
    public long? SpaceId { get; set; }
    public string? SpaceKey { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

    /// <summary>READY_TO_PRODUCE (100% đủ vật tư) hoặc WAITING_FOR_MATERIAL (Còn thiếu vật tư).</summary>
    public string OverallReadinessStatus { get; set; } = string.Empty;

    public int TotalOrderItemsProcessed { get; set; }
    public int FullyAllocatedItemsCount { get; set; }
    public int WaitingMaterialItemsCount { get; set; }
    public int TotalMaterialAllocationsUpdated { get; set; }

    /// <summary>ID Phiếu PR tổng hợp trong ngày được khởi tạo hoặc cập nhật tự động nếu có thiếu hụt (BR-PUR-01).</summary>
    public long? ConsolidatedPrId { get; set; }
    public string? ConsolidatedPrCode { get; set; }
    public int QueuedShortageMaterialsCount { get; set; }

    public List<MaterialOrderAllocationDto> Allocations { get; set; } = new();
    public List<MrpMaterialRequirementLineDto> RemainingShortages { get; set; } = new();
}

/// <summary>
/// Yêu cầu chạy thuật toán Phân bổ lại tồn kho khả dụng theo độ ưu tiên hạn giao hàng ETD sớm nhất (FIFO by ETD - SC-14 &amp; SC-17).
/// </summary>
public class ReallocateByEtdRequestDto
{
    /// <summary>Danh sách ID vật tư vừa nhập kho cần phân bổ (để trống = quét toàn bộ vật tư có AvailableQty &gt; 0).</summary>
    public List<long>? MaterialIds { get; set; }

    /// <summary>Mã chứng từ tham chiếu (VD: GRN-2026-1104-001).</summary>
    public string? ReferenceCode { get; set; }
}

/// <summary>
/// Kết quả chạy thuật toán Phân bổ lại tồn kho theo thứ tự ưu tiên ETD sớm nhất (FIFO by ETD).
/// </summary>
public class ReallocateByEtdResultDto
{
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public string? ReferenceCode { get; set; }
    public int MaterialsReallocatedCount { get; set; }
    public int AllocationsIncreasedCount { get; set; }
    public int OrderItemsUnlockedToReadyCount { get; set; }
    public List<long> UnlockedOrderItemIds { get; set; } = new();
    public List<MaterialOrderAllocationDto> UpdatedAllocations { get; set; } = new();
}

/// <summary>
/// Yêu cầu giải phóng (Release) lượng vật tư đang giữ chỗ của một đơn hàng về lại tồn kho khả dụng (Available Stock).
/// </summary>
public class ReleaseOrderAllocationsRequestDto
{
    public string Reason { get; set; } = "Giải phóng vật tư giữ chỗ theo yêu cầu điều độ kế hoạch.";
}
