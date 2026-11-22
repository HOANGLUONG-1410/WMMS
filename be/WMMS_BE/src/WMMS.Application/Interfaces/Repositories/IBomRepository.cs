using WMMS.Domain.Entities.CatalogBom;
using WMMS.Domain.Entities.Identity;

namespace WMMS.Application.Interfaces.Repositories;

/// <summary>
/// Giao diện kho lưu trữ định mức nguyên vật liệu BOM (<c>bom_headers</c>, <c>bom_details</c>) và nhật ký kiểm toán liên quan (QT-02, SC-10, SC-11).
/// </summary>
public interface IBomRepository
{
    /// <summary>Lấy thông tin BOM theo khóa chính bomId.</summary>
    Task<BomHeader?> GetByIdAsync(long bomId);

    /// <summary>Lấy thông tin BOM riêng của dòng sản phẩm đơn hàng (OrderItemId).</summary>
    Task<BomHeader?> GetByOrderItemIdAsync(long orderItemId);

    /// <summary>QT-02: Lấy bản ghi BOM mẫu gần nhất của sản phẩm (theo ProductId) để tự động kế thừa dữ liệu.</summary>
    Task<BomHeader?> GetLatestActiveBomByProductIdAsync(long productId);

    /// <summary>BR-001: Kiểm tra dòng đơn hàng đã có BOM chưa để tránh tạo trùng lặp.</summary>
    Task<bool> HasBomForOrderItemAsync(long orderItemId);

    /// <summary>Thêm mới bản ghi đầu phiếu BOM (BomHeader).</summary>
    Task AddAsync(BomHeader bom);

    /// <summary>Cập nhật thông tin đầu phiếu BOM.</summary>
    Task UpdateAsync(BomHeader bom);

    /// <summary>Xóa bản ghi BOM.</summary>
    Task DeleteAsync(BomHeader bom);

    /// <summary>Thêm danh sách chi tiết vật tư vào BOM (BomDetails).</summary>
    Task AddDetailsAsync(IEnumerable<BomDetail> details);

    /// <summary>Xóa danh sách chi tiết vật tư của BOM.</summary>
    Task RemoveDetailsAsync(IEnumerable<BomDetail> details);

    /// <summary>GBR-05: Lưu nhật ký kiểm toán bất biến vào bảng audit_logs.</summary>
    Task AddAuditLogAsync(AuditLog auditLog);
}
