using WMMS.Domain.Common;
using WMMS.Domain.Entities.InventoryPr;
using WMMS.Domain.Entities.WarehousePo;
using WMMS.Domain.Enums;

namespace WMMS.Domain.Entities.CatalogBom;

public class Material : BaseEntity
{
    public long MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public MaterialGroupEnum MaterialGroup { get; set; } = MaterialGroupEnum.LUMBER;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string? DimensionSpec { get; set; }
    public bool FscCertified { get; set; } = false;
    public decimal SafetyStockQty { get; set; } = 0;
    public string Status { get; set; } = "ACTIVE";

    public MaterialStock? MaterialStock { get; set; }
    public ICollection<BomDetail> BomDetails { get; set; } = new List<BomDetail>();
    public ICollection<MaterialAllocation> MaterialAllocations { get; set; } = new List<MaterialAllocation>();
    public ICollection<PurchaseRequestItem> PurchaseRequestItems { get; set; } = new List<PurchaseRequestItem>();
    public ICollection<PurchaseOrderDetail> PurchaseOrderDetails { get; set; } = new List<PurchaseOrderDetail>();
}
