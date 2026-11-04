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

    public void RecalculateAvailable()
    {
        AvailableQty = Math.Max(0m, Math.Round(PhysicalQty - AllocatedQty, 4));
        UpdatedAt = DateTime.UtcNow;
    }

    public decimal AllocateUpToAvailable(decimal requestedQty)
    {
        if (requestedQty <= 0m) return 0m;
        RecalculateAvailable();
        var allocatedNow = Math.Min(requestedQty, AvailableQty);
        if (allocatedNow > 0m)
        {
            AllocatedQty = Math.Round(AllocatedQty + allocatedNow, 4);
            RecalculateAvailable();
        }
        return allocatedNow;
    }

    public void ReleaseAllocation(decimal qtyToRelease)
    {
        if (qtyToRelease <= 0m) return;
        AllocatedQty = Math.Max(0m, Math.Round(AllocatedQty - qtyToRelease, 4));
        RecalculateAvailable();
    }

    public void ReceiveStock(decimal receivedQty)
    {
        if (receivedQty <= 0m) return;
        PhysicalQty = Math.Round(PhysicalQty + receivedQty, 4);
        RecalculateAvailable();
    }

    public void IssueAllocatedStock(decimal issuedQty)
    {
        if (issuedQty <= 0m) return;
        PhysicalQty = Math.Max(0m, Math.Round(PhysicalQty - issuedQty, 4));
        AllocatedQty = Math.Max(0m, Math.Round(AllocatedQty - issuedQty, 4));
        RecalculateAvailable();
    }
}
