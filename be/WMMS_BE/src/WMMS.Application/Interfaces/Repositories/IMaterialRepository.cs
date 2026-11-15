using WMMS.Domain.Entities.CatalogBom;

namespace WMMS.Application.Interfaces.Repositories;

/// <summary>
/// Kho lưu trữ dữ liệu Nguyên vật liệu danh mục Master Data (<see cref="Material"/>).
/// Cung cấp các thao tác truy vấn theo mã và ID cho phân hệ BOM và Kho.
/// </summary>
public interface IMaterialRepository
{
    /// <summary>
    /// Lấy nguyên vật liệu theo định danh duy nhất (ID).
    /// </summary>
    Task<Material?> GetByIdAsync(long materialId);

    /// <summary>
    /// Lấy nguyên vật liệu theo mã duy nhất (MaterialCode).
    /// </summary>
    Task<Material?> GetByCodeAsync(string materialCode);

    /// <summary>
    /// Lấy toàn bộ danh sách nguyên vật liệu có trong hệ thống.
    /// </summary>
    Task<List<Material>> GetAllAsync();

    /// <summary>
    /// Tra cứu hàng loạt nguyên vật liệu theo tập hợp mã nguyên vật liệu.
    /// </summary>
    Task<Dictionary<string, Material>> GetByCodesAsync(IEnumerable<string> materialCodes);
}
