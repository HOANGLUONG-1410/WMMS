namespace WMMS.Application.DTOs.KanbanQc;

/// <summary>
/// DTO Yêu cầu chia lô sản xuất thông minh và phát hành lệnh sản xuất (SC-19, UC-62).
/// Hỗ trợ chia tự động theo công suất trạm (Station Batch Capacity) hoặc mảng chia lô tùy biến (CustomBatchSplits).
/// </summary>
public class GenerateBatchesRequestDto
{
    /// <summary>ID dòng sản phẩm trong đơn hàng (<c>OrderItemId</c>).</summary>
    public long OrderItemId { get; set; }

    /// <summary>
    /// Công suất trạm tối đa / Kích thước lô định mức (ví dụ: 50, 100 sản phẩm/lô).
    /// Nếu không truyền hoặc &lt;= 0, hệ thống sử dụng công suất trạm chuẩn là 100 SP/lô.
    /// </summary>
    public int? MaxBatchSize { get; set; } = 100;

    /// <summary>
    /// Danh sách kích thước tùy biến cho từng lô (ví dụ: đơn 200 SP chia thành [100, 100] hoặc [80, 80, 40]).
    /// Tổng các phần tử phải khớp chính xác với số lượng cần phát hành.
    /// </summary>
    public List<int>? CustomBatchSplits { get; set; }

    /// <summary>
    /// Cho phép chạy trước theo cơ chế Overdrive nếu thiếu vật tư phụ trợ (ốc vít/sơn) nhưng cận ETD (QT-06, KBN02).
    /// </summary>
    public bool AllowOverdrive { get; set; } = false;

    /// <summary>Lý do giải trình bắt buộc nếu kích hoạt chế độ Overdrive hoặc ghi chú phân xưởng.</summary>
    public string? Justification { get; set; }
}

/// <summary>
/// DTO Kết quả phát hành Lệnh sản xuất tổng hợp kèm danh sách thẻ Lô Kanban (SC-19).
/// </summary>
public class ProductionOrderSummaryDto
{
    public long OrderId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string SpaceKey { get; set; } = string.Empty;
    public long OrderItemId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int TotalOrderedQty { get; set; }
    public int ReleasedQty { get; set; }
    public int TotalBatchesCount { get; set; }
    public int StationCapacity { get; set; }
    public bool IsOverdriver { get; set; }
    public int? HoldPointStageSeq { get; set; }
    public bool GoodsIssuedUnlocked { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<ProductionBatchDto> Batches { get; set; } = new();
}

/// <summary>
/// DTO Thẻ Lô Sản xuất Kanban trên bảng điều độ phân xưởng (Jira-style Kanban Card - QT-05, Table 1.22).
/// </summary>
public class ProductionBatchDto
{
    public long BatchId { get; set; }
    public string BatchCode { get; set; } = string.Empty;
    public long OrderItemId { get; set; }
    public long? ParentBatchId { get; set; }
    public string BatchType { get; set; } = "MAIN_CARD";
    public int BatchQty { get; set; }
    public int CurrentStageSeq { get; set; } = 1;
    public string CurrentStageName { get; set; } = string.Empty;
    public string CurrentWorkstationCode { get; set; } = string.Empty;
    public bool IsOverdriver { get; set; }
    public int? HoldPointStageSeq { get; set; }
    public bool GoodsIssuedUnlocked { get; set; }
    public string BatchStatus { get; set; } = "LOCKED";
    public string BatchStatusText => BatchStatus;
    public DateTime CreatedAt { get; set; }
    public List<BatchRoutingStepDto> Steps { get; set; } = new();
}

/// <summary>
/// DTO Công đoạn thực thi của Thẻ Lô Sản xuất (Table 1.23, Sequential State Machine - QT-05).
/// </summary>
public class BatchRoutingStepDto
{
    public long BatchStepId { get; set; }
    public long BatchId { get; set; }
    public int StageSequence { get; set; }
    public string StageName { get; set; } = string.Empty;
    public string WorkstationCode { get; set; } = string.Empty;
    public string WorkstationName { get; set; } = string.Empty;
    public string QualityGateCode { get; set; } = string.Empty;
    public string QualityGateName { get; set; } = string.Empty;
    public string InspectionCriteria { get; set; } = string.Empty;
    public bool IsHoldPoint { get; set; }
    public int TargetQty { get; set; }
    public int PassedQty { get; set; }
    public int DefectQty { get; set; }
    public string StepStatus { get; set; } = "WAITING";
    public string StepStatusText => StepStatus;
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// DTO Danh mục Trạm gia công tiêu chuẩn của xưởng gỗ (SC-18, Workstation Catalog).
/// </summary>
public class StandardWorkstationDto
{
    public string WorkstationCode { get; set; } = string.Empty;
    public string WorkstationName { get; set; } = string.Empty;
    public int DefaultStageSequence { get; set; }
    public int StandardCapacityPerHour { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// DTO Danh mục Điểm kiểm tra chất lượng bắt buộc (SC-18, QC Gate Catalog).
/// </summary>
public class StandardQualityGateDto
{
    public string QualityGateCode { get; set; } = string.Empty;
    public string QualityGateName { get; set; } = string.Empty;
    public int StageSequence { get; set; }
    public bool IsHoldPoint { get; set; }
    public string InspectionType { get; set; } = "100% Gate";
    public string InspectionCriteria { get; set; } = string.Empty;
}

/// <summary>
/// DTO Yêu cầu chuyển bước công đoạn tuần tự trên bảng Kanban (SC-19, QT-05).
/// </summary>
public class AdvanceStepRequestDto
{
    /// <summary>Mã công đoạn đích muốn chuyển tới (phải là S + 1, không được nhảy cóc).</summary>
    public int? TargetStageSeq { get; set; }

    /// <summary>Ghi chú chuyển công đoạn / nhật ký phân xưởng.</summary>
    public string? Notes { get; set; }
}

/// <summary>
/// DTO Ghi nhận kết quả kiểm định KCS tại cổng QC Gate và kích hoạt tách nhánh 97%-3% (SC-19, QT-06, KBN01).
/// </summary>
public class RecordQcInspectionDto
{
    /// <summary>Mã công đoạn thực thi đang kiểm định (<c>BatchStepId</c>).</summary>
    public long BatchStepId { get; set; }

    /// <summary>Tổng số lượng phôi/sản phẩm kiểm định thực tế (phải bằng 100% số lượng của thẻ lô).</summary>
    public int InspectedQty { get; set; }

    /// <summary>Số lượng phôi/sản phẩm đạt chuẩn kỹ thuật.</summary>
    public int PassQty { get; set; }

    /// <summary>Số lượng phôi/sản phẩm phát hiện lỗi hoặc không đạt chuẩn.</summary>
    public int FailQty { get; set; }

    /// <summary>Mã loại lỗi vi phạm (ví dụ: ERR_CUT_01, ERR_WOOD_01).</summary>
    public string? DefectCode { get; set; }

    /// <summary>Tên chi tiết mô tả lỗi (ví dụ: Vát mộng không khít, nứt phôi thô).</summary>
    public string? DefectName { get; set; }

    /// <summary>Phương án xử lý lỗi: REWORK_SUB_CARD (Tách thẻ nhánh viền cam 3%) hoặc SCRAP_REPLENISH (Lô làm bù hủy hoàn toàn).</summary>
    public string Disposition { get; set; } = "REWORK_SUB_CARD";

    /// <summary>Ghi chú của KCS.</summary>
    public string? Notes { get; set; }
}

/// <summary>
/// DTO Kết quả sau khi ghi nhận kiểm định chất lượng QC Gate.
/// </summary>
public class QcInspectionResultDto
{
    public long QcId { get; set; }
    public long BatchStepId { get; set; }
    public long BatchId { get; set; }
    public int InspectedQty { get; set; }
    public int PassQty { get; set; }
    public int FailQty { get; set; }
    public bool IsBranched { get; set; }
    public long? SpawnedSubBatchId { get; set; }
    public string? SpawnedSubBatchCode { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// DTO Yêu cầu chạy trước Overdrive do Tổ trưởng sản xuất khởi tạo (SC-20, UC-63, QT-06).
/// </summary>
public class RequestOverdriveDto
{
    /// <summary>Công đoạn dừng kỹ thuật (Hold Point) dự kiến khóa cứng (mặc định công đoạn 5 - Lắp ráp kết cấu).</summary>
    public int? HoldPointStageSeq { get; set; } = 5;

    /// <summary>Lý do giải trình kỹ thuật và rủi ro thời hạn giao hàng ETD (bắt buộc).</summary>
    public string Justification { get; set; } = string.Empty;
}

/// <summary>
/// DTO Phê duyệt hoặc từ chối yêu cầu Overdrive của Chủ xưởng (SC-20, KBN02).
/// </summary>
public class ApproveOverdriveDto
{
    /// <summary>Chấp thuận (true) hoặc Từ chối (false).</summary>
    public bool Approved { get; set; }

    /// <summary>Công đoạn chốt chặn Hold Point bắt buộc dừng lại (mặc định 5).</summary>
    public int? HoldPointStageSeq { get; set; } = 5;

    /// <summary>Ghi chú chỉ đạo hoặc lý do từ chối của Chủ xưởng.</summary>
    public string? Notes { get; set; }
}

/// <summary>
/// DTO Kết quả thực hiện thao tác Overdrive.
/// </summary>
public class OverdriveOperationResultDto
{
    public long BatchId { get; set; }
    public string BatchCode { get; set; } = string.Empty;
    public bool IsOverdriver { get; set; }
    public int? HoldPointStageSeq { get; set; }
    public string BatchStatus { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

