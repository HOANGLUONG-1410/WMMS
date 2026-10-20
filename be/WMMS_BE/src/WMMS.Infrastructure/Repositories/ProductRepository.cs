using Microsoft.EntityFrameworkCore;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Domain.Entities.CatalogBom;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly WmmsDbContext _context;

    public ProductRepository(WmmsDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetByIdAsync(long productId)
    {
        return await _context.Products
            .Include(p => p.RoutingTemplates)
                .ThenInclude(rt => rt.RoutingStages)
            .Include(p => p.BomHeaders)
            .FirstOrDefaultAsync(p => p.ProductId == productId);
    }

    public async Task<Product?> GetBySkuAsync(string productSku)
    {
        return await _context.Products
            .Include(p => p.RoutingTemplates)
                .ThenInclude(rt => rt.RoutingStages)
            .Include(p => p.BomHeaders)
            .FirstOrDefaultAsync(p => p.ProductSku == productSku);
    }

    public async Task<List<Product>> GetAllAsync(string? category = null)
    {
        var query = _context.Products
            .Include(p => p.RoutingTemplates)
                .ThenInclude(rt => rt.RoutingStages)
            .Include(p => p.BomHeaders)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => p.CategoryName == category);
        }

        return await query.ToListAsync();
    }
}
