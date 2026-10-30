using FluentValidation;
using WMMS.Application.DTOs.MasterData;
using WMMS.Domain.Enums;

namespace WMMS.Application.Validators.MasterData;

// Validators Master Data (MSG01 trường bắt buộc / MSG13 dữ liệu không hợp lệ) - UC-19, UC-20, UC-24, UC-28, UC-29.

internal static class MasterDataRules
{
    public static readonly string[] Statuses = { "ACTIVE", "INACTIVE" };
    public static bool IsMaterialGroup(string? v) => Enum.TryParse<MaterialGroupEnum>(v, true, out var g) && Enum.IsDefined(g);
    public static bool IsStatus(string? v) => v != null && Statuses.Contains(v.ToUpperInvariant());
}

/// <summary>UC-19: Tạo vật tư (BR-MDM-01).</summary>
public class CreateMaterialValidator : AbstractValidator<CreateMaterialDto>
{
    public CreateMaterialValidator()
    {
        RuleFor(x => x.MaterialCode).NotEmpty().WithMessage("Mã vật tư không được rỗng (MSG01).").MaximumLength(100);
        RuleFor(x => x.MaterialName).NotEmpty().WithMessage("Tên vật tư không được rỗng (MSG01).").MaximumLength(255);
        RuleFor(x => x.MaterialGroup).Must(MasterDataRules.IsMaterialGroup).WithMessage("Nhóm vật tư không hợp lệ (LUMBER, HARDWARE, FINISH_PAINT, PACKAGING).");
        RuleFor(x => x.UnitOfMeasure).NotEmpty().WithMessage("Đơn vị tính không được rỗng (MSG01).").MaximumLength(20);
        RuleFor(x => x.DimensionSpec).MaximumLength(150);
        RuleFor(x => x.SafetyStockQty).GreaterThanOrEqualTo(0).WithMessage("SafetyStockQty phải >= 0.");
    }
}

/// <summary>UC-20: Cập nhật vật tư.</summary>
public class UpdateMaterialValidator : AbstractValidator<UpdateMaterialDto>
{
    public UpdateMaterialValidator()
    {
        RuleFor(x => x.MaterialName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.MaterialGroup).Must(MasterDataRules.IsMaterialGroup).WithMessage("Nhóm vật tư không hợp lệ.");
        RuleFor(x => x.UnitOfMeasure).NotEmpty().MaximumLength(20);
        RuleFor(x => x.DimensionSpec).MaximumLength(150);
        RuleFor(x => x.SafetyStockQty).GreaterThanOrEqualTo(0).WithMessage("SafetyStockQty phải >= 0.");
        RuleFor(x => x.Status).Must(MasterDataRules.IsStatus).WithMessage("Status phải là ACTIVE hoặc INACTIVE.");
    }
}

/// <summary>UC-24: Tạo sản phẩm.</summary>
public class CreateProductValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.ProductSku).NotEmpty().WithMessage("SKU không được rỗng (MSG01).").MaximumLength(100);
        RuleFor(x => x.ProductName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.CategoryName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DimensionsCm).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CbmPerUnit).GreaterThanOrEqualTo(0).When(x => x.CbmPerUnit.HasValue);
        RuleFor(x => x.ImageUrl).MaximumLength(500);
    }
}

/// <summary>UC-24: Cập nhật sản phẩm.</summary>
public class UpdateProductValidator : AbstractValidator<UpdateProductDto>
{
    public UpdateProductValidator()
    {
        RuleFor(x => x.ProductName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.CategoryName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DimensionsCm).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CbmPerUnit).GreaterThanOrEqualTo(0).When(x => x.CbmPerUnit.HasValue);
        RuleFor(x => x.Status).Must(MasterDataRules.IsStatus).WithMessage("Status phải là ACTIVE hoặc INACTIVE.");
    }
}

/// <summary>UC-28: Tạo nhà cung cấp.</summary>
public class CreateSupplierValidator : AbstractValidator<CreateSupplierDto>
{
    public CreateSupplierValidator()
    {
        RuleFor(x => x.SupplierCode).NotEmpty().WithMessage("Mã NCC không được rỗng (MSG01).").MaximumLength(50);
        RuleFor(x => x.SupplierName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.SupplyCategory).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ContactEmail).EmailAddress().MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.LeadTimeDays).InclusiveBetween(0, 365).WithMessage("LeadTimeDays phải từ 0 đến 365.");
    }
}

/// <summary>UC-29: Cập nhật nhà cung cấp.</summary>
public class UpdateSupplierValidator : AbstractValidator<UpdateSupplierDto>
{
    public UpdateSupplierValidator()
    {
        RuleFor(x => x.SupplierName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.SupplyCategory).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ContactEmail).EmailAddress().MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.LeadTimeDays).InclusiveBetween(0, 365);
        RuleFor(x => x.Status).Must(MasterDataRules.IsStatus).WithMessage("Status phải là ACTIVE hoặc INACTIVE.");
    }
}

/// <summary>Tạo khách hàng.</summary>
public class CreateCustomerValidator : AbstractValidator<CreateCustomerDto>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.CustomerCode).NotEmpty().WithMessage("Mã khách hàng không được rỗng (MSG01).").MaximumLength(50);
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContactPerson).MaximumLength(255);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(255).When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
