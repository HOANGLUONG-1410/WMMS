using Microsoft.EntityFrameworkCore;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Domain.Entities.Orders;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Repositories;

public class OrderItemRepository : IOrderItemRepository
{
    private readonly WmmsDbContext _context;

    public OrderItemRepository(WmmsDbContext context)
    {
        _context = context;
    }

    public async Task<OrderItem?> GetByIdAsync(long orderItemId)
    {
        return await _context.OrderItems
            .Include(oi => oi.CustomerOrder)
            .Include(oi => oi.Product)
            .Include(oi => oi.BomHeader)
            .FirstOrDefaultAsync(oi => oi.OrderItemId == orderItemId);
    }

    public Task UpdateAsync(OrderItem orderItem)
    {
        _context.OrderItems.Update(orderItem);
        return Task.CompletedTask;
    }
}
