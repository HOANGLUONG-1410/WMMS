using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.Warehouse;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;

namespace WMMS.API.Controllers;

/// <summary>
/// [Dev-Iter 2] Controller Nhập kho theo đơn PO (GRN) &amp; Phân bổ tự động vật tư vào Staging kho (SC-17, UC-56, UC-57, BR-005, BR-WHS-01).
/// <para>
/// - Chức năng:
///   + <c>GET /api/v1/warehouse/purchase-orders/open</c>: Lấy danh sách đơn PO đang chờ nhận hàng (<c>ISSUED</c> / <c>PARTIALLY_RECEIVED</c>).
///   + <c>POST /api/v1/warehouse/goods-receipts</c>: Thủ kho xác nhận Commit phiếu nhập kho (GRN), đối chiếu lượng thực nhận với PO, cộng tồn kho &amp; sổ kho, đồng thời kích hoạt Auto-Allocation theo độ ưu tiên <c>ETD ASC</c>.
/// - Phân quyền RBAC: Chỉ cho phép <c>ADMIN</c> và <c>WAREHOUSE</c>.
/// - Móc nối: Gọi <see cref="IGoodsReceiptService"/> (<c>GoodsReceiptService</c>) -&gt; Cập nhật <c>purchase_orders</c>, <c>goods_receipt_notes</c>, <c>inventory_stocks</c>, <c>inventory_transactions</c>, <c>material_allocations</c>, <c>boms</c>, <c>audit_logs</c> và đẩy sự kiện qua <c>WmmsNotificationHub</c> (SignalR).
/// </para>
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

        var message = $"Đã xác nhận phiếu nhập kho thành công! Tồn kho đã được cập nhật và tự động phân bổ vật tư cho {result.AllocatedOrderCount} đơn hàng đang chờ.";
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
        return Ok(ApiResponse<IReadOnlyList<OpenPurchaseOrderDto>>.Ok(result, "Đã tải danh sách đơn đặt hàng đang chờ nhập kho."));
    }
}
