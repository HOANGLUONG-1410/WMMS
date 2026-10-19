using WMMS.Domain.Entities.Identity;

namespace WMMS.Domain.Entities.KanbanQc;

public class OverdriveLog
{
    public long OverdriveId { get; set; }
    public long BatchId { get; set; }
    public ProductionBatch ProductionBatch { get; set; } = null!;

    public long RequestedBy { get; set; }
    public User Requester { get; set; } = null!;

    public long ApprovedBy { get; set; }
    public User Approver { get; set; } = null!;

    public string Justification { get; set; } = string.Empty;
    public int HoldPointStageSeq { get; set; }
    public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;
}
