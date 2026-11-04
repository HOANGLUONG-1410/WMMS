namespace WMMS.Application.DTOs.MasterData;

// =============================================================================
// DTO phân hệ Master Data (SC-04 Vật tư, SC-05 Sản phẩm, SC-06 Đối tác)
// UC-17..UC-29 - Report 3. Quy tắc: BR-MDM-01 (mã duy nhất), UC-21 (soft-delete).
// =============================================================================

#region Materials (SC-04 / UC-17..UC-21)

/// <summary>Bộ lọc & phân trang danh mục vật tư (UC-17).</summary>
public class MaterialQueryFilterDto
{
    /// <summary>Từ khóa tìm theo MaterialCode / MaterialName.</summary>
    public string? Search { get; set; }

    /// <summary>Nhóm vật tư: LUMBER, HARDWARE, FINISH_PAINT, PACKAGING.</summary>
    public string? Category { get; set; }

    /// <summary>Trạng thái: ACTIVE / INACTIVE.</summary>
    public string? Status { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>Thông tin vật tư trong danh sách (UC-17).</summary>
public class MaterialDto
{
    public long MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialGroup { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public bool FscCertified { get; set; }
    public decimal SafetyStockQty { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Chi tiết vật tư kèm tồn kho (UC-18).</summary>
public class MaterialDetailDto : MaterialDto
{
    public string? DimensionSpec { get; set; }
    public decimal PhysicalQty { get; set; }
    public decimal AllocatedQty { get; set; }
    public decimal AvailableQty { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Yêu cầu tạo mới vật tư (UC-19, BR-MDM-01).</summary>
public class CreateMaterialDto
{
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialGroup { get; set; } = "LUMBER";
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string? DimensionSpec { get; set; }
    public bool FscCertified { get; set; }
    public decimal SafetyStockQty { get; set; }
}

/// <summary>Yêu cầu cập nhật vật tư (UC-20). MaterialCode là bất biến.</summary>
public class UpdateMaterialDto
{
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialGroup { get; set; } = "LUMBER";
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string? DimensionSpec { get; set; }
    public bool FscCertified { get; set; }
    public decimal SafetyStockQty { get; set; }
    public string Status { get; set; } = "ACTIVE";
}

#endregion

#region Products (SC-05 / UC-22..UC-25)

/// <summary>Bộ lọc & phân trang danh mục sản phẩm (UC-22).</summary>
public class ProductQueryFilterDto
{
    /// <summary>Từ khóa tìm theo SKU / tên sản phẩm (không phân biệt hoa thường).</summary>
    public string? Search { get; set; }
    public string? Category { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>Sản phẩm trong danh sách (UC-22).</summary>
public class ProductDto
{
    public long ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string DimensionsCm { get; set; } = string.Empty;
    public string? WoodSpecies { get; set; }
    public string? FinishColor { get; set; }
    public decimal? CbmPerUnit { get; set; }
    public string? ImageUrl { get; set; }
    public string? CadDrawingUrl { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Công đoạn trong mẫu định tuyến mặc định.</summary>
public class DefaultRoutingStageDto
{
    public int StageSequence { get; set; }
    public string StageName { get; set; } = string.Empty;
    public bool IsHoldPoint { get; set; }
}

/// <summary>Chi tiết sản phẩm kèm các công đoạn định tuyến mặc định (UC-23).</summary>
public class ProductDetailDto : ProductDto
{
    public string? PackingSpec { get; set; }
    public List<DefaultRoutingStageDto> DefaultRoutingStages { get; set; } = new();
}

/// <summary>Yêu cầu tạo sản phẩm (UC-24).</summary>
public class CreateProductDto
{
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string DimensionsCm { get; set; } = string.Empty;
    public string? WoodSpecies { get; set; }
    public string? FinishColor { get; set; }
    public string? PackingSpec { get; set; }
    public decimal? CbmPerUnit { get; set; }
    public string? ImageUrl { get; set; }
}

/// <summary>Yêu cầu cập nhật sản phẩm (UC-24). ProductSku là bất biến.</summary>
public class UpdateProductDto
{
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string DimensionsCm { get; set; } = string.Empty;
    public string? WoodSpecies { get; set; }
    public string? FinishColor { get; set; }
    public string? PackingSpec { get; set; }
    public decimal? CbmPerUnit { get; set; }
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = "ACTIVE";
}

#endregion

#region Suppliers & Customers (SC-06 / UC-26..UC-29)

/// <summary>Bộ lọc & phân trang nhà cung cấp / khách hàng (UC-26).</summary>
public class PartnerQueryFilterDto
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>Nhà cung cấp (UC-26, UC-27).</summary>
public class SupplierDto
{
    public long SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string SupplyCategory { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? Phone { get; set; }
    public int LeadTimeDays { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Yêu cầu tạo nhà cung cấp (UC-28, BR-MDM-01).</summary>
public class CreateSupplierDto
{
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string SupplyCategory { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? Phone { get; set; }
    public int LeadTimeDays { get; set; } = 7;
}

/// <summary>Yêu cầu cập nhật nhà cung cấp (UC-29). SupplierCode là bất biến.</summary>
public class UpdateSupplierDto
{
    public string SupplierName { get; set; } = string.Empty;
    public string SupplyCategory { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? Phone { get; set; }
    public int LeadTimeDays { get; set; } = 7;
    public string Status { get; set; } = "ACTIVE";
}

/// <summary>Khách hàng (đối tác bán hàng).</summary>
public class CustomerDto
{
    public long CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Country { get; set; }
}

/// <summary>Yêu cầu tạo khách hàng (BR-MDM-01).</summary>
public class CreateCustomerDto
{
    public string CustomerCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Country { get; set; }
}

#endregion
