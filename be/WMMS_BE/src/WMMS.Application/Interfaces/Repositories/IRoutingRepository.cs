using WMMS.Domain.Entities.CatalogBom;

namespace WMMS.Application.Interfaces.Repositories;

/// <summary>
/// Kho lưu trữ dữ liệu Quy trình công nghệ sản xuất xưởng gỗ (<see cref="RoutingTemplate"/>).
/// Quản lý chuỗi công đoạn qua các trạm máy (Workstation) và cổng kiểm tra chất lượng (Quality Gate).
/// </summary>
public interface IRoutingRepository
{
    /// <summary>
    /// Lấy quy trình công nghệ đang kích hoạt cho một sản phẩm.
    /// </summary>
    Task<RoutingTemplate?> GetActiveRoutingByProductIdAsync(long productId);

    /// <summary>
    /// Lấy danh sách quy trình công nghệ mẫu theo phân loại sản phẩm gỗ.
    /// </summary>
    Task<List<RoutingTemplate>> GetTemplatesByCategoryAsync(string categoryName);

    /// <summary>
    /// Lưu cấu hình quy trình sản xuất tùy biến (Dynamic Routing) cho một sản phẩm (BR-007, UC-37).
    /// </summary>
    Task<RoutingTemplate> SaveCustomRoutingAsync(long productId, IEnumerable<RoutingStage> stages);
}
