namespace WMMS.Domain.Entities.KanbanQc;

public class DefectCatalog
{
    public string DefectCode { get; set; } = string.Empty;
    public string DefectName { get; set; } = string.Empty;
    public string? StandardStage { get; set; }
    public string? RootCause { get; set; }
    public string? RecommendedAction { get; set; }

    public ICollection<DefectLog> DefectLogs { get; set; } = new List<DefectLog>();
}
