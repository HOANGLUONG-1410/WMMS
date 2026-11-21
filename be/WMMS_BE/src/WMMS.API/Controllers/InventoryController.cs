using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.InventoryPr;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;

namespace WMMS.API.Controllers;

/// <summary>
/// Controller Quản lý Tồn kho 3 cấp, Sổ kho điện tử &amp; Phân bổ Tồn kho Tự động MRP (SC-12, SC-13, SC-14).
/// Cho phép truy cập bởi: ADMIN, OWNER, PLANNER, PURCHASING, WAREHOUSE.
/// </summary>
[Route("api/v1/inventory")]
[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Purchasing},{RoleConstants.Warehouse}")]
public class InventoryController : MasterDataControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly IMrpAllocationService _mrpAllocationService;

    public InventoryController(
        IInventoryService inventoryService,
        IMrpAllocationService mrpAllocationService)
    {
        _inventoryService = inventoryService;
        _mrpAllocationService = mrpAllocationService;
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
        return Ok(ApiResponse<PagedResult<InventoryStockDto>>.Ok(result, "Lấy danh sách tồn kho 3 cấp thành công (SC-12)."));
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
        return Ok(ApiResponse<InventoryStockDetailDto>.Ok(detail, "Lấy chi tiết tồn kho vật tư và danh sách đơn giữ chỗ thành công (SC-12)."));
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
        return Ok(ApiResponse<PagedResult<InventoryTransactionDto>>.Ok(result, "Truy vấn sổ kho điện tử thành công (SC-13)."));
    }

    /// <summary>
    /// SC-14: Phân tích MRP chi tiết cho một Đơn hàng (<c>sales_orders</c>).
    /// </summary>
    [HttpGet("mrp/orders/{orderId:long}")]
    [ProducesResponseType(typeof(ApiResponse<MrpAnalysisResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AnalyzeOrderMrpAsync(
        [FromRoute] long orderId,
        CancellationToken cancellationToken)
    {
        var result = await _mrpAllocationService.AnalyzeOrderMrpAsync(orderId, cancellationToken);
        return Ok(ApiResponse<MrpAnalysisResultDto>.Ok(result, "Phân tích nhu cầu vật tư MRP cho đơn hàng thành công (SC-14)."));
    }

    /// <summary>
    /// SC-14: Phân tích MRP chi tiết cho một Không gian Đơn hàng (<c>order_spaces</c>).
    /// </summary>
    [HttpGet("mrp/spaces/{spaceId:long}")]
    [ProducesResponseType(typeof(ApiResponse<MrpAnalysisResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AnalyzeOrderSpaceMrpAsync(
        [FromRoute] long spaceId,
        CancellationToken cancellationToken)
    {
        var result = await _mrpAllocationService.AnalyzeOrderSpaceMrpAsync(spaceId, cancellationToken);
        return Ok(ApiResponse<MrpAnalysisResultDto>.Ok(result, "Phân tích nhu cầu vật tư MRP cho không gian đơn hàng thành công (SC-14)."));
    }

    /// <summary>
    /// SC-14: Thực thi thuật toán Phân bổ tồn kho tự động (Auto-Allocation) cho Đơn hàng.
    /// </summary>
    [HttpPost("mrp/orders/{orderId:long}/allocate")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Warehouse}")]
    [ProducesResponseType(typeof(ApiResponse<AutoAllocationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AllocateInventoryForOrderAsync(
        [FromRoute] long orderId,
        [FromBody] AutoAllocationRequestDto? request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _mrpAllocationService.ExecuteAutoAllocationForOrderAsync(orderId, request, userId, cancellationToken);
        return Ok(ApiResponse<AutoAllocationResultDto>.Ok(result, "Thực thi phân bổ tồn kho MRP tự động cho đơn hàng thành công (SC-14)."));
    }

    /// <summary>
    /// SC-14: Thực thi thuật toán Phân bổ tồn kho tự động (Auto-Allocation) cho Không gian Đơn hàng (<c>OrderSpace</c>).
    /// </summary>
    [HttpPost("mrp/spaces/{spaceId:long}/allocate")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Warehouse}")]
    [ProducesResponseType(typeof(ApiResponse<AutoAllocationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AllocateInventoryForSpaceAsync(
        [FromRoute] long spaceId,
        [FromBody] AutoAllocationRequestDto? request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _mrpAllocationService.ExecuteAutoAllocationForOrderSpaceAsync(spaceId, request, userId, cancellationToken);
        return Ok(ApiResponse<AutoAllocationResultDto>.Ok(result, "Thực thi phân bổ tồn kho MRP tự động cho không gian đơn hàng thành công (SC-14)."));
    }

    /// <summary>
    /// SC-14 &amp; SC-17: Tự động phân bổ lại tồn kho khả dụng theo thứ tự ưu tiên ETD sớm nhất (FIFO by ETD).
    /// </summary>
    [HttpPost("mrp/reallocate-by-etd")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Warehouse}")]
    [ProducesResponseType(typeof(ApiResponse<ReallocateByEtdResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReallocateByEtdAsync(
        [FromBody] ReallocateByEtdRequestDto? request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _mrpAllocationService.ReallocateAvailableStockByEtdAsync(
            request ?? new ReallocateByEtdRequestDto(),
            userId,
            cancellationToken);
        return Ok(ApiResponse<ReallocateByEtdResultDto>.Ok(result, "Thực thi tái phân bổ tồn kho ưu tiên theo ngày giao hàng ETD sớm nhất thành công (SC-14)."));
    }
}
