using Microsoft.EntityFrameworkCore;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Domain.Entities.Orders;
using WMMS.Domain.Enums;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Repositories;

/// <summary>
/// Thực thi kho lưu trữ dòng sản phẩm đơn hàng (<c>order_items</c>), đơn hàng (<c>sales_orders</c>) và không gian đơn hàng (<c>order_spaces</c>)
/// phục vụ Bóc tách BOM (SC-10) và Tính toán MRP / Phân bổ Tồn kho tự động (SC-14).
/// </summary>
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
                .ThenInclude(co => co.OrderSpace)
            .Include(oi => oi.CustomerOrder)
                .ThenInclude(co => co.Customer)
            .Include(oi => oi.Product)
            .Include(oi => oi.BomHeader)
            .FirstOrDefaultAsync(oi => oi.OrderItemId == orderItemId);
    }

    public async Task<OrderItem?> GetByIdWithFullMrpGraphAsync(long orderItemId, CancellationToken cancellationToken = default)
    {
        return await BuildFullMrpQuery()
            .FirstOrDefaultAsync(oi => oi.OrderItemId == orderItemId, cancellationToken);
    }

    public async Task<List<OrderItem>> GetByOrderIdWithFullMrpGraphAsync(long orderId, CancellationToken cancellationToken = default)
    {
        return await BuildFullMrpQuery()
            .Where(oi => oi.OrderId == orderId)
            .OrderBy(oi => oi.OrderItemId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<OrderItem>> GetBySpaceIdWithFullMrpGraphAsync(long spaceId, CancellationToken cancellationToken = default)
    {
        return await BuildFullMrpQuery()
            .Where(oi => oi.CustomerOrder.OrderSpace != null && oi.CustomerOrder.OrderSpace.SpaceId == spaceId)
            .OrderBy(oi => oi.OrderItemId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<OrderItem>> GetAllActiveOrderItemsWithMrpGraphAsync(CancellationToken cancellationToken = default)
    {
        return await BuildFullMrpQuery()
            .Where(oi => oi.CustomerOrder.Status != OrderStatusEnum.CANCELLED &&
                         oi.CustomerOrder.Status != OrderStatusEnum.COMPLETED &&
                         oi.BomHeader != null)
            .OrderBy(oi => oi.CustomerOrder.EtdDate)
            .ThenBy(oi => oi.OrderId)
            .ThenBy(oi => oi.OrderItemId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<OrderItem>> GetWaitingMaterialItemsOrderedByEtdAsync(
        IEnumerable<long>? filterMaterialIds = null,
        CancellationToken cancellationToken = default)
    {
        var materialIdSet = filterMaterialIds?.Distinct().ToList();

        var query = BuildFullMrpQuery()
            .Where(oi => oi.CustomerOrder.Status != OrderStatusEnum.CANCELLED &&
                         oi.CustomerOrder.Status != OrderStatusEnum.COMPLETED &&
                         oi.BomHeader != null &&
                         (oi.Status == OrderItemStatusEnum.WAITING_MAT ||
                          oi.Status == OrderItemStatusEnum.BOM_APPROVED ||
                          oi.BomHeader.BomStatus == BomStatusEnum.WAITING_FOR_MATERIALS ||
                          oi.BomHeader.BomStatus == BomStatusEnum.APPROVED));

        if (materialIdSet != null && materialIdSet.Count > 0)
        {
            query = query.Where(oi => oi.BomHeader!.BomDetails.Any(bd => materialIdSet.Contains(bd.MaterialId)));
        }

        return await query
            .OrderBy(oi => oi.CustomerOrder.EtdDate)
            .ThenBy(oi => oi.OrderId)
            .ThenBy(oi => oi.OrderItemId)
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerOrder?> GetOrderByIdAsync(long orderId, CancellationToken cancellationToken = default)
    {
        return await _context.CustomerOrders
            .Include(co => co.Customer)
            .Include(co => co.OrderSpace)
            .Include(co => co.OrderItems)
            .FirstOrDefaultAsync(co => co.OrderId == orderId, cancellationToken);
    }

    public async Task<CustomerOrder?> GetOrderWithFullProgressGraphAsync(long orderId, CancellationToken cancellationToken = default)
    {
        return await BuildOrderWithProgressGraphQuery()
            .FirstOrDefaultAsync(co => co.OrderId == orderId, cancellationToken);
    }

    public async Task<List<CustomerOrder>> GetAllOrdersWithProgressGraphAsync(CancellationToken cancellationToken = default)
    {
        return await BuildOrderWithProgressGraphQuery()
            .Where(co => co.Status != OrderStatusEnum.CANCELLED)
            .OrderBy(co => co.EtdDate)
            .ThenBy(co => co.OrderId)
            .ToListAsync(cancellationToken);
    }

    public async Task<OrderSpace?> GetOrderSpaceByIdAsync(long spaceId, CancellationToken cancellationToken = default)
    {
        return await _context.OrderSpaces
            .Include(os => os.CustomerOrder)
                .ThenInclude(co => co.Customer)
            .Include(os => os.CustomerOrder)
                .ThenInclude(co => co.OrderItems)
            .FirstOrDefaultAsync(os => os.SpaceId == spaceId, cancellationToken);
    }

    public Task UpdateAsync(OrderItem orderItem)
    {
        _context.OrderItems.Update(orderItem);
        return Task.CompletedTask;
    }

    public Task UpdateOrderAsync(CustomerOrder order)
    {
        _context.CustomerOrders.Update(order);
        return Task.CompletedTask;
    }

    public Task UpdateOrderSpaceAsync(OrderSpace space)
    {
        _context.OrderSpaces.Update(space);
        return Task.CompletedTask;
    }

    private IQueryable<OrderItem> BuildFullMrpQuery()
    {
        return _context.OrderItems
            .Include(oi => oi.CustomerOrder)
                .ThenInclude(co => co.Customer)
            .Include(oi => oi.CustomerOrder)
                .ThenInclude(co => co.OrderSpace)
            .Include(oi => oi.Product)
            .Include(oi => oi.BomHeader)
                .ThenInclude(bh => bh!.BomDetails)
                    .ThenInclude(bd => bd.Material)
                        .ThenInclude(m => m.MaterialStock)
            .Include(oi => oi.MaterialAllocations)
            .Include(oi => oi.ProductionBatches);
    }

    private IQueryable<CustomerOrder> BuildOrderWithProgressGraphQuery()
    {
        return _context.CustomerOrders
            .Include(co => co.Customer)
            .Include(co => co.OrderSpace)
            .Include(co => co.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Include(co => co.OrderItems)
                .ThenInclude(oi => oi.BomHeader)
                    .ThenInclude(bh => bh!.BomDetails)
                        .ThenInclude(bd => bd.Material)
            .Include(co => co.OrderItems)
                .ThenInclude(oi => oi.MaterialAllocations)
            .Include(co => co.OrderItems)
                .ThenInclude(oi => oi.ProductionBatches)
                    .ThenInclude(pb => pb.BatchRoutingSteps)
                        .ThenInclude(brs => brs.QcInspections)
                            .ThenInclude(qc => qc.DefectLogs);
    }
}
