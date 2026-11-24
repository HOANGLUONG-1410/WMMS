using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.MasterData;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;

namespace WMMS.API.Controllers;

/// <summary>
/// Controller Quản lý Danh mục Khách hàng B2B (Master Data Customers - SC-06, SCR-CUST-01, UC-26..UC-29, BR-MDM-01, GBR-05).
/// Cung cấp đầy đủ các thao tác RESTful CRUD:
/// - Danh sách phân trang, tìm kiếm theo từ khóa (Mã, Tên công ty, Người liên hệ, Email, Quốc gia).
/// - Tra cứu chi tiết theo ID hoặc theo Mã khách hàng (CustomerCode).
/// - Thêm mới khách hàng (chống trùng mã 409 Conflict).
/// - Cập nhật thông tin khách hàng (mã bất biến).
/// - Xóa khách hàng (kiểm tra an toàn: cấm xóa nếu đã phát sinh đơn hàng trong sales_orders).
/// </summary>
[Route("api/v1/customers")]
[Authorize]
public class CustomersController : MasterDataControllerBase
{
    private const string ReadRoles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Sales},{RoleConstants.Planner},{RoleConstants.Purchasing},{RoleConstants.Warehouse}";
    private const string WriteRoles = $"{RoleConstants.Admin},{RoleConstants.Sales},{RoleConstants.Owner}";

    private readonly ICustomerService _customerService;
    private readonly IValidator<CreateCustomerDto> _createValidator;
    private readonly IValidator<UpdateCustomerDto> _updateValidator;

    public CustomersController(
        ICustomerService customerService,
        IValidator<CreateCustomerDto> createValidator,
        IValidator<UpdateCustomerDto> updateValidator)
    {
        _customerService = customerService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>
    /// [SC-06]: Lấy danh sách khách hàng phân trang, hỗ trợ tìm kiếm từ khóa.
    /// </summary>
    /// <param name="filter">Bộ lọc chứa Search, Page, PageSize.</param>
    /// <param name="ct">Token hủy tác vụ.</param>
    /// <returns>Danh sách khách hàng phân trang.</returns>
    [HttpGet]
    [Authorize(Roles = ReadRoles)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CustomerDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomersAsync([FromQuery] PartnerQueryFilterDto filter, CancellationToken ct)
    {
        var result = await _customerService.GetCustomersAsync(filter, ct);
        return Ok(ApiResponse<PagedResult<CustomerDto>>.Ok(result, "Tải danh sách khách hàng thành công."));
    }

    /// <summary>
    /// [SC-06]: Lấy thông tin chi tiết một khách hàng theo định danh ID.
    /// </summary>
    /// <param name="id">ID định danh khách hàng.</param>
    /// <param name="ct">Token hủy tác vụ.</param>
    /// <returns>Thông tin chi tiết khách hàng.</returns>
    [HttpGet("{id:long}")]
    [Authorize(Roles = ReadRoles)]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCustomerByIdAsync([FromRoute] long id, CancellationToken ct)
    {
        var result = await _customerService.GetCustomerByIdAsync(id, ct);
        return Ok(ApiResponse<CustomerDto>.Ok(result, "Tải thông tin chi tiết khách hàng thành công."));
    }

    /// <summary>
    /// [SC-06]: Tra cứu thông tin khách hàng theo mã khách hàng (CustomerCode).
    /// </summary>
    /// <param name="customerCode">Mã khách hàng duy nhất (VD: CUST-ASHLEY-US).</param>
    /// <param name="ct">Token hủy tác vụ.</param>
    /// <returns>Thông tin chi tiết khách hàng.</returns>
    [HttpGet("by-code/{customerCode}")]
    [Authorize(Roles = ReadRoles)]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCustomerByCodeAsync([FromRoute] string customerCode, CancellationToken ct)
    {
        var result = await _customerService.GetCustomerByCodeAsync(customerCode, ct);
        return Ok(ApiResponse<CustomerDto>.Ok(result, "Tra cứu khách hàng theo mã thành công."));
    }

    /// <summary>
    /// [SC-06]: Thêm mới khách hàng vào danh mục Master Data (Mã khách hàng là duy nhất, ném lỗi 409 Conflict nếu trùng).
    /// </summary>
    /// <param name="dto">Dữ liệu tạo mới khách hàng.</param>
    /// <param name="ct">Token hủy tác vụ.</param>
    /// <returns>Thông tin khách hàng vừa được tạo.</returns>
    [HttpPost]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCustomerAsync([FromBody] CreateCustomerDto dto, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(dto, ct);
        var created = await _customerService.CreateCustomerAsync(dto, GetCurrentUserId(), GetClientIpAddress(), ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<CustomerDto>.Ok(created, "Thêm mới khách hàng thành công."));
    }

    /// <summary>
    /// [SC-06]: Cập nhật thông tin khách hàng theo ID (Mã khách hàng là bất biến để bảo toàn tính toàn vẹn dữ liệu đơn hàng).
    /// </summary>
    /// <param name="id">ID định danh khách hàng.</param>
    /// <param name="dto">Dữ liệu cập nhật khách hàng.</param>
    /// <param name="ct">Token hủy tác vụ.</param>
    /// <returns>Thông tin khách hàng sau khi cập nhật.</returns>
    [HttpPut("{id:long}")]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCustomerAsync([FromRoute] long id, [FromBody] UpdateCustomerDto dto, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, ct);
        var updated = await _customerService.UpdateCustomerAsync(id, dto, GetCurrentUserId(), GetClientIpAddress(), ct);
        return Ok(ApiResponse<CustomerDto>.Ok(updated, "Cập nhật thông tin khách hàng thành công."));
    }

    /// <summary>
    /// [SC-06]: Xóa khách hàng khỏi hệ thống (Chỉ cho phép xóa khi khách hàng chưa phát sinh đơn hàng trong sales_orders).
    /// </summary>
    /// <param name="id">ID định danh khách hàng.</param>
    /// <param name="ct">Token hủy tác vụ.</param>
    /// <returns>Thông báo kết quả xóa.</returns>
    [HttpDelete("{id:long}")]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCustomerAsync([FromRoute] long id, CancellationToken ct)
    {
        await _customerService.DeleteCustomerAsync(id, GetCurrentUserId(), GetClientIpAddress(), ct);
        return Ok(ApiResponse<object>.Ok(new { CustomerId = id }, "Đã xóa khách hàng thành công."));
    }
}
