using WMMS.Domain.Common;

namespace WMMS.Domain.Entities.Orders;

public class Customer : BaseEntity
{
    public long CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Country { get; set; }

    public ICollection<CustomerOrder> SalesOrders { get; set; } = new List<CustomerOrder>();
}
