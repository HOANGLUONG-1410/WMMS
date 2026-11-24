using Microsoft.EntityFrameworkCore;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.MasterData;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Entities.Orders;
using WMMS.Domain.Exceptions;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Services.MasterData;

/// <summary>
/// Dịch vụ quản lý Danh mục Khách hàng B2B (Master Data Customers - SC-06, SCR-CUST-01, UC-26..UC-29, BR-MDM-01, GBR-05).
/// Thực thi đầy đủ các nghiệp vụ:
/// - Kiểm soát tính duy nhất của mã khách hàng (CustomerCode).
/// - Khóa bất biến mã khách hàng khi cập nhật (Immutable Key).
/// - Chặn xóa khách hàng nếu đã phát sinh đơn hàng (Data Integrity).
/// - Ghi vết kiểm toán tự động vào bảng audit_logs cho mọi thao tác thêm, sửa, xóa.
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly WmmsDbContext _context;

    public CustomerService(WmmsDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<PagedResult<CustomerDto>> GetCustomersAsync(PartnerQueryFilterDto filter, CancellationToken cancellationToken = default)
    {
        var (page, size) = MasterDataAudit.Paging(filter.Page, filter.PageSize);
        var query = _context.Customers
            .AsNoTracking()
            .Include(c => c.SalesOrders)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var kw = filter.Search.Trim().ToLower();
            query = query.Where(c =>
                c.CustomerCode.ToLower().Contains(kw) ||
                c.CompanyName.ToLower().Contains(kw) ||
                (c.ContactPerson != null && c.ContactPerson.ToLower().Contains(kw)) ||
                (c.Email != null && c.Email.ToLower().Contains(kw)) ||
                (c.Country != null && c.Country.ToLower().Contains(kw)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(c => c.CustomerCode)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerDto>
        {
            Items = items.Select(ToDto).ToList(),
            TotalCount = total,
            PageNumber = page,
            PageSize = size
        };
    }

    /// <inheritdoc />
    public async Task<CustomerDto> GetCustomerByIdAsync(long customerId, CancellationToken cancellationToken = default)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .Include(c => c.SalesOrders)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);

        if (customer == null)
        {
            throw AppException.NotFound($"Không tìm thấy thông tin khách hàng với ID #{customerId}.");
        }

        return ToDto(customer);
    }

    /// <inheritdoc />
    public async Task<CustomerDto> GetCustomerByCodeAsync(string customerCode, CancellationToken cancellationToken = default)
    {
        var code = customerCode.Trim();
        var customer = await _context.Customers
            .AsNoTracking()
            .Include(c => c.SalesOrders)
            .FirstOrDefaultAsync(c => c.CustomerCode.ToLower() == code.ToLower(), cancellationToken);

        if (customer == null)
        {
            throw AppException.NotFound($"Không tìm thấy thông tin khách hàng với mã '{code}'.");
        }

        return ToDto(customer);
    }

    /// <inheritdoc />
    public async Task<CustomerDto> CreateCustomerAsync(CreateCustomerDto dto, long userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var code = dto.CustomerCode.Trim().ToUpperInvariant();
        if (await _context.Customers.AnyAsync(c => c.CustomerCode.ToLower() == code.ToLower(), cancellationToken))
        {
            throw AppException.Conflict($"Mã khách hàng '{code}' đã tồn tại trong hệ thống. Vui lòng chọn mã khác.");
        }

        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            CustomerCode = code,
            CompanyName = dto.CompanyName.Trim(),
            ContactPerson = dto.ContactPerson?.Trim(),
            Email = dto.Email?.Trim(),
            Country = dto.Country?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(cancellationToken);

        MasterDataAudit.Add(
            _context,
            userId,
            MasterDataAudit.CustomerCreated,
            nameof(Customer),
            customer.CustomerId.ToString(),
            null,
            ToDto(customer),
            ipAddress,
            $"Thêm mới khách hàng '{customer.CompanyName}' (Mã: {customer.CustomerCode}).");

        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(customer);
    }

    /// <inheritdoc />
    public async Task<CustomerDto> UpdateCustomerAsync(long customerId, UpdateCustomerDto dto, long userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var customer = await _context.Customers
            .Include(c => c.SalesOrders)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);

        if (customer == null)
        {
            throw AppException.NotFound($"Không tìm thấy thông tin khách hàng với ID #{customerId}.");
        }

        var before = ToDto(customer);

        customer.CompanyName = dto.CompanyName.Trim();
        customer.ContactPerson = dto.ContactPerson?.Trim();
        customer.Email = dto.Email?.Trim();
        customer.Country = dto.Country?.Trim();
        customer.UpdatedAt = DateTime.UtcNow;

        MasterDataAudit.Add(
            _context,
            userId,
            MasterDataAudit.CustomerUpdated,
            nameof(Customer),
            customer.CustomerId.ToString(),
            before,
            ToDto(customer),
            ipAddress,
            $"Cập nhật thông tin khách hàng '{customer.CompanyName}' (Mã: {customer.CustomerCode}).");

        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(customer);
    }

    /// <inheritdoc />
    public async Task DeleteCustomerAsync(long customerId, long userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var customer = await _context.Customers
            .Include(c => c.SalesOrders)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);

        if (customer == null)
        {
            throw AppException.NotFound($"Không tìm thấy thông tin khách hàng với ID #{customerId}.");
        }

        // Kiểm tra ràng buộc khóa ngoại: Nếu đã có đơn hàng thì không cho phép xóa
        bool hasOrders = await _context.CustomerOrders.AnyAsync(o => o.CustomerId == customerId, cancellationToken);
        if (hasOrders || customer.SalesOrders.Any())
        {
            throw AppException.Conflict(
                $"Không thể xóa khách hàng '{customer.CompanyName}' (Mã: {customer.CustomerCode}) vì đã có đơn hàng phát sinh trong hệ thống. Vui lòng kiểm tra lại đơn hàng liên quan.");
        }

        var before = ToDto(customer);

        _context.Customers.Remove(customer);

        MasterDataAudit.Add(
            _context,
            userId,
            MasterDataAudit.CustomerDeleted,
            nameof(Customer),
            customer.CustomerId.ToString(),
            before,
            null,
            ipAddress,
            $"Xóa khách hàng '{customer.CompanyName}' (Mã: {customer.CustomerCode}).");

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static CustomerDto ToDto(Customer c) => new()
    {
        CustomerId = c.CustomerId,
        CustomerCode = c.CustomerCode,
        CompanyName = c.CompanyName,
        ContactPerson = c.ContactPerson,
        Email = c.Email,
        Country = c.Country,
        TotalOrdersCount = c.SalesOrders?.Count ?? 0,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
}
