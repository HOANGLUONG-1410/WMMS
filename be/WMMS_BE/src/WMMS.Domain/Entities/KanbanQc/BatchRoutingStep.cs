using WMMS.Domain.Enums;

namespace WMMS.Domain.Entities.KanbanQc;

public class BatchRoutingStep
{
    public long BatchStepId { get; set; }
    public long BatchId { get; set; }
    public ProductionBatch ProductionBatch { get; set; } = null!;

    public int StageSequence { get; set; }
    public string StageName { get; set; } = string.Empty;
    public bool IsHoldPoint { get; set; } = false;
    public int TargetQty { get; set; }
    public int PassedQty { get; set; } = 0;
    public int DefectQty { get; set; } = 0;
    public StepStatusEnum StepStatus { get; set; } = StepStatusEnum.WAITING;
    public DateTime? CompletedAt { get; set; }

    public ICollection<QcInspection> QcInspections { get; set; } = new List<QcInspection>();

    public bool Is100PercentPassed() => PassedQty == TargetQty && TargetQty > 0;
}
