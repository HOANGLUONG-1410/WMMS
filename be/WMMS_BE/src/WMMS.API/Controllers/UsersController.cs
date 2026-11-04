using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.Users;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;
using WMMS.Domain.Exceptions;

namespace WMMS.API.Controllers;

/// <summary>
/// Controller Quản trị Tài khoản &amp; Phân quyền Đa vai trò (Phân hệ ADM - UC-06 đến UC-10, Report 4 Sequence Diagram 3.3.1 <c>UsersController</c>).
/// Phục vụ các màn hình trên Screen Flow: User &amp; Multi-Role RBAC, Create User, Edit User.
/// Chỉ cho phép vai trò <c>ADMIN</c> (hoặc <c>OWNER</c> khi xem danh sách/chi tiết) truy cập.
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner}")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IValidator<CreateStaffDto> _createStaffValidator;
    private readonly IValidator<UpdateUserDto> _updateUserValidator;
    private readonly IValidator<UpdateUserStatusDto> _updateStatusValidator;

    public UsersController(
        IUserService userService,
        IValidator<CreateStaffDto> createStaffValidator,
        IValidator<UpdateUserDto> updateUserValidator,
        IValidator<UpdateUserStatusDto> updateStatusValidator)
    {
        _userService = userService;
        _createStaffValidator = createStaffValidator;
        _updateUserValidator = updateUserValidator;
        _updateStatusValidator = updateStatusValidator;
    }

    /// <summary>
    /// UC-06: Xem danh sách tài khoản người dùng có phân trang, tìm kiếm (Username, Email, FullName, Phone) và lọc theo Role / Status.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<UserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsersAsync([FromQuery] UserQueryFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _userService.GetUsersAsync(filter, cancellationToken);
        return Ok(ApiResponse<PagedResult<UserDto>>.Ok(result, "Fetched paginated user account list (UC-06)."));
    }

    /// <summary>
    /// UC-07: Xem thông tin chi tiết một tài khoản người dùng và danh sách đa vai trò.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserByIdAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var user = await _userService.GetUserByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<UserDto>.Ok(user, "Fetched user account details (UC-07)."));
    }

    /// <summary>
    /// UC-08: Tạo mới tài khoản nhân sự và gán 1..* vai trò (Report 4 Sequence Diagram 3.3.1 <c>POST /api/v1/users</c>).
    /// Trả về HTTP 201 Created nếu thành công, hoặc HTTP 409 Conflict (MSG08) nếu trùng Username/Email.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = RoleConstants.Admin)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateStaffAccountAsync([FromBody] CreateStaffDto dto, CancellationToken cancellationToken)
    {
        await _createStaffValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var adminUserId = GetCurrentUserId();
        var createdUser = await _userService.CreateStaffAccountAsync(dto, adminUserId, GetClientIpAddress(), cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<UserDto>.Ok(createdUser, "Staff account created and roles assigned successfully (UC-08)."));
    }

    /// <summary>
    /// UC-09: Cập nhật thông tin hồ sơ tài khoản (FullName, Email, Phone) và tùy chọn đồng bộ lại vai trò.
    /// </summary>
    [HttpPut("{id:long}")]
    [Authorize(Roles = RoleConstants.Admin)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateUserAsync(
        [FromRoute] long id,
        [FromBody] UpdateUserDto dto,
        CancellationToken cancellationToken)
    {
        await _updateUserValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var adminUserId = GetCurrentUserId();
        var updatedUser = await _userService.UpdateUserAsync(id, dto, adminUserId, GetClientIpAddress(), cancellationToken);

        return Ok(ApiResponse<UserDto>.Ok(updatedUser, "User profile updated successfully (UC-09)."));
    }

    /// <summary>
    /// UC-09: Cập nhật riêng danh sách đa vai trò (Multi-Role RBAC - GBR-04) cho tài khoản nhân sự.
    /// </summary>
    [HttpPut("{id:long}/roles")]
    [Authorize(Roles = RoleConstants.Admin)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserRolesAsync(
        [FromRoute] long id,
        [FromBody] UpdateUserRolesDto dto,
        CancellationToken cancellationToken)
    {
        var adminUserId = GetCurrentUserId();
        var updatedUser = await _userService.UpdateStaffRolesAsync(id, dto, adminUserId, GetClientIpAddress(), cancellationToken);

        return Ok(ApiResponse<UserDto>.Ok(updatedUser, "User multi-role assignments updated successfully (UC-09, GBR-04)."));
    }

    /// <summary>
    /// UC-10: Khóa (BANNED) / Mở khóa (ACTIVE) / Vô hiệu hóa (INACTIVE) tài khoản nhân sự.
    /// Yêu cầu nhập lý do bắt buộc (Reason), chặn Admin tự khóa chính mình (BR-ADM-01) và tự động thu hồi toàn bộ phiên đăng nhập khi khóa.
    /// </summary>
    [HttpPatch("{id:long}/status")]
    [Authorize(Roles = RoleConstants.Admin)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserStatusAsync(
        [FromRoute] long id,
        [FromBody] UpdateUserStatusDto dto,
        CancellationToken cancellationToken)
    {
        await _updateStatusValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var adminUserId = GetCurrentUserId();
        var updatedUser = await _userService.UpdateUserStatusAsync(id, dto, adminUserId, GetClientIpAddress(), cancellationToken);

        return Ok(ApiResponse<UserDto>.Ok(updatedUser, $"Account status changed to {updatedUser.Status} (UC-10)."));
    }

    private long GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(sub, out var userId))
        {
            throw AppException.Unauthorized("Invalid user identity claim.");
        }
        return userId;
    }

    private string? GetClientIpAddress()
    {
        if (Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            return forwardedFor.FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();
        }
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
