using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.MasterData;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;

namespace WMMS.API.Controllers;

/// <summary>
/// Controller Danh mục Đối tác (SC-06 / SCR-SUP-01, UC-26..UC-29): Nhà cung cấp và Khách hàng.
/// NCC đọc: ADMIN, OWNER, PURCHASING, PLANNER, WAREHOUSE; ghi: ADMIN, PURCHASING. Khách hàng đọc: ADMIN, OWNER, SALES; tạo: ADMIN, SALES.
/// </summary>
[Route("api/v1/suppliers")]
[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Purchasing},{RoleConstants.Planner},{RoleConstants.Warehouse}")]
public class SuppliersController : MasterDataControllerBase
{
    private const string WriteRoles = $"{RoleConstants.Admin},{RoleConstants.Purchasing}";
    private const string CustomerRoles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Sales}";
    private const string CustomerWriteRoles = $"{RoleConstants.Admin},{RoleConstants.Sales}";

    private readonly ISupplierService _service;
    private readonly IValidator<CreateSupplierDto> _createValidator;
    private readonly IValidator<UpdateSupplierDto> _updateValidator;
    private readonly IValidator<CreateCustomerDto> _customerValidator;

    public SuppliersController(
        ISupplierService service,
        IValidator<CreateSupplierDto> createValidator,
        IValidator<UpdateSupplierDto> updateValidator,
        IValidator<CreateCustomerDto> customerValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _customerValidator = customerValidator;
    }

    /// <summary>UC-26: Danh sách nhà cung cấp phân trang.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<SupplierDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuppliersAsync([FromQuery] PartnerQueryFilterDto filter, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<SupplierDto>>.Ok(await _service.GetSuppliersAsync(filter, ct), "Fetched supplier list (UC-26)."));

    /// <summary>UC-27: Chi tiết nhà cung cấp.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<SupplierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSupplierByIdAsync([FromRoute] long id, CancellationToken ct)
        => Ok(ApiResponse<SupplierDto>.Ok(await _service.GetSupplierByIdAsync(id, ct), "Fetched supplier details (UC-27)."));

    /// <summary>UC-28: Tạo nhà cung cấp (mã duy nhất, 409 MSG08 nếu trùng).</summary>
    [HttpPost]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(typeof(ApiResponse<SupplierDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSupplierAsync([FromBody] CreateSupplierDto dto, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(dto, ct);
        var created = await _service.CreateSupplierAsync(dto, GetCurrentUserId(), GetClientIpAddress(), ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<SupplierDto>.Ok(created, "Supplier created (UC-28)."));
    }

    /// <summary>UC-29: Cập nhật nhà cung cấp (mã bất biến).</summary>
    [HttpPut("{id:long}")]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(typeof(ApiResponse<SupplierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSupplierAsync([FromRoute] long id, [FromBody] UpdateSupplierDto dto, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, ct);
        var updated = await _service.UpdateSupplierAsync(id, dto, GetCurrentUserId(), GetClientIpAddress(), ct);
        return Ok(ApiResponse<SupplierDto>.Ok(updated, "Supplier updated (UC-29)."));
    }

    /// <summary>SC-06: Danh sách khách hàng phân trang.</summary>
    [HttpGet("~/api/v1/customers")]
    [Authorize(Roles = CustomerRoles)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CustomerDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomersAsync([FromQuery] PartnerQueryFilterDto filter, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<CustomerDto>>.Ok(await _service.GetCustomersAsync(filter, ct), "Fetched customer list."));

    /// <summary>SC-06: Tạo khách hàng (mã duy nhất, 409 MSG08 nếu trùng).</summary>
    [HttpPost("~/api/v1/customers")]
    [Authorize(Roles = CustomerWriteRoles)]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCustomerAsync([FromBody] CreateCustomerDto dto, CancellationToken ct)
    {
        await _customerValidator.ValidateAndThrowAsync(dto, ct);
        var created = await _service.CreateCustomerAsync(dto, GetCurrentUserId(), GetClientIpAddress(), ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<CustomerDto>.Ok(created, "Customer created."));
    }
}
