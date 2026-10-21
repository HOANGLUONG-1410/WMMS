namespace WMMS.Domain.Entities.CatalogBom;

public class BomDetail
{
    public long BomDetailId { get; set; }
    public long BomId { get; set; }
    public BomHeader BomHeader { get; set; } = null!;

    public long MaterialId { get; set; }
    public Material Material { get; set; } = null!;

    public decimal NetQtyPerUnit { get; set; }
    public decimal GrossTotalDemand { get; set; }
    public string? Notes { get; set; }
}
