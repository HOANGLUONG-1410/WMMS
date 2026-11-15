using WMMS.Domain.Entities.CatalogBom;

namespace WMMS.Application.Interfaces.Repositories;

/// <summary>
/// Kho lưu trữ dữ liệu Thành phẩm đồ gỗ nội thất Master Data (<see cref="Product"/>).
/// </summary>
public interface IProductRepository
{
    /// <summary>
    /// Lấy thông tin thành phẩm theo định danh duy nhất (ID).
    /// </summary>
    Task<Product?> GetByIdAsync(long productId);

    /// <summary>
    /// Lấy thông tin thành phẩm theo mã SKU.
    /// </summary>
    Task<Product?> GetBySkuAsync(string productSku);

    /// <summary>
    /// Lấy danh sách thành phẩm có trong hệ thống, có thể lọc theo phân loại (category).
    /// </summary>
    Task<List<Product>> GetAllAsync(string? category = null);
}
