using WMMS.Domain.Entities.CatalogBom;
using WMMS.Domain.Enums;

namespace WMMS.Domain.Entities.WarehousePo;

public class InventoryTransaction
{
    public long TransactionId { get; set; }
    public long MaterialId { get; set; }
    public Material Material { get; set; } = null!;

    public InventoryTransactionTypeEnum TxType { get; set; } = InventoryTransactionTypeEnum.IN_RECEIPT;
    public decimal Quantity { get; set; }
    public string? ReferenceCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
