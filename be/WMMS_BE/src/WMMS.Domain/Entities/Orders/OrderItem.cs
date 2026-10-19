using WMMS.Domain.Entities.CatalogBom;
using WMMS.Domain.Entities.InventoryPr;
using WMMS.Domain.Entities.KanbanQc;
using WMMS.Domain.Enums;

namespace WMMS.Domain.Entities.Orders;

public class OrderItem
{
    public long OrderItemId { get; set; }
    public long OrderId { get; set; }
    public CustomerOrder CustomerOrder { get; set; } = null!;

    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int OrderedQty { get; set; }
    public decimal? UnitPriceUsd { get; set; }
    public bool FscCertifiedReq { get; set; } = false;
    public decimal? LengthMm { get; set; }
    public decimal? WidthMm { get; set; }
    public decimal? HeightMm { get; set; }
    public OrderItemStatusEnum Status { get; set; } = OrderItemStatusEnum.PENDING_BOM;

    public BomHeader? BomHeader { get; set; }
    public ICollection<MaterialAllocation> MaterialAllocations { get; set; } = new List<MaterialAllocation>();
    public ICollection<ProductionBatch> ProductionBatches { get; set; } = new List<ProductionBatch>();
}
