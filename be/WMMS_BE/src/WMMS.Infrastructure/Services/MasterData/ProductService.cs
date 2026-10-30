using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.MasterData;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Entities.CatalogBom;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Entities.InventoryPr;
using WMMS.Domain.Entities.Orders;
using WMMS.Domain.Entities.WarehousePo;
using WMMS.Domain.Enums;
using WMMS.Domain.Exceptions;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Services.MasterData;

/// <summary>Dịch vụ danh mục Sản phẩm (SC-05, UC-22..UC-25).</summary>
public class ProductService : IProductService
{
    private static readonly string[] AllowedCadExtensions = { ".dwg", ".dxf", ".pdf", ".png", ".jpg", ".jpeg" };
    private const long MaxCadBytes = 20L * 1024 * 1024;

    private readonly WmmsDbContext _context;

    public ProductService(WmmsDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<PagedResult<ProductDto>> GetProductsAsync(ProductQueryFilterDto filter, CancellationToken cancellationToken = default)
    {
        var (page, size) = MasterDataAudit.Paging(filter.Page, filter.PageSize);
        var query = _context.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            var c = filter.Category.Trim().ToLower();
            query = query.Where(p => p.CategoryName.ToLower() == c);
        }
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var st = filter.Status.Trim().ToUpperInvariant();
            query = query.Where(p => p.Status == st);
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var kw = filter.Search.Trim().ToLower();
            query = query.Where(p => p.ProductSku.ToLower().Contains(kw) || p.ProductName.ToLower().Contains(kw));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(p => p.ProductSku).Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);
        return new PagedResult<ProductDto> { Items = items.Select(ToDto).ToList(), TotalCount = total, PageNumber = page, PageSize = size };
    }

    /// <inheritdoc />
    public async Task<ProductDetailDto> GetProductByIdAsync(long productId, CancellationToken cancellationToken = default)
    {
        var p = await _context.Products.AsNoTracking()
            .Include(x => x.RoutingTemplates).ThenInclude(rt => rt.RoutingStages)
            .FirstOrDefaultAsync(x => x.ProductId == productId, cancellationToken)
            ?? throw AppException.NotFound($"Không tìm thấy sản phẩm ID = {productId}.");

        var template = p.RoutingTemplates.Where(rt => rt.IsActive).OrderByDescending(rt => rt.RoutingId).FirstOrDefault();
        var b = ToDto(p);
        return new ProductDetailDto
        {
            ProductId = b.ProductId, ProductSku = b.ProductSku, ProductName = b.ProductName, CategoryName = b.CategoryName,
            DimensionsCm = b.DimensionsCm, WoodSpecies = b.WoodSpecies, FinishColor = b.FinishColor, CbmPerUnit = b.CbmPerUnit,
            ImageUrl = b.ImageUrl, CadDrawingUrl = b.CadDrawingUrl, Status = b.Status, PackingSpec = p.PackingSpec,
            DefaultRoutingStages = template?.RoutingStages.OrderBy(s => s.StageSequence)
                .Select(s => new DefaultRoutingStageDto { StageSequence = s.StageSequence, StageName = s.StageName, IsHoldPoint = s.IsHoldPoint }).ToList()
                ?? new List<DefaultRoutingStageDto>()
        };
    }

    /// <inheritdoc />
    public async Task<ProductDto> CreateProductAsync(CreateProductDto dto, long userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var sku = dto.ProductSku.Trim();
        if (await _context.Products.AnyAsync(p => p.ProductSku.ToLower() == sku.ToLower(), cancellationToken))
            throw AppException.Conflict($"SKU '{sku}' đã tồn tại (BR-MDM-01).");

        var now = DateTime.UtcNow;
        var p = new Product
        {
            ProductSku = sku, ProductName = dto.ProductName.Trim(), CategoryName = dto.CategoryName.Trim(),
            DimensionsCm = dto.DimensionsCm.Trim(), WoodSpecies = dto.WoodSpecies?.Trim(), FinishColor = dto.FinishColor?.Trim(),
            PackingSpec = dto.PackingSpec?.Trim(), CbmPerUnit = dto.CbmPerUnit, ImageUrl = dto.ImageUrl, Status = "ACTIVE",
            CreatedAt = now, UpdatedAt = now
        };
        _context.Products.Add(p);
        await _context.SaveChangesAsync(cancellationToken);

        MasterDataAudit.Add(_context, userId, MasterDataAudit.ProductCreated, nameof(Product), p.ProductId.ToString(), null, ToDto(p), ipAddress);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(p);
    }

    /// <inheritdoc />
    public async Task<ProductDto> UpdateProductAsync(long productId, UpdateProductDto dto, long userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var p = await _context.Products.FirstOrDefaultAsync(x => x.ProductId == productId, cancellationToken)
            ?? throw AppException.NotFound($"Không tìm thấy sản phẩm ID = {productId}.");

        var before = ToDto(p);
        p.ProductName = dto.ProductName.Trim();
        p.CategoryName = dto.CategoryName.Trim();
        p.DimensionsCm = dto.DimensionsCm.Trim();
        p.WoodSpecies = dto.WoodSpecies?.Trim();
        p.FinishColor = dto.FinishColor?.Trim();
        p.PackingSpec = dto.PackingSpec?.Trim();
        p.CbmPerUnit = dto.CbmPerUnit;
        p.ImageUrl = dto.ImageUrl;
        p.Status = dto.Status.ToUpperInvariant();
        p.UpdatedAt = DateTime.UtcNow;

        MasterDataAudit.Add(_context, userId, MasterDataAudit.ProductUpdated, nameof(Product), p.ProductId.ToString(), before, ToDto(p), ipAddress);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(p);
    }

    /// <inheritdoc />
    public async Task<ProductDto> UploadCadDrawingAsync(long productId, string fileName, Stream content, long length, long userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var p = await _context.Products.FirstOrDefaultAsync(x => x.ProductId == productId, cancellationToken)
            ?? throw AppException.NotFound($"Không tìm thấy sản phẩm ID = {productId}.");

        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (!AllowedCadExtensions.Contains(ext))
            throw new AppException($"Định dạng '{ext}' không được hỗ trợ (cho phép: {string.Join(", ", AllowedCadExtensions)}).");
        if (length <= 0) throw new AppException("File bản vẽ rỗng.");
        if (length > MaxCadBytes) throw new AppException("File bản vẽ vượt quá 20MB.");

        // Tên file lưu là GUID để chống path traversal / trùng tên.
        var folder = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "cad");
        Directory.CreateDirectory(folder);
        var saved = $"{p.ProductId}_{Guid.NewGuid():N}{ext}";
        await using (var fs = new FileStream(Path.Combine(folder, saved), FileMode.Create, FileAccess.Write))
        {
            await content.CopyToAsync(fs, cancellationToken);
        }

        var before = ToDto(p);
        p.CadDrawingUrl = $"/uploads/cad/{saved}";
        p.UpdatedAt = DateTime.UtcNow;
        MasterDataAudit.Add(_context, userId, MasterDataAudit.ProductCadUploaded, nameof(Product), p.ProductId.ToString(), before, ToDto(p), ipAddress);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(p);
    }

    private static ProductDto ToDto(Product p) => new()
    {
        ProductId = p.ProductId, ProductSku = p.ProductSku, ProductName = p.ProductName, CategoryName = p.CategoryName,
        DimensionsCm = p.DimensionsCm, WoodSpecies = p.WoodSpecies, FinishColor = p.FinishColor, CbmPerUnit = p.CbmPerUnit,
        ImageUrl = p.ImageUrl, CadDrawingUrl = p.CadDrawingUrl, Status = p.Status
    };
}

