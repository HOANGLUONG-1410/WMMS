using WMMS.Domain.Enums;

namespace WMMS.Domain.Entities.KanbanQc;

public class DefectLog
{
    public long DefectLogId { get; set; }
    public long QcId { get; set; }
    public QcInspection QcInspection { get; set; } = null!;

    public string DefectCode { get; set; } = string.Empty;
    public DefectCatalog DefectCatalog { get; set; } = null!;

    public string DefectName { get; set; } = string.Empty;
    public int DefectQty { get; set; }
    public DefectDispositionEnum Disposition { get; set; } = DefectDispositionEnum.REWORK_SUB_CARD;

    public long? SpawnedSubBatchId { get; set; }
    public ProductionBatch? SpawnedSubBatch { get; set; }
}
