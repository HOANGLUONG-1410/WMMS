using WMMS.Domain.Common;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Entities.InventoryPr;
using WMMS.Domain.Enums;

namespace WMMS.Domain.Entities.WarehousePo;

public class PurchaseOrder : BaseEntity
{
    public long PoId { get; set; }
    public string PoCode { get; set; } = string.Empty;
    public long PrId { get; set; }
    public PurchaseRequest PurchaseRequest { get; set; } = null!;

    public long SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public long CreatedBy { get; set; }
    public User Creator { get; set; } = null!;

    public DateTime IssueDate { get; set; }
    public DateTime ExpectedArrivalDate { get; set; }
    public decimal? TotalAmount { get; set; }
    public PoStatusEnum PoStatus { get; set; } = PoStatusEnum.ISSUED;

    public ICollection<PurchaseOrderDetail> Details { get; set; } = new List<PurchaseOrderDetail>();
    public ICollection<GoodsReceipt> GoodsReceipts { get; set; } = new List<GoodsReceipt>();
}
