using WMMS.Domain.Entities.KanbanQc;
using WMMS.Domain.Enums;

namespace WMMS.Application.Interfaces.Repositories;

/// <summary>
/// Giao diện kho lưu trữ Thẻ Sản xuất Kanban (<c>production_batches</c>) và Công đoạn Thực thi (<c>batch_routing_steps</c>)
/// phục vụ phân hệ Điều độ Sản xuất Kanban, Tách thẻ nhánh 97-3 (BR-PRD-01) và Khóa xuất kho (SC-18, SC-19).
/// </summary>
public interface IProductionBatchRepository
{
    Task<ProductionBatch?> GetByIdAsync(long batchId, CancellationToken cancellationToken = default);
    Task<ProductionBatch?> GetByCodeAsync(string batchCode, CancellationToken cancellationToken = default);
    Task<List<ProductionBatch>> GetByOrderItemIdAsync(long orderItemId, CancellationToken cancellationToken = default);
    Task<List<ProductionBatch>> GetByOrderIdAsync(long orderId, CancellationToken cancellationToken = default);
    Task<List<ProductionBatch>> GetBySpaceIdAsync(long spaceId, CancellationToken cancellationToken = default);
    Task<List<ProductionBatch>> GetAllActiveBatchesAsync(BatchStatusEnum? status = null, CancellationToken cancellationToken = default);
    Task<BatchRoutingStep?> GetStepByIdAsync(long batchStepId, CancellationToken cancellationToken = default);
    Task AddAsync(ProductionBatch batch, CancellationToken cancellationToken = default);
    Task UpdateAsync(ProductionBatch batch);
    Task AddStepsRangeAsync(IEnumerable<BatchRoutingStep> steps, CancellationToken cancellationToken = default);
    Task UpdateStepAsync(BatchRoutingStep step);
}
