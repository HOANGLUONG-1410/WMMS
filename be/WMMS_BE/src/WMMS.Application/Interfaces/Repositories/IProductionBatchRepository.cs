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
    Task<List<ProductionBatch>> GetBySpaceKeyAsync(string spaceKey, CancellationToken cancellationToken = default);
    Task<List<ProductionBatch>> GetAllActiveBatchesAsync(BatchStatusEnum? status = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Đếm tổng số lô sản xuất phục vụ tính tỷ lệ phá khóa ngoại lệ Overdriver (Iter 3 - SC-26, <c>BR-PRD-01</c>).
    /// Sử dụng truy vấn <c>AsNoTracking()</c> gọn nhẹ (chỉ <c>COUNT(*)</c> khi không lọc loại gỗ, hoặc chỉ chiếu
    /// thông tin loại gỗ sản phẩm/BOM vật tư <c>LUMBER</c> khi có bộ lọc <paramref name="woodType"/>) nhằm tránh
    /// kéo đồ thị 16 bảng và loại bỏ nghẽn Memory Grant (<c>RESOURCE_SEMAPHORE</c>) trên SQL Server.
    /// </summary>
    Task<int> CountBatchesForAnalyticsAsync(long? orderId = null, string? woodType = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Lấy danh sách lô sản xuất ở chế độ chỉ đọc (<c>AsNoTracking()</c>) phục vụ xuất báo cáo Excel
    /// Lịch sử Điều độ Phân xưởng (Iter 3 - SC-26). Chỉ nạp nhánh <c>BomHeader -&gt; BomDetails -&gt; Material</c>
    /// khi <paramref name="includeBomDetails"/> là <c>true</c> (có lọc theo loại gỗ).
    /// </summary>
    Task<List<ProductionBatch>> GetBatchesForDispatchExportAsync(long? orderId = null, bool includeBomDetails = false, CancellationToken cancellationToken = default);
    Task<BatchRoutingStep?> GetStepByIdAsync(long batchStepId, CancellationToken cancellationToken = default);
    Task AddAsync(ProductionBatch batch, CancellationToken cancellationToken = default);
    Task UpdateAsync(ProductionBatch batch);
    Task AddStepsRangeAsync(IEnumerable<BatchRoutingStep> steps, CancellationToken cancellationToken = default);
    Task UpdateStepAsync(BatchRoutingStep step);
}
