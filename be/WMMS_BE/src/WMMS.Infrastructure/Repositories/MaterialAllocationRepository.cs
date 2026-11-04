using Microsoft.EntityFrameworkCore;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Domain.Entities.InventoryPr;
using WMMS.Domain.Enums;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Repositories;

/// <summary>
/// Thực thi kho lưu trữ phân bổ mềm / giữ chỗ nguyên vật liệu (<c>material_allocations</c>)
/// phục vụ thuật toán Phân bổ tồn kho tự động (SC-14) và Phân bổ lại theo thứ tự ưu tiên ETD (SC-17).
/// </summary>
public class MaterialAllocationRepository : IMaterialAllocationRepository
{
    private readonly WmmsDbContext _context;

    public MaterialAllocationRepository(WmmsDbContext context)
    {
        _context = context;
    }

    public async Task<MaterialAllocation?> GetByIdAsync(long allocationId, CancellationToken cancellationToken = default)
    {
        return await BuildFullAllocationQuery()
            .FirstOrDefaultAsync(ma => ma.AllocationId == allocationId, cancellationToken);
    }

    public async Task<MaterialAllocation?> GetByOrderItemAndMaterialAsync(
        long orderItemId,
        long materialId,
        CancellationToken cancellationToken = default)
    {
        return await BuildFullAllocationQuery()
            .Where(ma => ma.OrderItemId == orderItemId &&
                         ma.MaterialId == materialId &&
                         ma.AllocationStatus != AllocationStatusEnum.RELEASED)
            .OrderByDescending(ma => ma.AllocatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<MaterialAllocation>> GetByOrderItemIdAsync(long orderItemId, CancellationToken cancellationToken = default)
    {
        return await BuildFullAllocationQuery()
            .Where(ma => ma.OrderItemId == orderItemId && ma.AllocationStatus != AllocationStatusEnum.RELEASED)
            .OrderBy(ma => ma.MaterialId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MaterialAllocation>> GetByOrderIdAsync(long orderId, CancellationToken cancellationToken = default)
    {
        return await BuildFullAllocationQuery()
            .Where(ma => ma.OrderItem.OrderId == orderId && ma.AllocationStatus != AllocationStatusEnum.RELEASED)
            .OrderBy(ma => ma.OrderItemId)
            .ThenBy(ma => ma.MaterialId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MaterialAllocation>> GetBySpaceIdAsync(long spaceId, CancellationToken cancellationToken = default)
    {
        return await BuildFullAllocationQuery()
            .Where(ma => ma.OrderItem.CustomerOrder.OrderSpace != null &&
                         ma.OrderItem.CustomerOrder.OrderSpace.SpaceId == spaceId &&
                         ma.AllocationStatus != AllocationStatusEnum.RELEASED)
            .OrderBy(ma => ma.OrderItemId)
            .ThenBy(ma => ma.MaterialId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MaterialAllocation>> GetReservedByMaterialIdAsync(long materialId, CancellationToken cancellationToken = default)
    {
        return await BuildFullAllocationQuery()
            .Where(ma => ma.MaterialId == materialId && ma.AllocationStatus == AllocationStatusEnum.RESERVED)
            .OrderBy(ma => ma.OrderItem.CustomerOrder.EtdDate)
            .ThenBy(ma => ma.AllocatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MaterialAllocation>> GetShortageAllocationsOrderedByEtdAsync(
        IEnumerable<long>? materialIds = null,
        CancellationToken cancellationToken = default)
    {
        var filterIds = materialIds?.Distinct().ToList();
        var query = BuildFullAllocationQuery()
            .Where(ma => ma.AllocationStatus == AllocationStatusEnum.RESERVED &&
                         ma.ReservedQty < ma.RequiredQty &&
                         ma.OrderItem.CustomerOrder.Status != OrderStatusEnum.CANCELLED &&
                         ma.OrderItem.CustomerOrder.Status != OrderStatusEnum.COMPLETED);

        if (filterIds != null && filterIds.Count > 0)
        {
            query = query.Where(ma => filterIds.Contains(ma.MaterialId));
        }

        return await query
            .OrderBy(ma => ma.OrderItem.CustomerOrder.EtdDate)
            .ThenBy(ma => ma.OrderItem.OrderId)
            .ThenBy(ma => ma.AllocationId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(MaterialAllocation allocation, CancellationToken cancellationToken = default)
    {
        await _context.MaterialAllocations.AddAsync(allocation, cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<MaterialAllocation> allocations, CancellationToken cancellationToken = default)
    {
        await _context.MaterialAllocations.AddRangeAsync(allocations, cancellationToken);
    }

    public Task UpdateAsync(MaterialAllocation allocation)
    {
        _context.MaterialAllocations.Update(allocation);
        return Task.CompletedTask;
    }

    public Task RemoveRangeAsync(IEnumerable<MaterialAllocation> allocations)
    {
        _context.MaterialAllocations.RemoveRange(allocations);
        return Task.CompletedTask;
    }

    private IQueryable<MaterialAllocation> BuildFullAllocationQuery()
    {
        return _context.MaterialAllocations
            .Include(ma => ma.Material)
                .ThenInclude(m => m.MaterialStock)
            .Include(ma => ma.OrderItem)
                .ThenInclude(oi => oi.Product)
            .Include(ma => ma.OrderItem)
                .ThenInclude(oi => oi.CustomerOrder)
                    .ThenInclude(co => co.Customer)
            .Include(ma => ma.OrderItem)
                .ThenInclude(oi => oi.CustomerOrder)
                    .ThenInclude(co => co.OrderSpace);
    }
}
