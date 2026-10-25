using WMMS.Domain.Entities.CatalogBom;

namespace WMMS.Application.Interfaces.Repositories;

public interface IRoutingRepository
{
    Task<RoutingTemplate?> GetActiveRoutingByProductIdAsync(long productId);
    Task<List<RoutingTemplate>> GetTemplatesByCategoryAsync(string categoryName);
    Task<RoutingTemplate> SaveCustomRoutingAsync(long productId, IEnumerable<RoutingStage> stages);
}
