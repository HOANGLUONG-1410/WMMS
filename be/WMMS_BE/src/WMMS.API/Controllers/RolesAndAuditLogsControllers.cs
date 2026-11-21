using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.AuditLogs;
using WMMS.Application.DTOs.Roles;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;
using WMMS.Domain.Exceptions;

namespace WMMS.API.Controllers;

/// <summary>
/// Controller Quản lý Vai trò và Cấu hình Ma trận Phân quyền (UC-11: View Role List, UC-12: Configure Role Permissions).
/// Phục vụ màn hình <c>Role Permission Config (MOD-ADM-02)</c> trên Screen Flow.
/// </summary>
[ApiController]
[Route("api/v1/roles")]
[Authorize]
[Produces("application/json")]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly IValidator<UpdateRolePermissionsDto> _updatePermissionsValidator;

    public RolesController(
        IRoleService roleService,
        IValidator<UpdateRolePermissionsDto> updatePermissionsValidator)
    {
        _roleService = roleService;
        _updatePermissionsValidator = updatePermissionsValidator;
    }

    /// <summary>
    /// UC-11: Lấy danh sách toàn bộ 8 vai trò hệ thống kèm cấu hình JSON quyền hạn và số lượng nhân sự đang đảm nhận.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RoleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await _roleService.GetAllRolesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RoleDto>>.Ok(roles, "Lấy danh sách vai trò và ma trận phân quyền thành công (UC-11)."));
    }

    /// <summary>
    /// UC-12: Cập nhật cấu hình ma trận phân quyền JSON cho một vai trò.
    /// Tuân thủ BR-ADM-03: Không cho phép vô hiệu hóa quyền cốt lõi của vai trò ADMIN.
    /// </summary>
    [HttpPut("{id:int}/permissions")]
    [Authorize(Roles = RoleConstants.Admin)]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRolePermissionsAsync(
        [FromRoute] int id,
        [FromBody] UpdateRolePermissionsDto dto,
        CancellationToken cancellationToken)
    {
        await _updatePermissionsValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(sub, out var adminUserId))
        {
            throw AppException.Unauthorized("Thông tin định danh người dùng trong token không hợp lệ.");
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var updatedRole = await _roleService.UpdateRolePermissionsAsync(id, dto, adminUserId, ip, cancellationToken);

        return Ok(ApiResponse<RoleDto>.Ok(updatedRole, "Cập nhật phân quyền vai trò và ghi nhật ký kiểm toán thành công (UC-12)."));
    }
}

/// <summary>
/// Controller Tra cứu Nhật ký Kiểm toán Hệ thống bất biến &amp; Cấu hình Tham số Toàn cục (UC-13, UC-14, UC-15, GBR-05).
/// Phục vụ màn hình <c>Immutable System Audit Logs (SCR-ADM-02)</c> trên Screen Flow.
/// </summary>
[ApiController]
[Route("api/v1/audit-logs")]
[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner}")]
[Produces("application/json")]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// UC-13: Truy vấn phân trang lịch sử nhật ký kiểm toán hệ thống theo người dùng, loại hành động, thực thể và thời gian.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AuditLogDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogsAsync([FromQuery] AuditLogFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _auditLogService.GetAuditLogsAsync(filter, cancellationToken);
        return Ok(ApiResponse<PagedResult<AuditLogDto>>.Ok(result, "Lấy danh sách nhật ký kiểm toán hệ thống thành công (UC-13)."));
    }

    /// <summary>
    /// UC-13 / SCR-ADM-02: Xuất dữ liệu nhật ký kiểm toán bất biến ra file CSV (Nút "Export CSV" trên màn hình SCR-ADM-02).
    /// </summary>
    [HttpGet("export-csv")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportAuditLogsCsvAsync([FromQuery] AuditLogFilterDto filter, CancellationToken cancellationToken)
    {
        var csvBytes = await _auditLogService.ExportAuditLogsCsvAsync(filter, cancellationToken);
        var fileName = $"WMMS_AuditLogs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(csvBytes, "text/csv; charset=utf-8", fileName);
    }

    /// <summary>
    /// UC-14: Xem chi tiết một bản ghi nhật ký kiểm toán (bao gồm ảnh chụp dữ liệu JSON BeforeState và AfterState).
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<AuditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuditLogByIdAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var log = await _auditLogService.GetAuditLogByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<AuditLogDto>.Ok(log, "Lấy chi tiết nhật ký kiểm toán thành công (UC-14)."));
    }

    /// <summary>
    /// UC-15: Xem danh sách tham số cấu hình vận hành hệ thống (DEFAULT_SCRAP_MIN, DEFAULT_SCRAP_MAX, ETD_WARNING_DAYS).
    /// </summary>
    [HttpGet("system-configs")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SystemConfigDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSystemConfigsAsync(CancellationToken cancellationToken)
    {
        var configs = await _auditLogService.GetSystemConfigsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SystemConfigDto>>.Ok(configs, "Lấy danh sách tham số cấu hình hệ thống thành công (UC-15)."));
    }

    /// <summary>
    /// UC-15: Cập nhật tham số cấu hình vận hành toàn cục và ghi nhận vào nhật ký kiểm toán (BR-SYS-01, BR-002).
    /// </summary>
    [HttpPut("system-configs/{configKey}")]
    [ProducesResponseType(typeof(ApiResponse<SystemConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSystemConfigAsync(
        [FromRoute] string configKey,
        [FromBody] UpdateSystemConfigDto dto,
        CancellationToken cancellationToken)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(sub, out var actorUserId))
        {
            throw AppException.Unauthorized("Thông tin định danh người dùng trong token không hợp lệ.");
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var updated = await _auditLogService.UpdateSystemConfigAsync(configKey, dto, actorUserId, ip, cancellationToken);
        return Ok(ApiResponse<SystemConfigDto>.Ok(updated, "Lưu tham số vận hành xưởng gỗ thành công (UC-15, MSG-ADM-05)."));
    }
}
