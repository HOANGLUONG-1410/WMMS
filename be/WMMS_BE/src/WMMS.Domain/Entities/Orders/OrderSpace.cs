using WMMS.Domain.Common;
using WMMS.Domain.Enums;

namespace WMMS.Domain.Entities.Orders;

public class OrderSpace : BaseEntity
{
    public long SpaceId { get; set; }
    public long OrderId { get; set; }
    public CustomerOrder CustomerOrder { get; set; } = null!;

    public string SpaceKey { get; set; } = string.Empty;
    public string SpaceName { get; set; } = string.Empty;
    public string? DrawingFolderUrl { get; set; }
    public decimal OverallProgressPct { get; set; } = 0;
    public SpaceStatusEnum Status { get; set; } = SpaceStatusEnum.ACTIVE;
}
