using WMMS.Domain.Common;
using WMMS.Domain.Entities.Orders;
using WMMS.Domain.Enums;

namespace WMMS.Domain.Entities.KanbanQc;

public class ProductionBatch : BaseEntity
{
    public long BatchId { get; set; }
    public string BatchCode { get; set; } = string.Empty;
    public long OrderItemId { get; set; }
    public OrderItem OrderItem { get; set; } = null!;

    public long? ParentBatchId { get; set; }
    public ProductionBatch? ParentBatch { get; set; }

    public BatchTypeEnum BatchType { get; set; } = BatchTypeEnum.MAIN_CARD;
    public int BatchQty { get; set; }
    public int CurrentStageSeq { get; set; } = 1;
    public bool IsOverdriver { get; set; } = false;
    public int? HoldPointStageSeq { get; set; }
    public bool GoodsIssuedUnlocked { get; set; } = false;
    public BatchStatusEnum BatchStatus { get; set; } = BatchStatusEnum.LOCKED;

    public ICollection<ProductionBatch> SubBatches { get; set; } = new List<ProductionBatch>();
    public ICollection<BatchRoutingStep> BatchRoutingSteps { get; set; } = new List<BatchRoutingStep>();
    public ICollection<OverdriveLog> OverdriveLogs { get; set; } = new List<OverdriveLog>();

    public bool CanAdvancePastHoldPoint() => !IsOverdriver || !HoldPointStageSeq.HasValue || CurrentStageSeq < HoldPointStageSeq.Value || GoodsIssuedUnlocked;
    public void ReduceQtyOnBranch(int defectQty) => BatchQty = Math.Max(0, BatchQty - defectQty);
    public void UnlockForProduction()
    {
        GoodsIssuedUnlocked = true;
        if (BatchStatus == BatchStatusEnum.LOCKED)
        {
            BatchStatus = BatchStatusEnum.IN_PROGRESS;
        }
    }
}
