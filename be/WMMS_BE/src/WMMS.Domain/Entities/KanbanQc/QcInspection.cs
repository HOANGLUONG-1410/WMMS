using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Enums;

namespace WMMS.Domain.Entities.KanbanQc;

public class QcInspection
{
    public long QcId { get; set; }
    public long BatchStepId { get; set; }
    public BatchRoutingStep BatchRoutingStep { get; set; } = null!;

    public long InspectorId { get; set; }
    public User Inspector { get; set; } = null!;

    public int InspectedQty { get; set; }
    public int PassQty { get; set; }
    public int FailQty { get; set; }
    public QcResultStatusEnum ResultStatus { get; set; } = QcResultStatusEnum.FULL_PASS_100;
    public DateTime InspectedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DefectLog> DefectLogs { get; set; } = new List<DefectLog>();
}
