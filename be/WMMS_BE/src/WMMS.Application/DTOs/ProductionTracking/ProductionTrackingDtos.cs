namespace WMMS.Application.DTOs.ProductionTracking;

// ============================================================================
// MODULE 1: FINISHED GOODS RECEIPT (HOÀN TẤT LÔ SẢN XUẤT & NHẬP KHO THÀNH PHẨM)
// ============================================================================

/// <summary>
/// Yêu cầu xác nhận hoàn tất công đoạn đóng gói cuối cùng và nhập kho thành phẩm cho lô sản xuất (UC-63, UC-64).
/// </summary>
public class CompleteFinishedBatchRequestDto
{
    /// <summary>Số lượng thành phẩm thực tế bàn giao nhập kho (nếu để null sẽ mặc định lấy CurrentQty đạt chuẩn của lô).</summary>
    public int? ReceivedQty { get; set; }

    /// <summary>Vị trí lưu kho thành phẩm hoặc khu vực chờ xuất hàng (tùy chọn).</summary>
    public string? WarehouseLocation { get; set; }

    /// <summary>Ghi chú bàn giao nhập kho thành phẩm của Thủ kho / Tổ trưởng.</summary>
    public string? HandoverNote { get; set; }
}

/// <summary>
/// Kết quả xác nhận hoàn tất lô sản xuất và tự động đồng bộ tiến độ đơn hàng (UC-63, UC-64).
/// </summary>
public class FinishedGoodsReceiptResultDto
{
    public long BatchId { get; set; }
    public string BatchCode { get; set; } = string.Empty;
    public string BatchType { get; set; } = string.Empty;
    public string BatchStatus { get; set; } = string.Empty;
    public int CompletedBatchQty { get; set; }
    public long OrderItemId { get; set; }
    public long OrderId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int OrderedQty { get; set; }
    public int TotalCompletedQtyForOrderItem { get; set; }
    public int RemainingQtyForOrderItem { get; set; }
    public decimal OrderItemCompletionPercent { get; set; }
    public string OrderItemStatus { get; set; } = string.Empty;
    public string SalesOrderStatus { get; set; } = string.Empty;
    public bool IsOrderItemFullyCompleted { get; set; }
    public bool IsSalesOrderFullyCompleted { get; set; }
    public string? WarehouseLocation { get; set; }
    public string? HandoverNote { get; set; }
    public DateTime CompletedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Thông tin tóm tắt lô sản xuất sẵn sàng nhập kho thành phẩm hoặc đã hoàn tất nhập kho.
/// </summary>
public class FinishedGoodsBatchSummaryDto
{
    public long BatchId { get; set; }
    public string BatchCode { get; set; } = string.Empty;
    public string BatchType { get; set; } = string.Empty;
    public string BatchStatus { get; set; } = string.Empty;
    public long OrderItemId { get; set; }
    public long OrderId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int InitialQty { get; set; }
    public int CurrentQty { get; set; }
    public int OrderedQty { get; set; }
    public string CurrentStageCode { get; set; } = string.Empty;
    public bool IsAllRoutingStepsPassed { get; set; }
    public bool IsReadyForFinishedGoodsReceipt { get; set; }
    public DateTime EtdDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ============================================================================
// MODULE 2: ORDER PROGRESS TRACKING ENGINE (THEO DÕI TIẾN ĐỘ ĐƠN HÀNG TỰ ĐỘNG)
// ============================================================================

/// <summary>
/// Chi tiết tiến độ sản xuất của một đơn hàng bán (Sales Order) và toàn bộ các dòng sản phẩm con.
/// </summary>
public class OrderProductionProgressDto
{
    public long OrderId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public long? SpaceId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime EtdDate { get; set; }
    public int DaysUntilEtd { get; set; }
    public string EtdRiskLevel { get; set; } = string.Empty; // ON_TRACK, WARNING, OVERDUE, COMPLETED
    public int TotalOrderedUnits { get; set; }
    public int TotalCompletedUnits { get; set; }
    public int TotalInProgressUnits { get; set; }
    public decimal OverallCompletionPercent { get; set; }
    public int TotalOrderItems { get; set; }
    public int CompletedOrderItems { get; set; }
    public List<OrderItemProgressDto> Items { get; set; } = new();
}

/// <summary>
/// Tiến độ sản xuất chi tiết của từng dòng sản phẩm (OrderItem) trên đơn hàng.
/// </summary>
public class OrderItemProgressDto
{
    public long OrderItemId { get; set; }
    public long ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int OrderedQty { get; set; }
    public int CompletedQty { get; set; }
    public int InProgressQty { get; set; }
    public int BlockedQty { get; set; }
    public int RemainingQty { get; set; }
    public decimal CompletionPercent { get; set; }
    public string ItemStatus { get; set; } = string.Empty;
    public int TotalBatches { get; set; }
    public int DoneBatches { get; set; }
    public int BlockedBatches { get; set; }
    public List<BatchStageProgressDto> Batches { get; set; } = new();
}

/// <summary>
/// Tiến độ công đoạn của từng thẻ/lô Kanban thuộc dòng sản phẩm.
/// </summary>
public class BatchStageProgressDto
{
    public long BatchId { get; set; }
    public string BatchCode { get; set; } = string.Empty;
    public string BatchType { get; set; } = string.Empty;
    public string BatchStatus { get; set; } = string.Empty;
    public int InitialQty { get; set; }
    public int CurrentQty { get; set; }
    public string CurrentStageCode { get; set; } = string.Empty;
    public int PassedStepsCount { get; set; }
    public int TotalStepsCount { get; set; }
    public decimal StageProgressPercent { get; set; }
}

// ============================================================================
// MODULE 3: PRODUCTION DASHBOARD & SCRAP VARIANCE ANALYSIS (BÁO CÁO CHỦ XƯỞNG)
// ============================================================================

/// <summary>
/// Tổng quan điều hành sản xuất thời gian thực dành cho Chủ xưởng (Owner) và Kế hoạch (Planner).
/// </summary>
public class ProductionOverviewDashboardDto
{
    public int TotalActiveOrders { get; set; }
    public int TotalCompletedOrders { get; set; }
    public int TotalInProductionOrders { get; set; }
    public int OverdueOrdersCount { get; set; }
    public int EtdWarningOrdersCount { get; set; }
    public int TotalBatches { get; set; }
    public int ActiveBatchesCount { get; set; }
    public int DoneBatchesCount { get; set; }
    public int HoldPointBlockedBatchesCount { get; set; }
    public int ReworkAndReplenishBatchesCount { get; set; }
    public decimal OverallYieldRatePercent { get; set; }
    public decimal OverallDefectRatePercent { get; set; }
    public List<StageWipSummaryDto> WorkInProgressByStage { get; set; } = new();
    public List<OrderProductionProgressDto> PriorityOrders { get; set; } = new();
}

/// <summary>
/// Thống kê mật độ bán thành phẩm đang làm dở (WIP - Work In Progress) tại từng trạm công đoạn sản xuất.
/// </summary>
public class StageWipSummaryDto
{
    public string StageCode { get; set; } = string.Empty;
    public int ActiveBatchesCount { get; set; }
    public int BlockedBatchesCount { get; set; }
    public int TotalUnitsInStage { get; set; }
}

/// <summary>
/// Báo cáo đối chiếu tỷ lệ hao hụt sản xuất thực tế (QC Defect &amp; 97-3 Branching) so với định mức hao hụt tự nhiên trong BOM (BR-002).
/// </summary>
public class ScrapVarianceReportDto
{
    public DateTime GeneratedAt { get; set; }
    public int TotalAnalyzedOrderItems { get; set; }
    public decimal AveragePlannedBomScrapRatePercent { get; set; }
    public decimal AverageActualDefectScrapRatePercent { get; set; }
    public decimal OverallScrapVariancePercent { get; set; }
    public int ExceededThresholdItemsCount { get; set; }
    public List<OrderItemScrapVarianceDto> Items { get; set; } = new();
}

/// <summary>
/// Chi tiết đối chiếu định mức hao hụt BOM so với tỷ lệ lỗi/hao hụt thực tế trên từng dòng sản phẩm sản xuất.
/// </summary>
public class OrderItemScrapVarianceDto
{
    public long OrderItemId { get; set; }
    public long OrderId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int OrderedQty { get; set; }
    public int TotalInitialBatchQty { get; set; }
    public int TotalGoodCompletedQty { get; set; }
    public int TotalDefectQty { get; set; }
    public int TotalReworkQty { get; set; }
    public int TotalScrappedQty { get; set; }
    public decimal PlannedBomScrapRatePercent { get; set; }
    public decimal ActualDefectRatePercent { get; set; }
    public decimal ActualIrrecoverableScrapRatePercent { get; set; }
    public decimal ScrapVariancePercent { get; set; }
    public bool IsExceedingPlannedScrap { get; set; }
    public string VarianceEvaluation { get; set; } = string.Empty;
}
