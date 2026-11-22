using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.InventoryPr;
using WMMS.Application.DTOs.Warehouse;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;

namespace WMMS.API.Controllers;

/// <summary>
/// [Dev-Iter 2] Controller Nhập kho theo đơn PO (GRN) &amp; Xuất kho cấp phát vật tư bàn giao xuống xưởng (SC-17, UC-56, UC-57, BR-005, BR-WHS-01, BR-008).
/// <para>
/// - Chức năng:
///   + <c>GET /api/v1/warehouse/purchase-orders/open</c>: Lấy danh sách đơn PO đang chờ nhận hàng (<c>ISSUED</c> / <c>PARTIALLY_RECEIVED</c>).
///   + <c>POST /api/v1/warehouse/goods-receipts</c>: Thủ kho xác nhận Commit phiếu nhập kho (GRN), đối chiếu lượng thực nhận với PO, cộng tồn kho &amp; sổ kho, đồng thời kích hoạt Auto-Allocation theo độ ưu tiên <c>ETD ASC</c>.
///   + <c>POST /api/v1/warehouse/orders/{orderId}/issue</c>: Thủ kho xác nhận xuất kho cấp phát vật tư bàn giao xuống xưởng, chuyển phân bổ sang <c>ISSUED_TO_SHOP</c>, trừ tồn vật lý và mở khóa chốt chặn Hold Point.
/// - Phân quyền RBAC: Chỉ cho phép <c>ADMIN</c> và <c>WAREHOUSE</c>.
/// </para>
/// </summary>
[Route("api/v1/warehouse")]
[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Warehouse}")]
public class WarehouseController : MasterDataControllerBase
{
    private readonly IGoodsReceiptService _goodsReceiptService;
    private readonly IMrpAllocationService _mrpAllocationService;

    public WarehouseController(
        IGoodsReceiptService goodsReceiptService,
        IMrpAllocationService mrpAllocationService)
    {
        _goodsReceiptService = goodsReceiptService;
        _mrpAllocationService = mrpAllocationService;
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

    /// <summary>
    /// Xác nhận xuất kho cấp phát vật tư bàn giao xuống xưởng (Goods Issue to Shopfloor).
    /// Chuyển trạng thái phân bổ từ RESERVED sang ISSUED_TO_SHOP, trừ tồn vật lý (PhysicalQty),
    /// ghi nhận sổ kho OUT_ISSUE và mở khóa chốt chặn Hold Point cho các thẻ lô sản xuất (BR-008).
    /// </summary>
    [HttpPost("orders/{orderId:long}/issue")]
    [ProducesResponseType(typeof(ApiResponse<IssueOrderAllocationsResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> IssueOrderAllocationsAsync(
        [FromRoute] long orderId,
        [FromBody] IssueOrderAllocationsRequestDto? request,
        CancellationToken cancellationToken)
    {
        var result = await _mrpAllocationService.IssueOrderAllocationsAsync(
            orderId,
            request?.Reason,
            GetCurrentUserId(),
            cancellationToken);

        return Ok(ApiResponse<IssueOrderAllocationsResultDto>.Ok(result, result.Message));
    }
}
