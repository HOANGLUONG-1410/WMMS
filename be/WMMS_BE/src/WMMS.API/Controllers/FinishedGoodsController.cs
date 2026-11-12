using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.ProductionTracking;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;

namespace WMMS.API.Controllers;

/// <summary>
/// API Controller quản lý Hoàn tất Lô sản xuất, Nhập kho Thành phẩm &amp; Theo dõi Tiến độ Đơn hàng tự động (Iteration 3 - UC-63, UC-64).
/// </summary>
[ApiController]
[Route("api/v1/finished-goods")]
[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Leader},{RoleConstants.Warehouse},{RoleConstants.Planner},{RoleConstants.Qc},{RoleConstants.Sales}")]
public class FinishedGoodsController : ControllerBase
{
    private readonly IFinishedGoodsService _finishedGoodsService;
    private readonly IOrderProgressService _orderProgressService;
    private readonly IValidator<CompleteFinishedBatchRequestDto> _completeBatchValidator;

    public FinishedGoodsController(
        IFinishedGoodsService finishedGoodsService,
        IOrderProgressService orderProgressService,
        IValidator<CompleteFinishedBatchRequestDto> completeBatchValidator)
    {
        _finishedGoodsService = finishedGoodsService;
        _orderProgressService = orderProgressService;
        _completeBatchValidator = completeBatchValidator;
    }

    /// <summary>
    /// Lấy danh sách các lô sản xuất đã hoàn tất toàn bộ công đoạn (sẵn sàng nhập kho thành phẩm) hoặc đã nhập kho thành phẩm (`DONE`).
    /// </summary>
    [HttpGet("batches")]
    public async Task<ActionResult<ApiResponse<List<FinishedGoodsBatchSummaryDto>>>> GetFinishedGoodsBatches(
        [FromQuery] bool onlyReadyForReceipt = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _finishedGoodsService.GetFinishedGoodsBatchesAsync(onlyReadyForReceipt, cancellationToken);
        return Ok(ApiResponse<List<FinishedGoodsBatchSummaryDto>>.Ok(
            result,
            "Truy vấn danh sách lô thành phẩm thành công."));
    }

    /// <summary>
    /// UC-63 &amp; UC-64: Xác nhận hoàn tất công đoạn đóng gói cuối cùng, nhập kho thành phẩm (`DONE`)
    /// và tự động tính toán, đồng bộ trạng thái hoàn thành của `OrderItem` và `CustomerOrder`.
    /// </summary>
    [HttpPost("batches/{batchId:long}/complete")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Leader},{RoleConstants.Warehouse}")]
    public async Task<ActionResult<ApiResponse<FinishedGoodsReceiptResultDto>>> CompleteBatch(
        long batchId,
        [FromBody] CompleteFinishedBatchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validation = await _completeBatchValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<FinishedGoodsReceiptResultDto>.Fail(
                "Dữ liệu xác nhận nhập kho thành phẩm không hợp lệ.",
                validation.Errors.Select(e => e.ErrorMessage).ToList(),
                MessageCodes.Msg13ValidationError));
        }

        long currentUserId = GetCurrentUserId();
        var result = await _finishedGoodsService.CompleteBatchAndReceiveFinishedGoodsAsync(
            batchId,
            request,
            currentUserId,
            cancellationToken);

        return Ok(ApiResponse<FinishedGoodsReceiptResultDto>.Ok(result, result.Message));
    }

    /// <summary>
    /// Truy vấn danh sách tiến độ sản xuất của toàn bộ các đơn hàng trong xưởng, sắp xếp ưu tiên theo hạn giao hàng ETD.
    /// </summary>
    [HttpGet("orders/progress")]
    public async Task<ActionResult<ApiResponse<List<OrderProductionProgressDto>>>> GetAllOrdersProgress(
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _orderProgressService.GetAllOrdersProgressAsync(status, cancellationToken);
        return Ok(ApiResponse<List<OrderProductionProgressDto>>.Ok(
            result,
            "Truy vấn danh sách tiến độ sản xuất đơn hàng thành công."));
    }

    /// <summary>
    /// Truy vấn chi tiết tiến độ sản xuất của một đơn hàng cụ thể theo `orderId`.
    /// </summary>
    [HttpGet("orders/{orderId:long}/progress")]
    public async Task<ActionResult<ApiResponse<OrderProductionProgressDto>>> GetOrderProgressById(
        long orderId,
        CancellationToken cancellationToken = default)
    {
        var result = await _orderProgressService.GetOrderProgressByIdAsync(orderId, cancellationToken);
        return Ok(ApiResponse<OrderProductionProgressDto>.Ok(
            result,
            $"Truy vấn tiến độ sản xuất của đơn hàng '{result.PoNumber}' thành công."));
    }

    /// <summary>
    /// Đồng bộ hóa tự động trạng thái của dòng sản phẩm (`OrderItem`) và đơn hàng (`CustomerOrder`) dựa trên sản lượng thành phẩm thực tế của các lô `DONE`.
    /// </summary>
    [HttpPost("orders/{orderId:long}/synchronize-progress")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Leader},{RoleConstants.Planner},{RoleConstants.Warehouse}")]
    public async Task<ActionResult<ApiResponse<OrderProductionProgressDto>>> SynchronizeOrderProgress(
        long orderId,
        CancellationToken cancellationToken = default)
    {
        long currentUserId = GetCurrentUserId();
        var result = await _orderProgressService.SynchronizeOrderProgressAsync(orderId, currentUserId, cancellationToken);
        return Ok(ApiResponse<OrderProductionProgressDto>.Ok(
            result,
            $"Đã đồng bộ tiến độ sản xuất cho đơn hàng '{result.PoNumber}' (Hoàn thành {result.OverallCompletionPercent}%)."));
    }

    private long GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value;
        return long.TryParse(claim, out var id) ? id : 1L;
    }
}
