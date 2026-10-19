using WMMS.Domain.Entities.CatalogBom;

namespace WMMS.Domain.Entities.WarehousePo;

public class PurchaseOrderDetail
{
    public long PoDetailId { get; set; }
    public long PoId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public long MaterialId { get; set; }
    public Material Material { get; set; } = null!;

    public decimal OrderedQty { get; set; }
    public decimal ReceivedQty { get; set; } = 0;
    public decimal? UnitPrice { get; set; }
}
