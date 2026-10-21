namespace WMMS.Domain.Entities.CatalogBom;

public class RoutingStage
{
    public long StageId { get; set; }
    public long RoutingId { get; set; }
    public RoutingTemplate RoutingTemplate { get; set; } = null!;

    public int StageSequence { get; set; }
    public string StageName { get; set; } = string.Empty;
    public bool IsHoldPoint { get; set; } = false;
}
