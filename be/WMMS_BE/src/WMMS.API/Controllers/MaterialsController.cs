using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.MasterData;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;

namespace WMMS.API.Controllers;

/// <summary>
/// [Dev-Iter 1] Controller Quản lý Danh mục Vật tư (SC-04 / SCR-MAT-01, UC-17..UC-21, BR-MDM-01).
/// <para>
/// - Chức năng: Cung cấp REST API tra cứu phân trang, xem chi tiết kèm tồn kho 3 cấp, thêm mới, cập nhật và ngừng kích hoạt (soft-delete) nguyên vật liệu xưởng gỗ.
/// - Phân quyền RBAC: Đọc (ADMIN, OWNER, PLANNER, PURCHASING, WAREHOUSE); Ghi (ADMIN, PLANNER, WAREHOUSE).
/// - Móc nối: Nhận DTO từ FE -&gt; Validate qua <c>MasterDataValidators</c> -&gt; Gọi <see cref="IMaterialService"/> (<c>MaterialService</c>)
///   -&gt; Lưu bảng <c>materials</c>, tự động khởi tạo dòng tồn kho 0 tại bảng <c>inventory_stocks</c> (móc nối sang SC-12/SC-14) và ghi vết <c>audit_logs</c>.
/// </para>
/// </summary>
[Route("api/v1/materials")]
[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Purchasing},{RoleConstants.Warehouse}")]
public class MaterialsController : MasterDataControllerBase
{
    private const string WriteRoles = $"{RoleConstants.Admin},{RoleConstants.Planner},{RoleConstants.Warehouse}";

    private readonly IMaterialService _service;
    private readonly IValidator<CreateMaterialDto> _createValidator;
    private readonly IValidator<UpdateMaterialDto> _updateValidator;

    public MaterialsController(IMaterialService service, IValidator<CreateMaterialDto> createValidator, IValidator<UpdateMaterialDto> updateValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>UC-17: Danh sách vật tư phân trang (lọc Category/Status/Search).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<MaterialDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMaterialsAsync([FromQuery] MaterialQueryFilterDto filter, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<MaterialDto>>.Ok(await _service.GetMaterialsAsync(filter, ct), "Đã tải danh sách nguyên vật liệu."));

    /// <summary>UC-18: Chi tiết vật tư kèm tồn kho. 404 (MSG10) nếu không tồn tại.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<MaterialDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMaterialByIdAsync([FromRoute] long id, CancellationToken ct)
        => Ok(ApiResponse<MaterialDetailDto>.Ok(await _service.GetMaterialByIdAsync(id, ct), "Đã tải thông tin chi tiết nguyên vật liệu."));

    /// <summary>UC-19: Tạo vật tư mới (BR-MDM-01: mã duy nhất, 409 MSG08 nếu trùng).</summary>
    [HttpPost]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(typeof(ApiResponse<MaterialDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateMaterialAsync([FromBody] CreateMaterialDto dto, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(dto, ct);
        var created = await _service.CreateMaterialAsync(dto, GetCurrentUserId(), GetClientIpAddress(), ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<MaterialDto>.Ok(created, "Đã thêm nguyên vật liệu mới thành công."));
    }

    /// <summary>UC-20: Cập nhật vật tư (MaterialCode bất biến).</summary>
    [HttpPut("{id:long}")]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(typeof(ApiResponse<MaterialDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMaterialAsync([FromRoute] long id, [FromBody] UpdateMaterialDto dto, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, ct);
        var updated = await _service.UpdateMaterialAsync(id, dto, GetCurrentUserId(), GetClientIpAddress(), ct);
        return Ok(ApiResponse<MaterialDto>.Ok(updated, "Đã cập nhật thông tin nguyên vật liệu thành công."));
    }

    /// <summary>UC-21: Vô hiệu hóa vật tư (soft-delete, giữ tham chiếu BOM).</summary>
    [HttpDelete("{id:long}")]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateMaterialAsync([FromRoute] long id, CancellationToken ct)
    {
        await _service.DeactivateMaterialAsync(id, GetCurrentUserId(), GetClientIpAddress(), ct);
        return Ok(ApiResponse.Ok("Đã ngừng kích hoạt nguyên vật liệu thành công."));
    }
}
