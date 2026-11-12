using WMMS.Domain.Entities.Orders;

namespace WMMS.Application.Interfaces.Repositories;

/// <summary>
/// Giao diện truy vấn và cập nhật dòng sản phẩm đơn hàng (<c>order_items</c>), đơn hàng (<c>sales_orders</c>) và không gian đơn hàng (<c>order_spaces</c>)
/// phục vụ Bóc tách BOM (SC-10) và Tính toán MRP / Phân bổ Tồn kho tự động (SC-14).
/// </summary>
public interface IOrderItemRepository
{
    Task<OrderItem?> GetByIdAsync(long orderItemId);
    Task<OrderItem?> GetByIdWithFullMrpGraphAsync(long orderItemId, CancellationToken cancellationToken = default);
    Task<List<OrderItem>> GetByOrderIdWithFullMrpGraphAsync(long orderId, CancellationToken cancellationToken = default);
    Task<List<OrderItem>> GetBySpaceIdWithFullMrpGraphAsync(long spaceId, CancellationToken cancellationToken = default);
    Task<List<OrderItem>> GetAllActiveOrderItemsWithMrpGraphAsync(CancellationToken cancellationToken = default);
    Task<List<OrderItem>> GetWaitingMaterialItemsOrderedByEtdAsync(IEnumerable<long>? filterMaterialIds = null, CancellationToken cancellationToken = default);
    Task<CustomerOrder?> GetOrderByIdAsync(long orderId, CancellationToken cancellationToken = default);
    Task<CustomerOrder?> GetOrderWithFullProgressGraphAsync(long orderId, CancellationToken cancellationToken = default);
    Task<List<CustomerOrder>> GetAllOrdersWithProgressGraphAsync(CancellationToken cancellationToken = default);
    Task<OrderSpace?> GetOrderSpaceByIdAsync(long spaceId, CancellationToken cancellationToken = default);
    Task UpdateAsync(OrderItem orderItem);
    Task UpdateOrderAsync(CustomerOrder order);
    Task UpdateOrderSpaceAsync(OrderSpace space);
}
