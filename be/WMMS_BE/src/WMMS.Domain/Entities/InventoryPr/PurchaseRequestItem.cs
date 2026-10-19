using WMMS.Domain.Entities.CatalogBom;

namespace WMMS.Domain.Entities.InventoryPr;

public class PurchaseRequestItem
{
    public long PrItemId { get; set; }
    public long PrId { get; set; }
    public PurchaseRequest PurchaseRequest { get; set; } = null!;

    public long MaterialId { get; set; }
    public Material Material { get; set; } = null!;

    public decimal TotalShortageQty { get; set; }
    public decimal SuggestedOrderQty { get; set; }
    public string? SourceOrderIdsJson { get; set; }
}
