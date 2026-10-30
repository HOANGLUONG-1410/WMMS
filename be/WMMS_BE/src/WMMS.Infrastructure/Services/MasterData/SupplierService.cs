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

/// <summary>Dịch vụ danh mục Đối tác: Nhà cung cấp & Khách hàng (SC-06, UC-26..UC-29).</summary>
public class SupplierService : ISupplierService
{
    private readonly WmmsDbContext _context;

    public SupplierService(WmmsDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<PagedResult<SupplierDto>> GetSuppliersAsync(PartnerQueryFilterDto filter, CancellationToken cancellationToken = default)
    {
        var (page, size) = MasterDataAudit.Paging(filter.Page, filter.PageSize);
        var query = _context.Suppliers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var st = filter.Status.Trim().ToUpperInvariant();
            query = query.Where(s => s.Status == st);
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var kw = filter.Search.Trim().ToLower();
            query = query.Where(s => s.SupplierCode.ToLower().Contains(kw) || s.SupplierName.ToLower().Contains(kw) || s.SupplyCategory.ToLower().Contains(kw));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(s => s.SupplierCode).Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);
        return new PagedResult<SupplierDto> { Items = items.Select(ToDto).ToList(), TotalCount = total, PageNumber = page, PageSize = size };
    }

    /// <inheritdoc />
    public async Task<SupplierDto> GetSupplierByIdAsync(long supplierId, CancellationToken cancellationToken = default)
    {
        var s = await _context.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.SupplierId == supplierId, cancellationToken)
            ?? throw AppException.NotFound($"Không tìm thấy nhà cung cấp ID = {supplierId}.");
        return ToDto(s);
    }

    /// <inheritdoc />
    public async Task<SupplierDto> CreateSupplierAsync(CreateSupplierDto dto, long userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var code = dto.SupplierCode.Trim();
        if (await _context.Suppliers.AnyAsync(s => s.SupplierCode.ToLower() == code.ToLower(), cancellationToken))
            throw AppException.Conflict($"Mã nhà cung cấp '{code}' đã tồn tại (BR-MDM-01).");

        var now = DateTime.UtcNow;
        var s = new Supplier
        {
            SupplierCode = code, SupplierName = dto.SupplierName.Trim(), SupplyCategory = dto.SupplyCategory.Trim(),
            ContactEmail = dto.ContactEmail?.Trim(), Phone = dto.Phone?.Trim(), LeadTimeDays = dto.LeadTimeDays,
            Status = "ACTIVE", CreatedAt = now, UpdatedAt = now
        };
        _context.Suppliers.Add(s);
        await _context.SaveChangesAsync(cancellationToken);

        MasterDataAudit.Add(_context, userId, MasterDataAudit.SupplierCreated, nameof(Supplier), s.SupplierId.ToString(), null, ToDto(s), ipAddress);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(s);
    }

    /// <inheritdoc />
    public async Task<SupplierDto> UpdateSupplierAsync(long supplierId, UpdateSupplierDto dto, long userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var s = await _context.Suppliers.FirstOrDefaultAsync(x => x.SupplierId == supplierId, cancellationToken)
            ?? throw AppException.NotFound($"Không tìm thấy nhà cung cấp ID = {supplierId}.");

        var before = ToDto(s);
        s.SupplierName = dto.SupplierName.Trim();
        s.SupplyCategory = dto.SupplyCategory.Trim();
        s.ContactEmail = dto.ContactEmail?.Trim();
        s.Phone = dto.Phone?.Trim();
        s.LeadTimeDays = dto.LeadTimeDays;
        s.Status = dto.Status.ToUpperInvariant();
        s.UpdatedAt = DateTime.UtcNow;

        MasterDataAudit.Add(_context, userId, MasterDataAudit.SupplierUpdated, nameof(Supplier), s.SupplierId.ToString(), before, ToDto(s), ipAddress);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(s);
    }

    /// <inheritdoc />
    public async Task<PagedResult<CustomerDto>> GetCustomersAsync(PartnerQueryFilterDto filter, CancellationToken cancellationToken = default)
    {
        var (page, size) = MasterDataAudit.Paging(filter.Page, filter.PageSize);
        var query = _context.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var kw = filter.Search.Trim().ToLower();
            query = query.Where(c => c.CustomerCode.ToLower().Contains(kw) || c.CompanyName.ToLower().Contains(kw));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(c => c.CustomerCode).Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);
        return new PagedResult<CustomerDto> { Items = items.Select(ToDto).ToList(), TotalCount = total, PageNumber = page, PageSize = size };
    }

    /// <inheritdoc />
    public async Task<CustomerDto> CreateCustomerAsync(CreateCustomerDto dto, long userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var code = dto.CustomerCode.Trim();
        if (await _context.Customers.AnyAsync(c => c.CustomerCode.ToLower() == code.ToLower(), cancellationToken))
            throw AppException.Conflict($"Mã khách hàng '{code}' đã tồn tại (BR-MDM-01).");

        var now = DateTime.UtcNow;
        var c = new Customer
        {
            CustomerCode = code, CompanyName = dto.CompanyName.Trim(), ContactPerson = dto.ContactPerson?.Trim(),
            Email = dto.Email?.Trim(), Country = dto.Country?.Trim(), CreatedAt = now, UpdatedAt = now
        };
        _context.Customers.Add(c);
        await _context.SaveChangesAsync(cancellationToken);

        MasterDataAudit.Add(_context, userId, MasterDataAudit.CustomerCreated, nameof(Customer), c.CustomerId.ToString(), null, ToDto(c), ipAddress);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(c);
    }

    private static SupplierDto ToDto(Supplier s) => new()
    {
        SupplierId = s.SupplierId, SupplierCode = s.SupplierCode, SupplierName = s.SupplierName, SupplyCategory = s.SupplyCategory,
        ContactEmail = s.ContactEmail, Phone = s.Phone, LeadTimeDays = s.LeadTimeDays, Status = s.Status
    };

    private static CustomerDto ToDto(Customer c) => new()
    {
        CustomerId = c.CustomerId, CustomerCode = c.CustomerCode, CompanyName = c.CompanyName,
        ContactPerson = c.ContactPerson, Email = c.Email, Country = c.Country
    };
}
