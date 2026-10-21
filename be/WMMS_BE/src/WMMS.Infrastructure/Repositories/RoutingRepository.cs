using Microsoft.EntityFrameworkCore;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Domain.Entities.CatalogBom;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Repositories;

public class RoutingRepository : IRoutingRepository
{
    private readonly WmmsDbContext _context;

    public RoutingRepository(WmmsDbContext context)
    {
        _context = context;
    }

    public async Task<RoutingTemplate?> GetActiveRoutingByProductIdAsync(long productId)
    {
        return await _context.RoutingTemplates
            .Include(r => r.RoutingStages)
            .FirstOrDefaultAsync(r => r.ProductId == productId && r.IsActive);
    }

    public async Task<List<RoutingTemplate>> GetTemplatesByCategoryAsync(string categoryName)
    {
        return await _context.RoutingTemplates
            .Include(r => r.Product)
            .Include(r => r.RoutingStages)
            .Where(r => r.Product.CategoryName == categoryName && r.IsActive)
            .ToListAsync();
    }

    public async Task<RoutingTemplate> SaveCustomRoutingAsync(long productId, IEnumerable<RoutingStage> stages)
    {
        var existing = await _context.RoutingTemplates
            .Include(r => r.RoutingStages)
            .FirstOrDefaultAsync(r => r.ProductId == productId && r.IsActive);

        if (existing != null)
        {
            _context.RoutingStages.RemoveRange(existing.RoutingStages);
            existing.UpdatedAt = DateTime.UtcNow;

            var newStages = stages.Select(s => new RoutingStage
            {
                RoutingId = existing.RoutingId,
                StageSequence = s.StageSequence,
                StageName = s.StageName,
                IsHoldPoint = s.IsHoldPoint
            }).ToList();

            await _context.RoutingStages.AddRangeAsync(newStages);
            return existing;
        }
        else
        {
            var template = new RoutingTemplate
            {
                ProductId = productId,
                VersionCode = "v1.0",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _context.RoutingTemplates.AddAsync(template);
            await _context.SaveChangesAsync();

            var newStages = stages.Select(s => new RoutingStage
            {
                RoutingId = template.RoutingId,
                StageSequence = s.StageSequence,
                StageName = s.StageName,
                IsHoldPoint = s.IsHoldPoint
            }).ToList();

            await _context.RoutingStages.AddRangeAsync(newStages);
            return template;
        }
    }
}
