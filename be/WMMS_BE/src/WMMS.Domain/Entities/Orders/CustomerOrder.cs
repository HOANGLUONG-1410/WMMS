using WMMS.Domain.Common;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Enums;

namespace WMMS.Domain.Entities.Orders;

public class CustomerOrder : BaseEntity, IAuditableEntity
{
    public long OrderId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public long CreatedBy { get; set; }
    public User Creator { get; set; } = null!;

    public DateTime OrderDate { get; set; }
    public DateTime EtdDate { get; set; }
    public decimal? TotalCbm { get; set; }
    public OrderStatusEnum Status { get; set; } = OrderStatusEnum.CONFIRMED;

    public OrderSpace? OrderSpace { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public bool IsEtdAtRisk(int warningDays) => EtdDate <= DateTime.UtcNow.AddDays(warningDays);
}
