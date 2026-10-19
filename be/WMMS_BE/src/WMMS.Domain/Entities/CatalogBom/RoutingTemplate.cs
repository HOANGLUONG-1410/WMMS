using WMMS.Domain.Common;

namespace WMMS.Domain.Entities.CatalogBom;

public class RoutingTemplate : BaseEntity
{
    public long RoutingId { get; set; }
    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string VersionCode { get; set; } = "v1.0";
    public bool IsActive { get; set; } = true;

    public ICollection<RoutingStage> RoutingStages { get; set; } = new List<RoutingStage>();
}
