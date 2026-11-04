using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.Orders;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;
using WMMS.Domain.Exceptions;

namespace WMMS.API.Controllers;

/// <summary>
/// Controller Tiếp nhận Đơn hàng, Kiểm tra lưới Staging &amp; Khởi tạo Không gian Đơn hàng (Phân hệ ORD - BF-01, UC-30 đến UC-35, Report 4 Class Diagram <c>OrdersController</c>).
/// Phục vụ 3 màn hình trong Section III.4 của Report 3:
/// 1. <c>Sales Order List (SCR-ORD-01)</c>
/// 2. <c>Safe Excel PO Staging Grid (SCR-ORD-02)</c>
/// 3. <c>Order Space Detail Dashboard (SCR-ORD-03)</c>
/// </summary>
[ApiController]
[Route("api/v1/orders")]
[Authorize]
[Produces("application/json")]
public class OrdersController : ControllerBase
{
    private readonly IOrderIntakeService _orderIntakeService;
    private readonly IValidator<ValidateStagingGridRequestDto> _validateStagingValidator;
    private readonly IValidator<ConfirmSalesOrderRequestDto> _confirmOrderValidator;

    public OrdersController(
        IOrderIntakeService orderIntakeService,
        IValidator<ValidateStagingGridRequestDto> validateStagingValidator,
        IValidator<ConfirmSalesOrderRequestDto> confirmOrderValidator)
    {
        _orderIntakeService = orderIntakeService;
        _validateStagingValidator = validateStagingValidator;
        _confirmOrderValidator = confirmOrderValidator;
    }

    /// <summary>
    /// UC-34: Lấy danh sách đơn hàng bán chính thức kèm thống kê KPI (Active Orders, Urgent ETD &lt; 10 Days, Total Units on Floor) trên màn hình SCR-ORD-01.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<SalesOrderListSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalesOrdersAsync([FromQuery] SalesOrderFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _orderIntakeService.GetSalesOrdersAsync(filter, cancellationToken);
        return Ok(ApiResponse<SalesOrderListSummaryDto>.Ok(result, "Fetched official sales orders directory and space metrics (UC-34)."));
    }

    /// <summary>
    /// Lấy danh sách khách hàng B2B để chọn khi import Excel PO.
    /// </summary>
    [HttpGet("customers")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CustomerDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomersAsync(CancellationToken cancellationToken)
    {
        var customers = await _orderIntakeService.GetCustomersAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CustomerDto>>.Ok(customers, "Fetched B2B customer directory."));
    }

    /// <summary>
    /// UC-30 &amp; UC-31: Upload file Excel PO của khách hàng (.xlsx/.xls) hoặc nạp dữ liệu mẫu lên bảng Safe Excel PO Staging Grid (SCR-ORD-02).
    /// Tự động loại bỏ thông tin vận tải/giá nhạy cảm (BR-ORD-01) và đánh dấu đỏ các ô lỗi định dạng (BR-ORD-02).
    /// </summary>
    [HttpPost("staging/upload")]
    [Authorize(Roles = $"{RoleConstants.Sales},{RoleConstants.Owner},{RoleConstants.Admin}")]
    [ProducesResponseType(typeof(ApiResponse<StagingOrderGridResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadSalesOrderExcelAsync(
        IFormFile? file,
        [FromForm] string? poNumber,
        [FromForm] long? customerId,
        CancellationToken cancellationToken)
    {
        await using var stream = file?.OpenReadStream();
        var fileName = file?.FileName ?? "PO_Target_Home_US_880515.xlsx";

        var stagingGrid = await _orderIntakeService.ParseExcelPoToStagingGridAsync(
            stream,
            fileName,
            poNumber,
            customerId,
            cancellationToken);

        return Ok(ApiResponse<StagingOrderGridResponseDto>.Ok(
            stagingGrid,
            stagingGrid.SummaryMessage,
            stagingGrid.CanConfirm ? MessageCodes.Msg07Success : MessageCodes.Msg13ValidationError));
    }

    /// <summary>
    /// UC-32: Kiểm tra lại toàn bộ các ô trên bảng Staging Grid sau khi Sales chỉnh sửa trực tiếp (Nút "Validate Grid Again" trên SCR-ORD-02, BR-ORD-02).
    /// </summary>
    [HttpPost("staging/validate")]
    [Authorize(Roles = $"{RoleConstants.Sales},{RoleConstants.Owner},{RoleConstants.Admin}")]
    [ProducesResponseType(typeof(ApiResponse<StagingOrderGridResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateStagingGridAsync(
        [FromBody] ValidateStagingGridRequestDto request,
        CancellationToken cancellationToken)
    {
        await _validateStagingValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await _orderIntakeService.ValidateStagingGridAsync(request, cancellationToken);
        return Ok(ApiResponse<StagingOrderGridResponseDto>.Ok(
            result,
            result.SummaryMessage,
            result.CanConfirm ? MessageCodes.Msg07Success : MessageCodes.Msg13ValidationError));
    }

    /// <summary>
    /// UC-33: Xác nhận đơn hàng đã sạch lỗi (0 red cells), lưu vào CSDL và tự động khởi tạo Order Space độc lập (Nút "Confirm &amp; Create Order Space" trên SCR-ORD-02, BR-ORD-03).
    /// </summary>
    [HttpPost("confirm")]
    [Authorize(Roles = $"{RoleConstants.Sales},{RoleConstants.Owner},{RoleConstants.Admin}")]
    [ProducesResponseType(typeof(ApiResponse<OrderSpaceDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmSalesOrderAsync(
        [FromBody] ConfirmSalesOrderRequestDto request,
        CancellationToken cancellationToken)
    {
        await _confirmOrderValidator.ValidateAndThrowAsync(request, cancellationToken);

        var currentUserId = GetRequiredCurrentUserId();
        var createdSpace = await _orderIntakeService.ConfirmSalesOrderAndInitSpaceAsync(
            request,
            currentUserId,
            GetClientIpAddress(),
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<OrderSpaceDetailDto>.Ok(
                createdSpace,
                $"Sales Order confirmed successfully! Order Space #{createdSpace.SpaceKey} initialized and notification dispatched to Material Planning (UC-33, MSG-ORD-04)."));
    }

    /// <summary>
    /// UC-35: Xem chi tiết Order Space Detail Dashboard (SCR-ORD-03) theo <c>spaceKey</c> (ví dụ <c>SPACE-PO-880504</c>) hoặc <c>orderId</c>.
    /// Tự động bật cờ <c>IsViewOnlyForCurrentUser = true</c> đối với vai trò Material Planning Staff theo quy tắc BR-ORD-04.
    /// </summary>
    [HttpGet("spaces/{idOrKey}")]
    [ProducesResponseType(typeof(ApiResponse<OrderSpaceDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderSpaceDetailAsync([FromRoute] string idOrKey, CancellationToken cancellationToken)
    {
        var isViewOnly = !User.IsInRole(RoleConstants.Sales) &&
                         !User.IsInRole(RoleConstants.Owner) &&
                         !User.IsInRole(RoleConstants.Admin);

        var detail = await _orderIntakeService.GetOrderSpaceDetailAsync(idOrKey, isViewOnly, cancellationToken);
        return Ok(ApiResponse<OrderSpaceDetailDto>.Ok(detail, "Fetched Order Space detail dashboard (UC-35)."));
    }

    /// <summary>
    /// UC-35 / Use Case Diagram 4.3.5: Đính kèm hoặc cập nhật đường dẫn file bản vẽ kỹ thuật CAD/PDF cho Order Space.
    /// </summary>
    [HttpPatch("spaces/{idOrKey}/drawings")]
    [Authorize(Roles = $"{RoleConstants.Sales},{RoleConstants.Owner},{RoleConstants.Admin}")]
    [ProducesResponseType(typeof(ApiResponse<OrderSpaceDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AttachTechnicalDrawingAsync(
        [FromRoute] string idOrKey,
        [FromBody] UpdateOrderSpaceDrawingDto dto,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetRequiredCurrentUserId();
        var updated = await _orderIntakeService.AttachTechnicalDrawingAsync(
            idOrKey,
            dto,
            currentUserId,
            GetClientIpAddress(),
            cancellationToken);

        return Ok(ApiResponse<OrderSpaceDetailDto>.Ok(updated, "Attached technical CAD drawing to Order Space successfully."));
    }

    private long GetRequiredCurrentUserId()
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
