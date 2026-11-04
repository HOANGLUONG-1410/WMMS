using Microsoft.EntityFrameworkCore;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Domain.Entities.InventoryPr;
using WMMS.Domain.Entities.WarehousePo;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Repositories;

/// <summary>
/// Thực thi kho lưu trữ số dư tồn kho 3 cấp (<c>inventory_stocks</c>) và sổ kho giao dịch (<c>inventory_transactions</c>).
/// </summary>
public class MaterialStockRepository : IMaterialStockRepository
{
    private readonly WmmsDbContext _context;

    public MaterialStockRepository(WmmsDbContext context)
    {
        _context = context;
    }

    public async Task<MaterialStock?> GetByMaterialIdAsync(long materialId, CancellationToken cancellationToken = default)
    {
        return await _context.MaterialStocks
            .Include(ms => ms.Material)
            .FirstOrDefaultAsync(ms => ms.MaterialId == materialId, cancellationToken);
    }

    public async Task<Dictionary<long, MaterialStock>> GetByMaterialIdsAsync(
        IEnumerable<long> materialIds,
        CancellationToken cancellationToken = default)
    {
        var ids = materialIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<long, MaterialStock>();

        var stocks = await _context.MaterialStocks
            .Include(ms => ms.Material)
            .Where(ms => ids.Contains(ms.MaterialId))
            .ToListAsync(cancellationToken);

        return stocks.ToDictionary(s => s.MaterialId, s => s);
    }

    public async Task<List<MaterialStock>> GetAllWithMaterialAsync(CancellationToken cancellationToken = default)
    {
        return await _context.MaterialStocks
            .Include(ms => ms.Material)
            .OrderBy(ms => ms.MaterialId)
            .ToListAsync(cancellationToken);
    }

    public async Task<MaterialStock> GetOrCreateByMaterialIdAsync(long materialId, CancellationToken cancellationToken = default)
    {
        var existing = await GetByMaterialIdAsync(materialId, cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        var created = new MaterialStock
        {
            MaterialId = materialId,
            PhysicalQty = 0m,
            AllocatedQty = 0m,
            AvailableQty = 0m,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.MaterialStocks.AddAsync(created, cancellationToken);
        return created;
    }

    public async Task AddAsync(MaterialStock stock, CancellationToken cancellationToken = default)
    {
        await _context.MaterialStocks.AddAsync(stock, cancellationToken);
    }

    public Task UpdateAsync(MaterialStock stock)
    {
        _context.MaterialStocks.Update(stock);
        return Task.CompletedTask;
    }

    public async Task AddTransactionAsync(InventoryTransaction transaction, CancellationToken cancellationToken = default)
    {
        await _context.InventoryTransactions.AddAsync(transaction, cancellationToken);
    }

    public async Task AddTransactionsRangeAsync(IEnumerable<InventoryTransaction> transactions, CancellationToken cancellationToken = default)
    {
        await _context.InventoryTransactions.AddRangeAsync(transactions, cancellationToken);
    }
}
