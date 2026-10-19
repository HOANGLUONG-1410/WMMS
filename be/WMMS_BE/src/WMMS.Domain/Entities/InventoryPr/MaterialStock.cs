using WMMS.Domain.Entities.CatalogBom;

namespace WMMS.Domain.Entities.InventoryPr;

public class MaterialStock
{
    public long StockId { get; set; }
    public long MaterialId { get; set; }
    public Material Material { get; set; } = null!;

    public decimal PhysicalQty { get; set; } = 0;
    public decimal AllocatedQty { get; set; } = 0;
    public decimal AvailableQty { get; set; } = 0;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public bool CanAllocate(decimal qty) => AvailableQty >= qty;
}
