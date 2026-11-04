using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.InventoryPr;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;

namespace WMMS.API.Controllers;

/// <summary>
/// Controller Tính toán Nhu cầu Nguyên vật liệu (MRP) &amp; Phân bổ Tồn kho Tự động (SC-14).
/// Hỗ trợ phân tích nhu cầu gộp có hao hụt (BR-BOM-02), loại trừ tồn kho đã giữ chỗ của đơn khác (BR-INV-01),
/// tự động khóa phân bổ mềm (BR-INV-02), gom thiếu hụt vào PR tổng hợp trong ngày (BR-PUR-01),
/// và tự động phân bổ lại vật tư mới nhập kho theo độ ưu tiên hạn giao hàng ETD sớm nhất (FIFO by ETD).
/// </summary>
[Route("api/v1/mrp")]
[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Purchasing},{RoleConstants.Warehouse}")]
public class MrpController : MasterDataControllerBase
{
    private readonly IMrpAllocationService _mrpAllocationService;

    public MrpController(IMrpAllocationService mrpAllocationService)
    {
        _mrpAllocationService = mrpAllocationService;
    }

    /// <summary>
    /// SC-14: Phân tích nhu cầu nguyên vật liệu MRP tổng thể trên toàn bộ các đơn hàng đang hoạt động của xưởng.
    /// </summary>
    [HttpGet("factory-analysis")]
    [ProducesResponseType(typeof(ApiResponse<MrpAnalysisResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AnalyzeFactoryMrpAsync(CancellationToken cancellationToken)
    {
        var result = await _mrpAllocationService.AnalyzeFactoryMrpAsync(cancellationToken);
        return Ok(ApiResponse<MrpAnalysisResultDto>.Ok(result, "Analyzed factory-wide MRP material requirements and shortages (SC-14)."));
    }

    /// <summary>
    /// SC-14: Phân tích MRP chi tiết (Gross Demand, Available Stock loại trừ giữ chỗ đơn khác, Net Shortage) cho một Đơn hàng (<c>sales_orders</c>).
    /// </summary>
    [HttpGet("orders/{orderId:long}/analysis")]
    [ProducesResponseType(typeof(ApiResponse<MrpAnalysisResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AnalyzeOrderMrpAsync(
        [FromRoute] long orderId,
        CancellationToken cancellationToken)
    {
        var result = await _mrpAllocationService.AnalyzeOrderMrpAsync(orderId, cancellationToken);
        return Ok(ApiResponse<MrpAnalysisResultDto>.Ok(result, "Analyzed order MRP material requirements and net shortage (SC-14)."));
    }

    /// <summary>
    /// SC-14: Phân tích MRP chi tiết cho một Không gian Đơn hàng (<c>order_spaces</c>).
    /// </summary>
    [HttpGet("spaces/{spaceId:long}/analysis")]
    [ProducesResponseType(typeof(ApiResponse<MrpAnalysisResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AnalyzeOrderSpaceMrpAsync(
        [FromRoute] long spaceId,
        CancellationToken cancellationToken)
    {
        var result = await _mrpAllocationService.AnalyzeOrderSpaceMrpAsync(spaceId, cancellationToken);
        return Ok(ApiResponse<MrpAnalysisResultDto>.Ok(result, "Analyzed order space MRP material requirements and net shortage (SC-14)."));
    }

    /// <summary>
    /// SC-14: Thực thi thuật toán Phân bổ tồn kho tự động (Auto-Allocation) cho toàn bộ dòng sản phẩm của một Đơn hàng (<c>sales_orders</c>).
    /// Tự động khóa tồn kho khả dụng vào <c>material_allocations</c>, chuyển trạng thái sang READY_PROD / WAITING_MAT
    /// và tự động gom phần thiếu hụt vào PR tổng hợp trong ngày (BR-PUR-01).
    /// </summary>
    [HttpPost("orders/{orderId:long}/auto-allocate")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Warehouse}")]
    [ProducesResponseType(typeof(ApiResponse<AutoAllocationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExecuteAutoAllocationForOrderAsync(
        [FromRoute] long orderId,
        [FromBody] AutoAllocationRequestDto? request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _mrpAllocationService.ExecuteAutoAllocationForOrderAsync(orderId, request, userId, cancellationToken);
        return Ok(ApiResponse<AutoAllocationResultDto>.Ok(result, "Executed automated MRP inventory allocation for order (SC-14)."));
    }

    /// <summary>
    /// SC-14: Thực thi thuật toán Phân bổ tồn kho tự động (Auto-Allocation) cho một Không gian Đơn hàng (<c>order_spaces</c>).
    /// </summary>
    [HttpPost("spaces/{spaceId:long}/auto-allocate")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Warehouse}")]
    [ProducesResponseType(typeof(ApiResponse<AutoAllocationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExecuteAutoAllocationForOrderSpaceAsync(
        [FromRoute] long spaceId,
        [FromBody] AutoAllocationRequestDto? request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _mrpAllocationService.ExecuteAutoAllocationForOrderSpaceAsync(spaceId, request, userId, cancellationToken);
        return Ok(ApiResponse<AutoAllocationResultDto>.Ok(result, "Executed automated MRP inventory allocation for order space (SC-14)."));
    }

    /// <summary>
    /// SC-14: Thực thi thuật toán Phân bổ tồn kho tự động cho một dòng sản phẩm đơn hàng (<c>order_items</c>) cụ thể.
    /// </summary>
    [HttpPost("order-items/{orderItemId:long}/auto-allocate")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Warehouse}")]
    [ProducesResponseType(typeof(ApiResponse<AutoAllocationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExecuteAutoAllocationForOrderItemAsync(
        [FromRoute] long orderItemId,
        [FromBody] AutoAllocationRequestDto? request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _mrpAllocationService.ExecuteAutoAllocationForOrderItemAsync(orderItemId, request, userId, cancellationToken);
        return Ok(ApiResponse<AutoAllocationResultDto>.Ok(result, "Executed automated MRP inventory allocation for order item (SC-14)."));
    }

    /// <summary>
    /// SC-14 &amp; SC-17: Tự động phân bổ lại tồn kho khả dụng (Available Stock) cho các đơn hàng đang chờ vật tư
    /// theo thứ tự ưu tiên hạn giao hàng ETD sớm nhất (FIFO by Order ETD).
    /// </summary>
    [HttpPost("reallocate-by-etd")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Warehouse}")]
    [ProducesResponseType(typeof(ApiResponse<ReallocateByEtdResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReallocateAvailableStockByEtdAsync(
        [FromBody] ReallocateByEtdRequestDto? request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _mrpAllocationService.ReallocateAvailableStockByEtdAsync(
            request ?? new ReallocateByEtdRequestDto(),
            userId,
            cancellationToken);

        return Ok(ApiResponse<ReallocateByEtdResultDto>.Ok(result, "Executed FIFO ETD-prioritized inventory re-allocation (SC-14)."));
    }

    /// <summary>
    /// Giải phóng (Release) phần vật tư đang giữ chỗ (RESERVED) của một đơn hàng về lại tồn kho khả dụng (Available Stock).
    /// </summary>
    [HttpPost("orders/{orderId:long}/release-allocations")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Warehouse}")]
    [ProducesResponseType(typeof(ApiResponse<List<MaterialOrderAllocationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReleaseOrderAllocationsAsync(
        [FromRoute] long orderId,
        [FromBody] ReleaseOrderAllocationsRequestDto? request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _mrpAllocationService.ReleaseOrderAllocationsAsync(
            orderId,
            request?.Reason ?? "Giải phóng giữ chỗ vật tư theo điều độ kế hoạch.",
            userId,
            cancellationToken);

        return Ok(ApiResponse<List<MaterialOrderAllocationDto>>.Ok(result, "Released reserved inventory allocations for order."));
    }

    /// <summary>
    /// Lấy danh sách các bản ghi giữ chỗ vật tư (<c>material_allocations</c>) của một Đơn hàng.
    /// </summary>
    [HttpGet("orders/{orderId:long}/allocations")]
    [ProducesResponseType(typeof(ApiResponse<List<MaterialOrderAllocationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllocationsByOrderIdAsync(
        [FromRoute] long orderId,
        CancellationToken cancellationToken)
    {
        var result = await _mrpAllocationService.GetAllocationsByOrderIdAsync(orderId, cancellationToken);
        return Ok(ApiResponse<List<MaterialOrderAllocationDto>>.Ok(result, "Fetched material allocations for order (SC-14)."));
    }

    /// <summary>
    /// Lấy danh sách các bản ghi giữ chỗ vật tư (<c>material_allocations</c>) của một Không gian Đơn hàng (<c>OrderSpace</c>).
    /// </summary>
    [HttpGet("spaces/{spaceId:long}/allocations")]
    [ProducesResponseType(typeof(ApiResponse<List<MaterialOrderAllocationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllocationsBySpaceIdAsync(
        [FromRoute] long spaceId,
        CancellationToken cancellationToken)
    {
        var result = await _mrpAllocationService.GetAllocationsBySpaceIdAsync(spaceId, cancellationToken);
        return Ok(ApiResponse<List<MaterialOrderAllocationDto>>.Ok(result, "Fetched material allocations for order space (SC-14)."));
    }
}
