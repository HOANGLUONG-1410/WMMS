using WMMS.Domain.Entities.Orders;

namespace WMMS.Application.Interfaces.Repositories;

public interface IOrderItemRepository
{
    Task<OrderItem?> GetByIdAsync(long orderItemId);
    Task UpdateAsync(OrderItem orderItem);
}
