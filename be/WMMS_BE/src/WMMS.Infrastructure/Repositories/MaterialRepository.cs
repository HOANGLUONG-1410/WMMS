using Microsoft.EntityFrameworkCore;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Domain.Entities.CatalogBom;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Repositories;

public class MaterialRepository : IMaterialRepository
{
    private readonly WmmsDbContext _context;

    public MaterialRepository(WmmsDbContext context)
    {
        _context = context;
    }

    public async Task<Material?> GetByIdAsync(long materialId)
    {
        return await _context.Materials
            .Include(m => m.MaterialStock)
            .FirstOrDefaultAsync(m => m.MaterialId == materialId);
    }

    public async Task<Material?> GetByCodeAsync(string materialCode)
    {
        return await _context.Materials
            .Include(m => m.MaterialStock)
            .FirstOrDefaultAsync(m => m.MaterialCode == materialCode);
    }

    public async Task<List<Material>> GetAllAsync()
    {
        return await _context.Materials
            .Include(m => m.MaterialStock)
            .ToListAsync();
    }

    public async Task<Dictionary<string, Material>> GetByCodesAsync(IEnumerable<string> materialCodes)
    {
        var list = await _context.Materials
            .Where(m => materialCodes.Contains(m.MaterialCode))
            .ToListAsync();

        return list.ToDictionary(m => m.MaterialCode, m => m, StringComparer.OrdinalIgnoreCase);
    }
}
