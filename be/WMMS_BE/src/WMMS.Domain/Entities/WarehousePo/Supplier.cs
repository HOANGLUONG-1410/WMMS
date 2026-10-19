using WMMS.Domain.Common;

namespace WMMS.Domain.Entities.WarehousePo;

public class Supplier : BaseEntity
{
    public long SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string SupplyCategory { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? Phone { get; set; }
    public int LeadTimeDays { get; set; } = 7;
    public string Status { get; set; } = "ACTIVE";

    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
}
