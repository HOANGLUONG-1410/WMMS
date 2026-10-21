using WMMS.Domain.Entities.CatalogBom;

namespace WMMS.Application.Interfaces.Repositories;

public interface IBomRepository
{
    Task<BomHeader?> GetByIdAsync(long bomId);
    Task<BomHeader?> GetByOrderItemIdAsync(long orderItemId);
    Task<BomHeader?> GetLatestActiveBomByProductIdAsync(long productId);
    Task<bool> HasBomForOrderItemAsync(long orderItemId);
    Task AddAsync(BomHeader bom);
    Task UpdateAsync(BomHeader bom);
    Task DeleteAsync(BomHeader bom);
    Task AddDetailsAsync(IEnumerable<BomDetail> details);
    Task RemoveDetailsAsync(IEnumerable<BomDetail> details);
}
