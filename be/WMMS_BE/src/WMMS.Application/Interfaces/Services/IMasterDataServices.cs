using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.MasterData;

namespace WMMS.Application.Interfaces.Services;

/// <summary>Dịch vụ danh mục Vật tư (SC-04, UC-17..UC-21).</summary>
public interface IMaterialService
{
    /// <summary>UC-17: Danh sách vật tư phân trang, lọc theo nhóm/trạng thái/từ khóa.</summary>
    Task<PagedResult<MaterialDto>> GetMaterialsAsync(MaterialQueryFilterDto filter, CancellationToken cancellationToken = default);

    /// <summary>UC-18: Chi tiết vật tư kèm tồn kho. Ném NotFound (MSG10) nếu không tồn tại.</summary>
    Task<MaterialDetailDto> GetMaterialByIdAsync(long materialId, CancellationToken cancellationToken = default);

    /// <summary>UC-19: Tạo vật tư + MaterialStock ban đầu. Ném Conflict (MSG08) nếu trùng mã (BR-MDM-01).</summary>
    Task<MaterialDto> CreateMaterialAsync(CreateMaterialDto dto, long userId, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>UC-20: Cập nhật vật tư và ghi AuditLog (GBR-05).</summary>
    Task<MaterialDto> UpdateMaterialAsync(long materialId, UpdateMaterialDto dto, long userId, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>UC-21: Vô hiệu hóa (soft-delete) vật tư, không xóa vật lý.</summary>
    Task DeactivateMaterialAsync(long materialId, long userId, string? ipAddress, CancellationToken cancellationToken = default);
}

/// <summary>Dịch vụ danh mục Sản phẩm (SC-05, UC-22..UC-25).</summary>
public interface IProductService
{
    /// <summary>UC-22: Danh sách sản phẩm phân trang, tìm kiếm không phân biệt hoa thường.</summary>
    Task<PagedResult<ProductDto>> GetProductsAsync(ProductQueryFilterDto filter, CancellationToken cancellationToken = default);

    /// <summary>UC-23: Chi tiết sản phẩm + công đoạn định tuyến mặc định.</summary>
    Task<ProductDetailDto> GetProductByIdAsync(long productId, CancellationToken cancellationToken = default);

    /// <summary>UC-24: Tạo sản phẩm. Ném Conflict (MSG08) nếu trùng SKU.</summary>
    Task<ProductDto> CreateProductAsync(CreateProductDto dto, long userId, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>UC-24: Cập nhật sản phẩm.</summary>
    Task<ProductDto> UpdateProductAsync(long productId, UpdateProductDto dto, long userId, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>UC-25: Tải bản vẽ CAD/ảnh cho sản phẩm; trả về sản phẩm đã cập nhật CadDrawingUrl.</summary>
    Task<ProductDto> UploadCadDrawingAsync(long productId, string fileName, Stream content, long length, long userId, string? ipAddress, CancellationToken cancellationToken = default);
}

/// <summary>Dịch vụ danh mục Đối tác: Nhà cung cấp & Khách hàng (SC-06, UC-26..UC-29).</summary>
public interface ISupplierService
{
    /// <summary>UC-26: Danh sách nhà cung cấp phân trang.</summary>
    Task<PagedResult<SupplierDto>> GetSuppliersAsync(PartnerQueryFilterDto filter, CancellationToken cancellationToken = default);

    /// <summary>UC-27: Chi tiết nhà cung cấp.</summary>
    Task<SupplierDto> GetSupplierByIdAsync(long supplierId, CancellationToken cancellationToken = default);

    /// <summary>UC-28: Tạo nhà cung cấp. Ném Conflict (MSG08) nếu trùng mã.</summary>
    Task<SupplierDto> CreateSupplierAsync(CreateSupplierDto dto, long userId, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>UC-29: Cập nhật nhà cung cấp.</summary>
    Task<SupplierDto> UpdateSupplierAsync(long supplierId, UpdateSupplierDto dto, long userId, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>Danh sách khách hàng phân trang.</summary>
    Task<PagedResult<CustomerDto>> GetCustomersAsync(PartnerQueryFilterDto filter, CancellationToken cancellationToken = default);

    /// <summary>Tạo khách hàng. Ném Conflict (MSG08) nếu trùng mã.</summary>
    Task<CustomerDto> CreateCustomerAsync(CreateCustomerDto dto, long userId, string? ipAddress, CancellationToken cancellationToken = default);
}
