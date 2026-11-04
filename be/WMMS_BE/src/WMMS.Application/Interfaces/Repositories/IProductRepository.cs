using WMMS.Domain.Entities.CatalogBom;

namespace WMMS.Application.Interfaces.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(long productId);
    Task<Product?> GetBySkuAsync(string productSku);
    Task<List<Product>> GetAllAsync(string? category = null);
}
