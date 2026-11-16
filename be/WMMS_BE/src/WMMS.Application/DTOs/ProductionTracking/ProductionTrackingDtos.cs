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

// ============================================================================
// MODULE 4: DEFECT ANALYTICS BY STATION / WOOD TYPE & OVERDRIVER FREQUENCY
// ============================================================================

/// <summary>
/// Bộ lọc truy vấn cho Báo cáo Phân tích Lỗi Chất lượng &amp; Thống kê Tần suất Phá khóa Ngoại lệ Overdriver (Iteration 3, SC-20).
/// </summary>
public class QualityAnalyticsFilterDto
{
    /// <summary>Lọc theo mã đơn hàng (OrderId), để trống nếu thống kê toàn xưởng.</summary>
    public long? OrderId { get; set; }

    /// <summary>Lọc từ ngày phát sinh (UTC).</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Lọc đến ngày phát sinh (UTC).</summary>
    public DateTime? ToDate { get; set; }

    /// <summary>Lọc theo loại gỗ (ví dụ: Oak, Ash, Walnut).</summary>
    public string? WoodType { get; set; }

    /// <summary>Lọc theo thứ tự trạm công đoạn (1..7).</summary>
    public int? StageSequence { get; set; }

    /// <summary>Số lượng bản ghi chi tiết tối đa trả về (mặc định 20).</summary>
    public int TopN { get; set; } = 20;
}

/// <summary>
/// Báo cáo Phân tích Lỗi Chất lượng theo Trạm máy / Công đoạn và theo Loại gỗ (Defect Analytics by Station / Wood Type - Iteration 3, QT-06, KBN01).
/// </summary>
public class DefectAnalyticsReportDto
{
    public DateTime GeneratedAt { get; set; }
    public int TotalDefectEventsCount { get; set; }
    public int TotalDefectUnits { get; set; }
    public int TotalReworkUnits { get; set; }
    public int TotalScrapReplenishUnits { get; set; }
    public decimal ReworkSharePercent { get; set; }
    public decimal ScrapSharePercent { get; set; }
    public string TopDefectStationName { get; set; } = string.Empty;
    public int TopDefectStationQty { get; set; }
    public decimal TopDefectStationSharePercent { get; set; }
    public string TopDefectWoodType { get; set; } = string.Empty;
    public int TopDefectWoodQty { get; set; }
    public string SummaryAssessment { get; set; } = string.Empty;
    public List<StationDefectAnalyticsDto> DefectsByStation { get; set; } = new();
    public List<WoodTypeDefectAnalyticsDto> DefectsByWoodType { get; set; } = new();
    public List<DefectCatalogSummaryDto> DefectsByCatalogCode { get; set; } = new();
}

/// <summary>
/// Thống kê lỗi chất lượng phát sinh tại từng trạm máy / công đoạn sản xuất (ví dụ: trạm Xẻ gỗ phôi nứt, trạm Sơn màu bọt khí).
/// </summary>
public class StationDefectAnalyticsDto
{
    public int StageSequence { get; set; }
    public string StationName { get; set; } = string.Empty;
    public int TotalDefectEventsCount { get; set; }
    public int TotalDefectQty { get; set; }
    public int ReworkQty { get; set; }
    public int ScrapReplenishQty { get; set; }
    public decimal ShareOfTotalDefectsPercent { get; set; }
    public string TopDefectCode { get; set; } = string.Empty;
    public string TopDefectName { get; set; } = string.Empty;
    public string RootCauseSummary { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public List<StationDefectCodeBreakdownDto> DefectBreakdown { get; set; } = new();
}

/// <summary>
/// Chi tiết phân rã từng mã lỗi trong một trạm máy công đoạn.
/// </summary>
public class StationDefectCodeBreakdownDto
{
    public string DefectCode { get; set; } = string.Empty;
    public string DefectName { get; set; } = string.Empty;
    public int OccurrencesCount { get; set; }
    public int DefectQty { get; set; }
    public decimal ShareInStationPercent { get; set; }
}

/// <summary>
/// Thống kê lỗi chất lượng phân nhóm theo loại gỗ nguyên liệu (Wood Species / Lumber Material).
/// </summary>
public class WoodTypeDefectAnalyticsDto
{
    public string WoodType { get; set; } = string.Empty;
    public int TotalDefectEventsCount { get; set; }
    public int TotalDefectQty { get; set; }
    public int ReworkQty { get; set; }
    public int ScrapReplenishQty { get; set; }
    public decimal ShareOfTotalDefectsPercent { get; set; }
    public int AffectedProductsCount { get; set; }
    public string MostCommonDefectCode { get; set; } = string.Empty;
    public string MostCommonDefectName { get; set; } = string.Empty;
    public string MostProblematicStationName { get; set; } = string.Empty;
}

/// <summary>
/// Thống kê Pareto theo danh mục mã lỗi chuẩn của nhà máy (<c>defect_catalogs</c>).
/// </summary>
public class DefectCatalogSummaryDto
{
    public string DefectCode { get; set; } = string.Empty;
    public string DefectName { get; set; } = string.Empty;
    public string StandardStage { get; set; } = string.Empty;
    public string RootCause { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public int OccurrencesCount { get; set; }
    public int TotalDefectQty { get; set; }
    public decimal ShareOfTotalDefectsPercent { get; set; }
}

/// <summary>
/// Báo cáo Thống kê Tần suất Phá khóa Ngoại lệ Overdriver tại các trạm Hold Point (SC-20, KBN02, GBR-05).
/// </summary>
public class OverdriveAnalyticsReportDto
{
    public DateTime GeneratedAt { get; set; }
    public int TotalOverdriveApprovalsCount { get; set; }
    public int TotalOverdrivenBatchesCount { get; set; }
    public int TotalSystemBatchesCount { get; set; }
    public decimal OverdrivenBatchRatePercent { get; set; }
    public int AffectedOrdersCount { get; set; }
    public int? MostOverdrivenHoldPointSeq { get; set; }
    public string MostOverdrivenHoldPointName { get; set; } = string.Empty;
    public int MostOverdrivenStageCount { get; set; }
    public string GovernanceEvaluation { get; set; } = string.Empty;
    public List<OverdriveByApproverDto> ByApprover { get; set; } = new();
    public List<OverdriveByRequesterDto> ByRequester { get; set; } = new();
    public List<OverdriveByHoldPointStageDto> ByHoldPointStage { get; set; } = new();
    public List<OverdriveEventItemDto> RecentOverdriveLogs { get; set; } = new();
}

/// <summary>
/// Thống kê tần suất phê duyệt phá khóa Hold Point theo từng Quản lý / Chủ xưởng (<c>ApprovedBy</c>).
/// </summary>
public class OverdriveByApproverDto
{
    public long ApproverUserId { get; set; }
    public string ApproverUsername { get; set; } = string.Empty;
    public string ApproverFullName { get; set; } = string.Empty;
    public int TotalApprovalsCount { get; set; }
    public int DistinctBatchesCount { get; set; }
    public decimal SharePercent { get; set; }
    public DateTime? LastApprovedAt { get; set; }
}

/// <summary>
/// Thống kê số lần gửi yêu cầu phá khóa Hold Point theo từng Tổ trưởng / Điều độ (<c>RequestedBy</c>).
/// </summary>
public class OverdriveByRequesterDto
{
    public long RequesterUserId { get; set; }
    public string RequesterUsername { get; set; } = string.Empty;
    public string RequesterFullName { get; set; } = string.Empty;
    public int TotalRequestsApprovedCount { get; set; }
    public int DistinctBatchesCount { get; set; }
    public decimal SharePercent { get; set; }
}

/// <summary>
/// Thống kê tần suất phá khóa theo từng trạm dừng kỹ thuật Hold Point (<c>HoldPointStageSeq</c>).
/// </summary>
public class OverdriveByHoldPointStageDto
{
    public int HoldPointStageSeq { get; set; }
    public string StageName { get; set; } = string.Empty;
    public int OverdriveCount { get; set; }
    public int DistinctBatchesCount { get; set; }
    public decimal SharePercent { get; set; }
    public List<string> SampleJustifications { get; set; } = new();
}

/// <summary>
/// Dòng nhật ký chi tiết của một lần phê duyệt phá khóa Hold Point.
/// </summary>
public class OverdriveEventItemDto
{
    public long OverdriveId { get; set; }
    public long BatchId { get; set; }
    public string BatchCode { get; set; } = string.Empty;
    public long OrderId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int HoldPointStageSeq { get; set; }
    public string StageName { get; set; } = string.Empty;
    public long RequestedBy { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public long ApprovedBy { get; set; }
    public string ApproverName { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public DateTime ApprovedAt { get; set; }
}

/// <summary>
/// DTO Tổng hợp Báo cáo Phân tích Lỗi Chất lượng (Defect Analytics) &amp; Thống kê Tần suất Phá khóa Ngoại lệ Overdriver.
/// </summary>
public class QualityAndOverdriveAnalyticsDto
{
    public DateTime GeneratedAt { get; set; }
    public DefectAnalyticsReportDto DefectAnalytics { get; set; } = new();
    public OverdriveAnalyticsReportDto OverdriveAnalytics { get; set; } = new();
}

// ============================================================================
// MODULE 5: EXCEL REPORT EXPORT MODELS (.XLSX - EPPLUS)
// ============================================================================

/// <summary>
/// Kết quả xuất tệp báo cáo Excel chuẩn xưởng gỗ (<c>.xlsx</c>).
/// </summary>
public class ExcelExportFileDto
{
    public byte[] FileContents { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

/// <summary>
/// Dòng dữ liệu xuất Excel cho Bảng Nhật ký Kiểm định Chất lượng KCS từng công đoạn phục vụ lưu trữ nghiệm thu.
/// </summary>
public class QcInspectionStageExportRowDto
{
    public long QcId { get; set; }
    public string BatchCode { get; set; } = string.Empty;
    public string BatchType { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string WoodType { get; set; } = string.Empty;
    public int StageSequence { get; set; }
    public string StageName { get; set; } = string.Empty;
    public bool IsHoldPoint { get; set; }
    public int InspectedQty { get; set; }
    public int PassQty { get; set; }
    public int FailQty { get; set; }
    public string ResultStatus { get; set; } = string.Empty;
    public string InspectorName { get; set; } = string.Empty;
    public DateTime InspectedAt { get; set; }
    public string DefectSummary { get; set; } = string.Empty;
}

/// <summary>
/// Dòng dữ liệu chi tiết lỗi xuất Excel trên Phiếu Kiểm định Chất lượng KCS.
/// </summary>
public class DefectLogExportRowDto
{
    public long DefectLogId { get; set; }
    public long QcId { get; set; }
    public string BatchCode { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string WoodType { get; set; } = string.Empty;
    public int StageSequence { get; set; }
    public string StageName { get; set; } = string.Empty;
    public string DefectCode { get; set; } = string.Empty;
    public string DefectName { get; set; } = string.Empty;
    public int DefectQty { get; set; }
    public string Disposition { get; set; } = string.Empty;
    public string SpawnedSubBatchCode { get; set; } = string.Empty;
    public string InspectorName { get; set; } = string.Empty;
    public DateTime InspectedAt { get; set; }
}

/// <summary>
/// Dòng dữ liệu xuất Excel cho Lịch sử Điều độ Phân xưởng &amp; Lộ trình Thẻ Lô Kanban.
/// </summary>
public class WorkshopDispatchExportRowDto
{
    public long BatchId { get; set; }
    public string BatchCode { get; set; } = string.Empty;
    public string BatchType { get; set; } = string.Empty;
    public string ParentBatchCode { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime EtdDate { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int InitialQty { get; set; }
    public int CurrentQty { get; set; }
    public int CurrentStageSeq { get; set; }
    public string CurrentStageName { get; set; } = string.Empty;
    public string BatchStatus { get; set; } = string.Empty;
    public bool IsOverdriver { get; set; }
    public int? HoldPointStageSeq { get; set; }
    public bool GoodsIssuedUnlocked { get; set; }
    public int PassedStagesCount { get; set; }
    public int TotalStagesCount { get; set; }
    public string RoutingTimelineSummary { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
