using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.InventoryPr;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;

namespace WMMS.API.Controllers;

/// <summary>
/// Controller Quản lý Tồn kho 3 cấp &amp; Sổ kho điện tử (SC-12, SC-13).
/// Cho phép truy cập bởi: ADMIN, OWNER, PLANNER, PURCHASING, WAREHOUSE.
/// </summary>
[Route("api/v1/inventory")]
[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Purchasing},{RoleConstants.Warehouse}")]
public class InventoryController : MasterDataControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    /// <summary>
    /// SC-12: Trả về danh sách tồn kho 3 cấp (Physical, Allocated, Available) của tất cả vật tư
    /// kèm bộ lọc theo nhóm vật tư (Gỗ/LUMBER, Sơn/FINISH_PAINT, Phụ kiện/HARDWARE, Đóng gói/PACKAGING).
    /// </summary>
    [HttpGet("stocks")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<InventoryStockDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStocksAsync(
        [FromQuery] InventoryStockFilterDto filter,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetStocksAsync(filter, cancellationToken);
        return Ok(ApiResponse<PagedResult<InventoryStockDto>>.Ok(result, "Fetched 3-tier inventory stocks (SC-12)."));
    }

    /// <summary>
    /// SC-12: Xem chi tiết tồn kho 3 cấp và danh sách các đơn hàng đang giữ chỗ (Allocated/Reserved) vật tư đó.
    /// </summary>
    [HttpGet("stocks/{materialId:long}")]
    [ProducesResponseType(typeof(ApiResponse<InventoryStockDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStockByMaterialIdAsync(
        [FromRoute] long materialId,
        CancellationToken cancellationToken)
    {
        var detail = await _inventoryService.GetStockByMaterialIdAsync(materialId, cancellationToken);
        return Ok(ApiResponse<InventoryStockDetailDto>.Ok(detail, "Fetched material stock detail and reserved orders (SC-12)."));
    }

    /// <summary>
    /// SC-13: Truy vấn lịch sử giao dịch sổ kho từ bảng <c>inventory_transactions</c> (phân trang, lọc theo ngày, loại giao dịch, mã vật tư).
    /// </summary>
    [HttpGet("ledger")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<InventoryTransactionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLedgerAsync(
        [FromQuery] InventoryLedgerFilterDto filter,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetLedgerAsync(filter, cancellationToken);
        return Ok(ApiResponse<PagedResult<InventoryTransactionDto>>.Ok(result, "Fetched inventory transaction ledger (SC-13)."));
    }
}
