using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.Warehouse;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;

namespace WMMS.API.Controllers;

/// <summary>
/// Controller Nhập kho theo đơn PO (GRN) & Phân bổ tự động vật tư vào Staging (SC-17, UC-56, UC-57).
/// Chỉ Thủ kho (WAREHOUSE) và Admin được thao tác.
/// </summary>
[Route("api/v1/warehouse")]
[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Warehouse}")]
public class WarehouseController : MasterDataControllerBase
{
    private readonly IGoodsReceiptService _goodsReceiptService;

    public WarehouseController(IGoodsReceiptService goodsReceiptService)
    {
        _goodsReceiptService = goodsReceiptService;
    }

    /// <summary>
    /// SC-17: Commit phiếu nhập kho theo PO — đối chiếu số lượng thực nhận, cập nhật tồn vật lý + sổ kho,
    /// kích hoạt Auto-Allocation theo ETD tăng dần (BR-WHS-01).
    /// </summary>
    [HttpPost("goods-receipts")]
    [ProducesResponseType(typeof(ApiResponse<GoodsReceiptResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CommitGoodsReceiptAsync(
        [FromBody] CommitGoodsReceiptRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _goodsReceiptService.CommitGoodsReceiptAsync(
            request, GetCurrentUserId(), GetClientIpAddress(), cancellationToken);

        var message = $"Goods Receipt committed successfully! Stock updated and materials auto-allocated to {result.AllocatedOrderCount} pending orders. (MSG-WHS-01)";
        return Ok(ApiResponse<GoodsReceiptResultDto>.Ok(result, message));
    }

    /// <summary>SC-17: Tra cứu các PO đang mở (ISSUED / PARTIALLY_RECEIVED) để nhập kho.</summary>
    [HttpGet("purchase-orders/open")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<OpenPurchaseOrderDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOpenPurchaseOrdersAsync(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await _goodsReceiptService.GetOpenPurchaseOrdersAsync(search, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<OpenPurchaseOrderDto>>.Ok(result, "Fetched open purchase orders awaiting receipt (SC-17)."));
    }
}
