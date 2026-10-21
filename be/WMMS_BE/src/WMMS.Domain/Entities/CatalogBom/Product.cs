using WMMS.Domain.Common;
using WMMS.Domain.Entities.Orders;

namespace WMMS.Domain.Entities.CatalogBom;

public class Product : BaseEntity
{
    public long ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string DimensionsCm { get; set; } = string.Empty;
    public string? WoodSpecies { get; set; }
    public string? FinishColor { get; set; }
    public string? PackingSpec { get; set; }
    public decimal? CbmPerUnit { get; set; }
    public string? ImageUrl { get; set; }
    public string? CadDrawingUrl { get; set; }
    public string Status { get; set; } = "ACTIVE";

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<RoutingTemplate> RoutingTemplates { get; set; } = new List<RoutingTemplate>();
    public ICollection<BomHeader> BomHeaders { get; set; } = new List<BomHeader>();
}
