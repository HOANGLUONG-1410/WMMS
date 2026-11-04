using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.MasterData;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;
using WMMS.Domain.Exceptions;

namespace WMMS.API.Controllers;

/// <summary>
/// Controller Danh mục Sản phẩm (SC-05 / SCR-PRD-01, UC-22..UC-25).
/// Đọc: ADMIN, OWNER, SALES, PLANNER, LEADER, QC. Ghi: ADMIN, PLANNER.
/// </summary>
[Route("api/v1/products")]
[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Sales},{RoleConstants.Planner},{RoleConstants.Leader},{RoleConstants.Qc}")]
public class ProductsController : MasterDataControllerBase
{
    private const string WriteRoles = $"{RoleConstants.Admin},{RoleConstants.Planner}";

    private readonly IProductService _service;
    private readonly IValidator<CreateProductDto> _createValidator;
    private readonly IValidator<UpdateProductDto> _updateValidator;

    public ProductsController(IProductService service, IValidator<CreateProductDto> createValidator, IValidator<UpdateProductDto> updateValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>UC-22: Danh sách sản phẩm phân trang (Search không phân biệt hoa thường, Category, Status).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ProductDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProductsAsync([FromQuery] ProductQueryFilterDto filter, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<ProductDto>>.Ok(await _service.GetProductsAsync(filter, ct), "Fetched product list (UC-22)."));

    /// <summary>UC-23: Chi tiết sản phẩm kèm công đoạn định tuyến mặc định.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<ProductDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductByIdAsync([FromRoute] long id, CancellationToken ct)
        => Ok(ApiResponse<ProductDetailDto>.Ok(await _service.GetProductByIdAsync(id, ct), "Fetched product details (UC-23)."));

    /// <summary>UC-24: Tạo sản phẩm (SKU duy nhất, 409 MSG08 nếu trùng).</summary>
    [HttpPost]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProductAsync([FromBody] CreateProductDto dto, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(dto, ct);
        var created = await _service.CreateProductAsync(dto, GetCurrentUserId(), GetClientIpAddress(), ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ProductDto>.Ok(created, "Product created (UC-24)."));
    }

    /// <summary>UC-24: Cập nhật sản phẩm (SKU bất biến).</summary>
    [HttpPut("{id:long}")]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProductAsync([FromRoute] long id, [FromBody] UpdateProductDto dto, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, ct);
        var updated = await _service.UpdateProductAsync(id, dto, GetCurrentUserId(), GetClientIpAddress(), ct);
        return Ok(ApiResponse<ProductDto>.Ok(updated, "Product updated (UC-24)."));
    }

    /// <summary>UC-25: Tải bản vẽ CAD/ảnh (.dwg, .dxf, .pdf, .png, .jpg; tối đa 20MB).</summary>
    [HttpPost("{id:long}/cad-drawing")]
    [Authorize(Roles = WriteRoles)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadCadDrawingAsync([FromRoute] long id, IFormFile? file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            throw new AppException("Vui lòng chọn file bản vẽ (MSG01).");
        }

        await using var stream = file.OpenReadStream();
        var updated = await _service.UploadCadDrawingAsync(id, file.FileName, stream, file.Length, GetCurrentUserId(), GetClientIpAddress(), ct);
        return Ok(ApiResponse<ProductDto>.Ok(updated, "CAD drawing uploaded (UC-25)."));
    }
}
