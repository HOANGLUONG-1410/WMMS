using WMMS.Domain.Entities.InventoryPr;
using WMMS.Domain.Entities.WarehousePo;

namespace WMMS.Application.Interfaces.Repositories;

/// <summary>
/// Giao diện kho lưu trữ số dư tồn kho 3 cấp (<c>inventory_stocks</c>) và sổ kho giao dịch (<c>inventory_transactions</c>)
/// phục vụ SC-12, SC-13, SC-14 và SC-17.
/// </summary>
public interface IMaterialStockRepository
{
    Task<MaterialStock?> GetByMaterialIdAsync(long materialId, CancellationToken cancellationToken = default);
    Task<Dictionary<long, MaterialStock>> GetByMaterialIdsAsync(IEnumerable<long> materialIds, CancellationToken cancellationToken = default);
    Task<List<MaterialStock>> GetAllWithMaterialAsync(CancellationToken cancellationToken = default);
    Task<MaterialStock> GetOrCreateByMaterialIdAsync(long materialId, CancellationToken cancellationToken = default);
    Task AddAsync(MaterialStock stock, CancellationToken cancellationToken = default);
    Task UpdateAsync(MaterialStock stock);
    Task AddTransactionAsync(InventoryTransaction transaction, CancellationToken cancellationToken = default);
    Task AddTransactionsRangeAsync(IEnumerable<InventoryTransaction> transactions, CancellationToken cancellationToken = default);
}
