using WMMS.Domain.Entities.CatalogBom;
using WMMS.Domain.Entities.Identity;

namespace WMMS.Application.Interfaces.Repositories;

/// <summary>
/// Giao diện kho lưu trữ định mức nguyên vật liệu BOM (<c>bom_headers</c>, <c>bom_details</c>) và nhật ký kiểm toán liên quan (QT-02, SC-10, SC-11).
/// </summary>
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
    Task AddAuditLogAsync(AuditLog auditLog);
}
