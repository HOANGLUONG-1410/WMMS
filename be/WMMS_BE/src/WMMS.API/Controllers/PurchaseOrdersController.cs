using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.Purchasing;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;

namespace WMMS.API.Controllers;

/// <summary>
/// Controller lập Đơn đặt hàng Nhà cung cấp (PO) gom theo Supplier &amp; Theo dõi tiến độ ETA (SC-16, SCR-19).
/// Use case: UC-49 (PR đã duyệt chờ đặt), UC-50 (Danh sách PO), UC-51 (Chi tiết PO), UC-52 (Tạo PO), UC-53 (Cập nhật lịch giao).
/// Quyền: xem cho ADMIN/OWNER/PURCHASING/PLANNER (view-only)/WAREHOUSE; tạo PO và đổi ETA chỉ ADMIN/PURCHASING
/// (KHVT không có quyền CUD trên PO).
/// </summary>
[Route("api/v1/purchase-orders")]
[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Purchasing},{RoleConstants.Planner},{RoleConstants.Warehouse}")]
public class PurchaseOrdersController : MasterDataControllerBase
{
    private const string WriteRoles = $"{RoleConstants.Admin},{RoleConstants.Purchasing}";

    private readonly IPurchaseOrderService _purchaseOrderService;
    private readonly IValidator<PurchaseOrderFilterDto> _filterValidator;
    private readonly IValidator<CreatePurchaseOrdersRequestDto> _createValidator;
    private readonly IValidator<UpdatePoEtaDto> _etaValidator;

    public PurchaseOrdersController(
        IPurchaseOrderService purchaseOrderService,
        IValidator<PurchaseOrderFilterDto> filterValidator,
        IValidator<CreatePurchaseOrdersRequestDto> createValidator,
        IValidator<UpdatePoEtaDto> etaValidator)
    {
        _purchaseOrderService = purchaseOrderService;
        _filterValidator = filterValidator;
        _createValidator = createValidator;
        _etaValidator = etaValidator;
    }

    /// <summary>UC-49: Hàng đợi các PR đã được Chủ xưởng duyệt và còn dòng chưa đặt hàng.</summary>
    [HttpGet("approved-requisitions")]
    [ProducesResponseType(typeof(ApiResponse<List<ApprovedRequisitionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetApprovedRequisitionsAsync(CancellationToken cancellationToken)
    {
        var result = await _purchaseOrderService.GetApprovedRequisitionsAsync(cancellationToken);
        return Ok(ApiResponse<List<ApprovedRequisitionDto>>.Ok(result, "Fetched approved requisitions awaiting PO generation (UC-49)."));
    }

    /// <summary>
    /// UC-50: Danh sách PO có phân trang; lọc theo NCC, trạng thái, tình trạng ETA (OVERDUE/DUE_SOON), khoảng ngày giao và từ khóa.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PurchaseOrderListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPurchaseOrdersAsync([FromQuery] PurchaseOrderFilterDto filter, CancellationToken cancellationToken)
    {
        await _filterValidator.ValidateAndThrowAsync(filter, cancellationToken);
        var result = await _purchaseOrderService.GetPurchaseOrdersAsync(filter, cancellationToken);
        return Ok(ApiResponse<PagedResult<PurchaseOrderListItemDto>>.Ok(result, "Fetched supplier purchase orders (UC-50)."));
    }

    /// <summary>Thẻ KPI SCR-19: số PO theo trạng thái, quá hạn và sắp đến hạn giao.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var result = await _purchaseOrderService.GetSummaryAsync(cancellationToken);
        return Ok(ApiResponse<PurchaseOrderSummaryDto>.Ok(result, "Fetched purchase order ETA summary (SC-16)."));
    }

    /// <summary>UC-51: Chi tiết PO: dòng vật tư, đơn giá, tiến độ nhận hàng, tình trạng ETA và các phiếu nhập kho.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPurchaseOrderByIdAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _purchaseOrderService.GetPurchaseOrderByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<PurchaseOrderDetailDto>.Ok(result, "Fetched purchase order details (UC-51)."));
    }

    /// <summary>
    /// UC-52: Tạo PO từ PR đã duyệt. Các dòng được gom theo Supplier, mỗi Supplier một PO (ISSUED).
    /// 403 nếu PR chưa duyệt (BR-PUR-02); 400 nếu vượt lượng còn lại, NCC không hoạt động hoặc ETA ở quá khứ.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(typeof(ApiResponse<List<PurchaseOrderDetailDto>>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePurchaseOrdersAsync(
        [FromBody] CreatePurchaseOrdersRequestDto request,
        CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var created = await _purchaseOrderService.CreatePurchaseOrdersAsync(
            request, GetCurrentUserId(), GetClientIpAddress(), cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<List<PurchaseOrderDetailDto>>.Ok(created, $"Issued {created.Count} supplier purchase order(s) (UC-52)."));
    }

    /// <summary>UC-53: Cập nhật ngày hẹn giao và ghi chú vận chuyển; 409 nếu PO đã nhận đủ hoặc đã hủy.</summary>
    [HttpPut("{id:long}/eta")]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateEtaAsync(
        [FromRoute] long id,
        [FromBody] UpdatePoEtaDto request,
        CancellationToken cancellationToken)
    {
        await _etaValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await _purchaseOrderService.UpdateEtaAsync(
            id, request, GetCurrentUserId(), GetClientIpAddress(), cancellationToken);

        return Ok(ApiResponse<PurchaseOrderDetailDto>.Ok(result, $"Delivery schedule of PO {result.PoCode} updated (UC-53)."));
    }
}
