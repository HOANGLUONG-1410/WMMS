using WMMS.Domain.Entities.CatalogBom;

namespace WMMS.Application.Interfaces.Repositories;

public interface IMaterialRepository
{
    Task<Material?> GetByIdAsync(long materialId);
    Task<Material?> GetByCodeAsync(string materialCode);
    Task<List<Material>> GetAllAsync();
    Task<Dictionary<string, Material>> GetByCodesAsync(IEnumerable<string> materialCodes);
}
