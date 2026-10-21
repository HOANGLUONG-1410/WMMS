using Microsoft.EntityFrameworkCore;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Domain.Entities.CatalogBom;
using WMMS.Domain.Enums;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Repositories;

public class BomRepository : IBomRepository
{
    private readonly WmmsDbContext _context;

    public BomRepository(WmmsDbContext context)
    {
        _context = context;
    }

    public async Task<BomHeader?> GetByIdAsync(long bomId)
    {
        return await _context.BomHeaders
            .Include(b => b.Product)
            .Include(b => b.OrderItem)
            .Include(b => b.Creator)
            .Include(b => b.BomDetails)
                .ThenInclude(d => d.Material)
            .FirstOrDefaultAsync(b => b.BomId == bomId);
    }

    public async Task<BomHeader?> GetByOrderItemIdAsync(long orderItemId)
    {
        return await _context.BomHeaders
            .Include(b => b.Product)
            .Include(b => b.OrderItem)
            .Include(b => b.Creator)
            .Include(b => b.BomDetails)
                .ThenInclude(d => d.Material)
            .FirstOrDefaultAsync(b => b.OrderItemId == orderItemId);
    }

    public async Task<BomHeader?> GetLatestActiveBomByProductIdAsync(long productId)
    {
        return await _context.BomHeaders
            .Include(b => b.Product)
            .Include(b => b.BomDetails)
                .ThenInclude(d => d.Material)
            .Where(b => b.ProductId == productId &&
                       (b.BomStatus == BomStatusEnum.READY_FOR_PRODUCTION || b.BomStatus == BomStatusEnum.DRAFT))
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> HasBomForOrderItemAsync(long orderItemId)
    {
        return await _context.BomHeaders.AnyAsync(b => b.OrderItemId == orderItemId);
    }

    public async Task AddAsync(BomHeader bom)
    {
        await _context.BomHeaders.AddAsync(bom);
    }

    public Task UpdateAsync(BomHeader bom)
    {
        _context.BomHeaders.Update(bom);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(BomHeader bom)
    {
        _context.BomHeaders.Remove(bom);
        return Task.CompletedTask;
    }

    public async Task AddDetailsAsync(IEnumerable<BomDetail> details)
    {
        await _context.BomDetails.AddRangeAsync(details);
    }

    public Task RemoveDetailsAsync(IEnumerable<BomDetail> details)
    {
        _context.BomDetails.RemoveRange(details);
        return Task.CompletedTask;
    }
}
