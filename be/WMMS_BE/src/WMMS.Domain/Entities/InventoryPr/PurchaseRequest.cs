using WMMS.Domain.Common;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Entities.WarehousePo;
using WMMS.Domain.Enums;

namespace WMMS.Domain.Entities.InventoryPr;

public class PurchaseRequest : BaseEntity
{
    public long PrId { get; set; }
    public string PrCode { get; set; } = string.Empty;
    public DateTime AggregationDate { get; set; }
    public long CreatedBy { get; set; }
    public User Creator { get; set; } = null!;

    public long? ApprovedBy { get; set; }
    public User? Approver { get; set; }

    public PrStatusEnum PrStatus { get; set; } = PrStatusEnum.PENDING_APPROVAL;
    public string? RejectionReason { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public ICollection<PurchaseRequestItem> Items { get; set; } = new List<PurchaseRequestItem>();
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
}
