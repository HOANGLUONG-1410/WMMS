using WMMS.Domain.Common;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Entities.Orders;
using WMMS.Domain.Enums;

namespace WMMS.Domain.Entities.CatalogBom;

public class BomHeader : BaseEntity, IAuditableEntity
{
    public long BomId { get; set; }
    public long OrderItemId { get; set; }
    public OrderItem OrderItem { get; set; } = null!;

    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public long CreatedBy { get; set; }
    public User Creator { get; set; } = null!;

    public decimal ScrapRatePercent { get; set; } = 3.00m;
    public BomStatusEnum BomStatus { get; set; } = BomStatusEnum.DRAFT;

    public ICollection<BomDetail> BomDetails { get; set; } = new List<BomDetail>();
}
