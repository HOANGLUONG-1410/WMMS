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

/// <summary>
/// Dịch vụ danh mục Vật tư (SC-04, UC-17..UC-21). Tuân thủ BR-MDM-01 (mã vật tư duy nhất) và UC-21 (soft-delete).
/// </summary>
public class MaterialService : IMaterialService
{
    private readonly WmmsDbContext _context;

    public MaterialService(WmmsDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<PagedResult<MaterialDto>> GetMaterialsAsync(MaterialQueryFilterDto filter, CancellationToken cancellationToken = default)
    {
        var (page, size) = MasterDataAudit.Paging(filter.Page, filter.PageSize);
        var query = _context.Materials.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            if (!Enum.TryParse<MaterialGroupEnum>(filter.Category, true, out var group))
                throw new AppException($"Nhóm vật tư '{filter.Category}' không hợp lệ.");
            query = query.Where(m => m.MaterialGroup == group);
        }
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var st = filter.Status.Trim().ToUpperInvariant();
            query = query.Where(m => m.Status == st);
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var kw = filter.Search.Trim().ToLower();
            query = query.Where(m => m.MaterialCode.ToLower().Contains(kw) || m.MaterialName.ToLower().Contains(kw));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(m => m.MaterialCode).Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);

        return new PagedResult<MaterialDto> { Items = items.Select(ToDto).ToList(), TotalCount = total, PageNumber = page, PageSize = size };
    }

    /// <inheritdoc />
    public async Task<MaterialDetailDto> GetMaterialByIdAsync(long materialId, CancellationToken cancellationToken = default)
    {
        var m = await _context.Materials.AsNoTracking().Include(x => x.MaterialStock)
            .FirstOrDefaultAsync(x => x.MaterialId == materialId, cancellationToken)
            ?? throw AppException.NotFound($"Không tìm thấy vật tư ID = {materialId}.");

        return new MaterialDetailDto
        {
            MaterialId = m.MaterialId, MaterialCode = m.MaterialCode, MaterialName = m.MaterialName,
            MaterialGroup = m.MaterialGroup.ToString(), UnitOfMeasure = m.UnitOfMeasure, FscCertified = m.FscCertified,
            SafetyStockQty = m.SafetyStockQty, Status = m.Status, DimensionSpec = m.DimensionSpec,
            PhysicalQty = m.MaterialStock?.PhysicalQty ?? 0, AllocatedQty = m.MaterialStock?.AllocatedQty ?? 0,
            AvailableQty = m.MaterialStock?.AvailableQty ?? 0, CreatedAt = m.CreatedAt, UpdatedAt = m.UpdatedAt
        };
    }

    /// <inheritdoc />
    public async Task<MaterialDto> CreateMaterialAsync(CreateMaterialDto dto, long userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var code = dto.MaterialCode.Trim();
        if (await _context.Materials.AnyAsync(m => m.MaterialCode.ToLower() == code.ToLower(), cancellationToken))
            throw AppException.Conflict($"Mã vật tư '{code}' đã tồn tại (BR-MDM-01).");

        var now = DateTime.UtcNow;
        var material = new Material
        {
            MaterialCode = code, MaterialName = dto.MaterialName.Trim(),
            MaterialGroup = Enum.Parse<MaterialGroupEnum>(dto.MaterialGroup, true),
            UnitOfMeasure = dto.UnitOfMeasure.Trim(), DimensionSpec = dto.DimensionSpec?.Trim(),
            FscCertified = dto.FscCertified, SafetyStockQty = dto.SafetyStockQty, Status = "ACTIVE",
            CreatedAt = now, UpdatedAt = now,
            MaterialStock = new MaterialStock { PhysicalQty = 0, AllocatedQty = 0, AvailableQty = 0, UpdatedAt = now }
        };
        _context.Materials.Add(material);
        await _context.SaveChangesAsync(cancellationToken);

        MasterDataAudit.Add(_context, userId, MasterDataAudit.MaterialCreated, nameof(Material), material.MaterialId.ToString(), null, ToDto(material), ipAddress);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(material);
    }

    /// <inheritdoc />
    public async Task<MaterialDto> UpdateMaterialAsync(long materialId, UpdateMaterialDto dto, long userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var m = await _context.Materials.FirstOrDefaultAsync(x => x.MaterialId == materialId, cancellationToken)
            ?? throw AppException.NotFound($"Không tìm thấy vật tư ID = {materialId}.");

        var before = ToDto(m);
        m.MaterialName = dto.MaterialName.Trim();
        m.MaterialGroup = Enum.Parse<MaterialGroupEnum>(dto.MaterialGroup, true);
        m.UnitOfMeasure = dto.UnitOfMeasure.Trim();
        m.DimensionSpec = dto.DimensionSpec?.Trim();
        m.FscCertified = dto.FscCertified;
        m.SafetyStockQty = dto.SafetyStockQty;
        m.Status = dto.Status.ToUpperInvariant();
        m.UpdatedAt = DateTime.UtcNow;

        MasterDataAudit.Add(_context, userId, MasterDataAudit.MaterialUpdated, nameof(Material), m.MaterialId.ToString(), before, ToDto(m), ipAddress);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(m);
    }

    /// <inheritdoc />
    public async Task DeactivateMaterialAsync(long materialId, long userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var m = await _context.Materials.FirstOrDefaultAsync(x => x.MaterialId == materialId, cancellationToken)
            ?? throw AppException.NotFound($"Không tìm thấy vật tư ID = {materialId}.");

        var before = ToDto(m);
        m.Status = "INACTIVE"; // Soft-delete: giữ nguyên tham chiếu BOM cũ (UC-21).
        m.UpdatedAt = DateTime.UtcNow;
        MasterDataAudit.Add(_context, userId, MasterDataAudit.MaterialDeactivated, nameof(Material), m.MaterialId.ToString(), before, ToDto(m), ipAddress);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static MaterialDto ToDto(Material m) => new()
    {
        MaterialId = m.MaterialId, MaterialCode = m.MaterialCode, MaterialName = m.MaterialName,
        MaterialGroup = m.MaterialGroup.ToString(), UnitOfMeasure = m.UnitOfMeasure,
        FscCertified = m.FscCertified, SafetyStockQty = m.SafetyStockQty, Status = m.Status
    };
}

