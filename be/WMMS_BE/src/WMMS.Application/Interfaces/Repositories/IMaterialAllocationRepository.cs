using WMMS.Domain.Entities.InventoryPr;

namespace WMMS.Application.Interfaces.Repositories;

/// <summary>
/// Giao diện kho lưu trữ phân bổ mềm / giữ chỗ nguyên vật liệu (<c>material_allocations</c>)
/// phục vụ thuật toán Phân bổ tồn kho tự động (SC-14) và Phân bổ lại theo thứ tự ưu tiên ETD khi nhập kho GRN (SC-17).
/// </summary>
public interface IMaterialAllocationRepository
{
    Task<MaterialAllocation?> GetByIdAsync(long allocationId, CancellationToken cancellationToken = default);
    Task<MaterialAllocation?> GetByOrderItemAndMaterialAsync(long orderItemId, long materialId, CancellationToken cancellationToken = default);
    Task<List<MaterialAllocation>> GetByOrderItemIdAsync(long orderItemId, CancellationToken cancellationToken = default);
    Task<List<MaterialAllocation>> GetByOrderIdAsync(long orderId, CancellationToken cancellationToken = default);
    Task<List<MaterialAllocation>> GetBySpaceIdAsync(long spaceId, CancellationToken cancellationToken = default);
    Task<List<MaterialAllocation>> GetReservedByMaterialIdAsync(long materialId, CancellationToken cancellationToken = default);
    Task<List<MaterialAllocation>> GetShortageAllocationsOrderedByEtdAsync(IEnumerable<long>? materialIds = null, CancellationToken cancellationToken = default);
    Task AddAsync(MaterialAllocation allocation, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<MaterialAllocation> allocations, CancellationToken cancellationToken = default);
    Task UpdateAsync(MaterialAllocation allocation);
    Task RemoveRangeAsync(IEnumerable<MaterialAllocation> allocations);
}
