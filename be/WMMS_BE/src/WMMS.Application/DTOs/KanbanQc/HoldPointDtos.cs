namespace WMMS.Application.DTOs.KanbanQc;

/// <summary>
/// DTO Chi tiết trạng thái điểm dừng Hold Point và tiến độ công đoạn của Thẻ Lô Sản Xuất (SC-20, KBN02).
/// </summary>
public class HoldPointStatusDto
{
    public long BatchId { get; set; }
    public string BatchCode { get; set; } = string.Empty;
    public long OrderItemId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int BatchQty { get; set; }
    public int CurrentStageSeq { get; set; }
    public string CurrentStageName { get; set; } = string.Empty;
    public string BatchStatus { get; set; } = string.Empty;
    public string BatchType { get; set; } = string.Empty;

    /// <summary>Cờ Overdriver cho phép chạy trước các công đoạn đầu khi thiếu phụ kiện/sơn ở trạm sau (KBN02).</summary>
    public bool IsOverdriver { get; set; }

    /// <summary>Thứ tự công đoạn trạm chốt chặn Hold Point bị giới hạn dừng lại.</summary>
    public int? HoldPointStageSeq { get; set; }

    /// <summary>Thủ kho đã xác nhận Goods Issue cấp phát thực tế vật tư xuống xưởng hay chưa.</summary>
    public bool GoodsIssuedUnlocked { get; set; }

    /// <summary>Lô sản xuất có đủ điều kiện để chuyển tiếp sang công đoạn tiếp theo hay không.</summary>
    public bool CanAdvance { get; set; }

    /// <summary>Lý do bị chặn (nếu có).</summary>
    public string? BlockReason { get; set; }

    /// <summary>Danh sách các công đoạn routing của thẻ lô kèm đánh dấu Hold Point.</summary>
    public List<HoldPointStepDto> Steps { get; set; } = new();

    /// <summary>Lịch sử các lần ghi đè/phá khóa Hold Point của lô này.</summary>
    public List<OverdriveLogDto> OverdriveLogs { get; set; } = new();
}

/// <summary>
/// DTO Thông tin công đoạn sản xuất trong chuỗi quy trình của Thẻ Lô (SC-20).
/// </summary>
public class HoldPointStepDto
{
    public long BatchStepId { get; set; }
    public int StageSequence { get; set; }
    public string StageName { get; set; } = string.Empty;
    public bool IsHoldPoint { get; set; }
    public int TargetQty { get; set; }
    public int PassedQty { get; set; }
    public int DefectQty { get; set; }
    public string StepStatus { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// DTO Yêu cầu Ghi đè/Phá khóa điểm dừng Hold Point do Tổ trưởng SX (LEADER) gửi lên Chủ xưởng (SC-20, KBN02).
/// </summary>
public class RequestHoldPointOverrideDto
{
    public long BatchId { get; set; }

    /// <summary>Thứ tự công đoạn Hold Point muốn xin phép chạy trước (nếu để trống, hệ thống tự động nhận diện trạm Hold Point gần nhất).</summary>
    public int? HoldPointStageSeq { get; set; }

    /// <summary>Lý do nghiệp vụ bắt buộc (VD: Đơn hàng cận ETD, đã đủ phôi gỗ cho phép gia công trước cụm chi tiết).</summary>
    public string Justification { get; set; } = string.Empty;
}

/// <summary>
/// DTO Phê duyệt Ghi đè điểm dừng Hold Point do Chủ xưởng (OWNER) hoặc ADMIN xác nhận (SC-20, KBN02).
/// </summary>
public class ApproveHoldPointOverrideDto
{
    /// <summary>Ghi chú/Chỉ đạo của Chủ xưởng khi phê duyệt mở khóa điểm dừng (bắt buộc).</summary>
    public string Justification { get; set; } = string.Empty;
}

/// <summary>
/// DTO Từ chối Yêu cầu Ghi đè điểm dừng Hold Point (SC-20, KBN02).
/// </summary>
public class RejectHoldPointOverrideDto
{
    /// <summary>Lý do từ chối yêu cầu (bắt buộc).</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// DTO Kết quả sau khi gửi yêu cầu, phê duyệt hoặc từ chối ghi đè điểm dừng Hold Point (SC-20).
/// </summary>
public class HoldPointOverrideResultDto
{
    public long? OverdriveId { get; set; }
    public long BatchId { get; set; }
    public string BatchCode { get; set; } = string.Empty;
    public int HoldPointStageSeq { get; set; }
    public string StageName { get; set; } = string.Empty;
    public bool IsOverdriver { get; set; }
    public string BatchStatus { get; set; } = string.Empty;
    public bool GoodsIssuedUnlocked { get; set; }
    public long RequestedBy { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public long? ApprovedBy { get; set; }
    public string? ApproverName { get; set; }
    public string Justification { get; set; } = string.Empty;
    public DateTime? ApprovedAt { get; set; }
    public string ActionTaken { get; set; } = string.Empty;
}

/// <summary>
/// DTO Bản ghi nhật ký phá khóa / ghi đè điểm dừng Hold Point (<c>overdrive_logs</c>).
/// </summary>
public class OverdriveLogDto
{
    public long OverdriveId { get; set; }
    public long BatchId { get; set; }
    public string BatchCode { get; set; } = string.Empty;
    public long RequestedBy { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public long ApprovedBy { get; set; }
    public string ApproverName { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public int HoldPointStageSeq { get; set; }
    public DateTime ApprovedAt { get; set; }
}

/// <summary>
/// DTO Yêu cầu chuyển công đoạn sản xuất (Advance Stage / Move Kanban Card).
/// </summary>
public class AdvanceStageRequestDto
{
    /// <summary>Ghi chú bổ sung khi kéo thẻ sang công đoạn mới.</summary>
    public string? Notes { get; set; }
}

/// <summary>
/// DTO Kết quả chuyển công đoạn sản xuất.
/// </summary>
public class AdvanceStageResultDto
{
    public long BatchId { get; set; }
    public string BatchCode { get; set; } = string.Empty;
    public int PreviousStageSeq { get; set; }
    public int NewStageSeq { get; set; }
    public string NewStageName { get; set; } = string.Empty;
    public string BatchStatus { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime AdvancedAt { get; set; }
}
