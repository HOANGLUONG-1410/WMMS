using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Enums;

namespace WMMS.Domain.Entities.WarehousePo;

public class GoodsReceipt
{
    public long GrnId { get; set; }
    public string GrnCode { get; set; } = string.Empty;
    public long PoId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public long WarehouseKeeperId { get; set; }
    public User WarehouseKeeper { get; set; } = null!;

    public DateTime CommittedAt { get; set; } = DateTime.UtcNow;
    public GrnStatusEnum Status { get; set; } = GrnStatusEnum.COMMITTED;
}
