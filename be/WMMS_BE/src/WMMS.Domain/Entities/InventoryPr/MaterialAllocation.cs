using WMMS.Domain.Entities.CatalogBom;
using WMMS.Domain.Entities.Orders;
using WMMS.Domain.Enums;

namespace WMMS.Domain.Entities.InventoryPr;

public class MaterialAllocation
{
    public long AllocationId { get; set; }
    public long OrderItemId { get; set; }
    public OrderItem OrderItem { get; set; } = null!;

    public long MaterialId { get; set; }
    public Material Material { get; set; } = null!;

    public decimal ReservedQty { get; set; } = 0;
    public decimal IssuedQty { get; set; } = 0;
    public decimal RequiredQty { get; set; } = 0;
    public decimal ShortageQty => Math.Max(0, RequiredQty - ReservedQty);

    public AllocationStatusEnum AllocationStatus { get; set; } = AllocationStatusEnum.RESERVED;
    public DateTime AllocatedAt { get; set; } = DateTime.UtcNow;
}
