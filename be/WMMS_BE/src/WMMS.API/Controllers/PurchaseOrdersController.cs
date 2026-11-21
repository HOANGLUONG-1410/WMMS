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
        return Ok(ApiResponse<List<ApprovedRequisitionDto>>.Ok(result, "Đã tải danh sách phiếu yêu cầu mua hàng chờ đặt hàng."));
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
        return Ok(ApiResponse<PagedResult<PurchaseOrderListItemDto>>.Ok(result, "Đã tải danh sách đơn đặt hàng nhà cung cấp."));
    }

    /// <summary>Thẻ KPI SCR-19: số PO theo trạng thái, quá hạn và sắp đến hạn giao.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var result = await _purchaseOrderService.GetSummaryAsync(cancellationToken);
        return Ok(ApiResponse<PurchaseOrderSummaryDto>.Ok(result, "Đã tải báo cáo tổng hợp tiến độ đơn đặt hàng."));
    }

    /// <summary>UC-51: Chi tiết PO: dòng vật tư, đơn giá, tiến độ nhận hàng, tình trạng ETA và các phiếu nhập kho.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPurchaseOrderByIdAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _purchaseOrderService.GetPurchaseOrderByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<PurchaseOrderDetailDto>.Ok(result, "Đã tải thông tin chi tiết đơn đặt hàng."));
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
        if (request.PrId <= 0 && (request.Lines == null || request.Lines.Count == 0) && !string.IsNullOrWhiteSpace(request.SupplierName))
        {
            var batchCreated = await _purchaseOrderService.BatchIssuePurchaseOrdersAsync(
                null, GetCurrentUserId(), GetClientIpAddress(), cancellationToken);
            return StatusCode(
                StatusCodes.Status201Created,
                ApiResponse<List<PurchaseOrderDetailDto>>.Ok(batchCreated, $"Đã phát hành thành công {batchCreated.Count} đơn đặt hàng nhà cung cấp."));
        }

        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var created = await _purchaseOrderService.CreatePurchaseOrdersAsync(
            request, GetCurrentUserId(), GetClientIpAddress(), cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<List<PurchaseOrderDetailDto>>.Ok(created, $"Đã phát hành thành công {created.Count} đơn đặt hàng nhà cung cấp."));
    }

    /// <summary>
    /// Phát hành đồng loạt các Đơn đặt hàng nhà cung cấp (PO) từ một hoặc nhiều Phiếu yêu cầu mua hàng (PR) đã được Chủ xưởng phê duyệt.
    /// </summary>
    [HttpPost("batch-issue")]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(typeof(ApiResponse<List<PurchaseOrderDetailDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BatchIssuePurchaseOrdersAsync(
        [FromBody] BatchIssuePurchaseOrdersRequestDto? request = null,
        CancellationToken cancellationToken = default)
    {
        var created = await _purchaseOrderService.BatchIssuePurchaseOrdersAsync(
            request, GetCurrentUserId(), GetClientIpAddress(), cancellationToken);

        return Ok(ApiResponse<List<PurchaseOrderDetailDto>>.Ok(
            created,
            $"Đã phát hành đồng loạt thành công {created.Count} đơn đặt hàng tới các nhà cung cấp."));
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

        return Ok(ApiResponse<PurchaseOrderDetailDto>.Ok(result, $"Đã cập nhật lịch giao hàng dự kiến cho đơn đặt hàng {result.PoCode}."));
    }

    /// <summary>SC-16: Cập nhật điều khoản thanh toán &amp; tiến độ giao nhận cho PO.</summary>
    [HttpPut("{id:long}/terms")]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTermsAsync(
        [FromRoute] long id,
        [FromBody] UpdatePoTermsDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Terms))
        {
            return BadRequest(ApiResponse<object>.Fail("Vui lòng nhập điều khoản thanh toán và giao hàng."));
        }

        var result = await _purchaseOrderService.UpdateTermsAsync(
            id, request, GetCurrentUserId(), GetClientIpAddress(), cancellationToken);

        return Ok(ApiResponse<PurchaseOrderDetailDto>.Ok(result, $"Đã cập nhật điều khoản hợp đồng và thanh toán cho đơn đặt hàng {result.PoCode}."));
    }

    /// <summary>SC-16: Cập nhật trạng thái vòng đời của PO (ISSUED, PARTIALLY_RECEIVED, FULFILLED, CANCELLED).</summary>
    [HttpPut("{id:long}/status")]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatusAsync(
        [FromRoute] long id,
        [FromBody] UpdatePoStatusDto request,
        CancellationToken cancellationToken)
    {
        var result = await _purchaseOrderService.UpdateStatusAsync(
            id, request, GetCurrentUserId(), GetClientIpAddress(), cancellationToken);

        return Ok(ApiResponse<PurchaseOrderDetailDto>.Ok(result, $"Đã cập nhật trạng thái đơn đặt hàng {result.PoCode} sang '{result.FulfillmentStatus}'."));
    }

    /// <summary>
    /// SC-16 / UC-52: Gửi email Đơn đặt hàng nguyên vật liệu theo biểu mẫu chuẩn cho Nhà cung cấp
    /// (hỗ trợ gửi ngay cho Nhà cung cấp ACTIVE mặc định hoặc chọn Nhà cung cấp thủ công).
    /// </summary>
    [HttpPost("{id:long}/send-email")]
    [Authorize(Roles = WriteRoles)]
    [ProducesResponseType(typeof(ApiResponse<SendPurchaseOrderEmailResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendPurchaseOrderEmailAsync(
        [FromRoute] long id,
        [FromBody] SendPurchaseOrderEmailDto? request,
        CancellationToken cancellationToken)
    {
        var result = await _purchaseOrderService.SendPurchaseOrderEmailAsync(
            id, request, GetCurrentUserId(), GetClientIpAddress(), cancellationToken);

        return Ok(ApiResponse<SendPurchaseOrderEmailResultDto>.Ok(
            result,
            $"Đã gửi đơn đặt hàng {result.PoCode} tới nhà cung cấp {result.SupplierName} ({result.RecipientEmail}) thành công."));
    }
}
