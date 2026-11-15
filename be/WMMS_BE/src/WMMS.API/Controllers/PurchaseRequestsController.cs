using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.InventoryPr;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;

namespace WMMS.API.Controllers;

/// <summary>
/// Controller Quản lý Phiếu Yêu cầu Mua hàng gộp (PR) &amp; Luồng phê duyệt của Chủ xưởng (SC-15, SCR-18).
/// Use case: UC-45 (Danh sách PR), UC-46 (Chi tiết PR), UC-47 (Phê duyệt PR), UC-48 (Từ chối PR).
/// Quyền (BR-PUR-02): xem PR cho ADMIN/OWNER/PLANNER/PURCHASING (Purchasing chỉ thấy PR đã duyệt);
/// phê duyệt/từ chối chỉ dành cho OWNER.
/// </summary>
[Route("api/v1/purchase-requests")]
[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Purchasing}")]
public class PurchaseRequestsController : MasterDataControllerBase
{
    private readonly IPurchaseRequestService _purchaseRequestService;
    private readonly IValidator<PurchaseRequestFilterDto> _filterValidator;
    private readonly IValidator<ApprovePurchaseRequestDto> _approveValidator;
    private readonly IValidator<RejectPurchaseRequestDto> _rejectValidator;

    public PurchaseRequestsController(
        IPurchaseRequestService purchaseRequestService,
        IValidator<PurchaseRequestFilterDto> filterValidator,
        IValidator<ApprovePurchaseRequestDto> approveValidator,
        IValidator<RejectPurchaseRequestDto> rejectValidator)
    {
        _purchaseRequestService = purchaseRequestService;
        _filterValidator = filterValidator;
        _approveValidator = approveValidator;
        _rejectValidator = rejectValidator;
    }

    /// <summary>
    /// UC-45: Danh sách PR gộp có phân trang, lọc theo trạng thái, khoảng ngày tổng hợp và từ khóa.
    /// Purchasing chỉ thấy PR APPROVED/PO_ISSUED (BR-PUR-02).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PurchaseRequestListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPurchaseRequestsAsync(
        [FromQuery] PurchaseRequestFilterDto filter,
        CancellationToken cancellationToken)
    {
        await _filterValidator.ValidateAndThrowAsync(filter, cancellationToken);

        var result = await _purchaseRequestService.GetPurchaseRequestsAsync(filter, IsPurchasingOnly(), cancellationToken);
        return Ok(ApiResponse<PagedResult<PurchaseRequestListItemDto>>.Ok(result, "Lấy danh sách phiếu yêu cầu mua hàng gộp thành công (UC-45)."));
    }

    /// <summary>
    /// UC-46: Chi tiết một PR gộp: dòng vật tư, tổng thiếu hụt, lượng đề xuất mua, ETD sớm nhất và các đơn hàng nguồn.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseRequestDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPurchaseRequestByIdAsync(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var detail = await _purchaseRequestService.GetPurchaseRequestByIdAsync(id, IsPurchasingOnly(), cancellationToken);
        return Ok(ApiResponse<PurchaseRequestDetailDto>.Ok(detail, "Lấy chi tiết phiếu yêu cầu mua hàng thành công (UC-46)."));
    }

    /// <summary>
    /// UC-47: Chủ xưởng phê duyệt PR đang PENDING_APPROVAL (ghi chú tùy chọn). HTTP 409 nếu PR đã được xử lý ở phiên khác (UC047.E1).
    /// </summary>
    [HttpPatch("{id:long}/approve")]
    [Authorize(Roles = RoleConstants.Owner)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseRequestDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApprovePurchaseRequestAsync(
        [FromRoute] long id,
        [FromBody] ApprovePurchaseRequestDto? request,
        CancellationToken cancellationToken)
    {
        request ??= new ApprovePurchaseRequestDto();
        await _approveValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await _purchaseRequestService.ApprovePurchaseRequestAsync(
            id, request, GetCurrentUserId(), GetClientIpAddress(), cancellationToken);

        return Ok(ApiResponse<PurchaseRequestDetailDto>.Ok(result, $"Phiếu PR {result.PrCode} đã được phê duyệt và chuyển sang bộ phận Thu mua (UC-47)."));
    }

    /// <summary>
    /// UC-48: Chủ xưởng từ chối PR đang PENDING_APPROVAL, bắt buộc nhập lý do (UC048.E1). HTTP 409 nếu PR đã được xử lý.
    /// </summary>
    [HttpPatch("{id:long}/reject")]
    [Authorize(Roles = RoleConstants.Owner)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseRequestDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectPurchaseRequestAsync(
        [FromRoute] long id,
        [FromBody] RejectPurchaseRequestDto request,
        CancellationToken cancellationToken)
    {
        await _rejectValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await _purchaseRequestService.RejectPurchaseRequestAsync(
            id, request, GetCurrentUserId(), GetClientIpAddress(), cancellationToken);

        return Ok(ApiResponse<PurchaseRequestDetailDto>.Ok(result, $"Phiếu PR {result.PrCode} đã bị từ chối và chuyển trả về Kế hoạch vật tư (UC-48)."));
    }

    /// <summary>
    /// Người gọi chỉ có vai trò PURCHASING (không kiêm ADMIN/OWNER/PLANNER) thì chỉ được xem PR đã duyệt (BR-PUR-02).
    /// </summary>
    private bool IsPurchasingOnly()
    {
        return User.IsInRole(RoleConstants.Purchasing) &&
               !User.IsInRole(RoleConstants.Admin) &&
               !User.IsInRole(RoleConstants.Owner) &&
               !User.IsInRole(RoleConstants.Planner);
    }
}
