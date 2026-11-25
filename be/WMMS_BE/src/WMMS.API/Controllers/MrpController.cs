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
[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Leader},{RoleConstants.Purchasing},{RoleConstants.Warehouse},{RoleConstants.Sales}")]
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
        return Ok(ApiResponse<MrpAnalysisResultDto>.Ok(result, "Phân tích nhu cầu nguyên vật liệu và thiếu hụt toàn xưởng thành công."));
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
        return Ok(ApiResponse<MrpAnalysisResultDto>.Ok(result, "Phân tích nhu cầu vật tư và thiếu hụt ròng cho đơn hàng thành công."));
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
        return Ok(ApiResponse<MrpAnalysisResultDto>.Ok(result, "Phân tích nhu cầu vật tư và thiếu hụt ròng cho không gian đơn hàng thành công."));
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
        return Ok(ApiResponse<AutoAllocationResultDto>.Ok(result, "Thực thi phân bổ tồn kho vật tư tự động cho đơn hàng thành công."));
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
        return Ok(ApiResponse<AutoAllocationResultDto>.Ok(result, "Thực thi phân bổ tồn kho vật tư tự động cho không gian đơn hàng thành công."));
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
        return Ok(ApiResponse<AutoAllocationResultDto>.Ok(result, "Thực thi phân bổ tồn kho vật tư tự động cho dòng sản phẩm thành công."));
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

        return Ok(ApiResponse<ReallocateByEtdResultDto>.Ok(result, "Tái phân bổ tồn kho khả dụng theo thứ tự ưu tiên hạn giao hàng sớm nhất thành công."));
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

        return Ok(ApiResponse<List<MaterialOrderAllocationDto>>.Ok(result, "Giải phóng giữ chỗ vật tư cho đơn hàng thành công."));
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
        return Ok(ApiResponse<List<MaterialOrderAllocationDto>>.Ok(result, "Lấy danh sách phân bổ vật tư của đơn hàng thành công."));
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
        return Ok(ApiResponse<List<MaterialOrderAllocationDto>>.Ok(result, "Lấy danh sách phân bổ vật tư của không gian đơn hàng thành công."));
    }

    /// <summary>
    /// [P0-04 &amp; UC-14]: Yêu cầu cấp bù nguyên vật liệu cho số lượng sản phẩm phế phẩm bị hủy (Scrap Replenishment Request).
    /// Tự động nhân định mức BOM theo số lượng phế phẩm, đối soát tồn kho khả dụng để khóa giữ chỗ (RESERVED),
    /// hoặc tự động sinh phiếu Yêu cầu mua hàng (PR) gửi Chủ xưởng phê duyệt mua thêm nếu kho không đủ.
    /// </summary>
    /// <param name="request">DTO chứa OrderItemId, ScrappedQty, Reason, AutoCreatePrIfShortage.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>Kết quả cấp bù vật tư phế phẩm và mã phiếu PR sinh tự động (nếu có).</returns>
    [HttpPost("scrap-replenishment-request")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Leader},{RoleConstants.Warehouse}")]
    [ProducesResponseType(typeof(ApiResponse<ScrapReplenishmentResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestScrapReplenishmentAsync(
        [FromBody] ScrapReplenishmentRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _mrpAllocationService.RequestScrapReplenishmentAsync(request, userId, cancellationToken);
        return Ok(ApiResponse<ScrapReplenishmentResultDto>.Ok(result, result.Message));
    }

    /// <summary>
    /// [P0-04, SC-14b]: Xem trước &amp; Tính toán Nhu cầu Nguyên vật liệu cấp bù phế phẩm (Scrap Replenishment Preview).
    /// Cho phép Tổ trưởng / Quản đốc (Leader) và Kế hoạch (Planner) đối soát tồn kho, kiểm tra xem kho có đủ vật tư để cấp phát lại không,
    /// hoặc có cần gửi Yêu cầu mua hàng (PR) bổ sung hay không trước khi bấm xác nhận cấp bù.
    /// </summary>
    /// <param name="orderItemId">ID dòng sản phẩm đơn hàng.</param>
    /// <param name="scrappedQty">Số lượng phế phẩm muốn tính toán (nếu để trống, hệ thống tự động quét từ số lượng lỗi đã hủy).</param>
    /// <param name="cancellationToken">Token hủy tác vụ.</param>
    [HttpGet("order-items/{orderItemId:long}/scrap-replenishment-preview")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Planner},{RoleConstants.Leader},{RoleConstants.Warehouse}")]
    [ProducesResponseType(typeof(ApiResponse<ScrapReplenishmentPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetScrapReplenishmentPreviewAsync(
        [FromRoute] long orderItemId,
        [FromQuery] int? scrappedQty,
        CancellationToken cancellationToken)
    {
        var result = await _mrpAllocationService.GetScrapReplenishmentPreviewAsync(orderItemId, scrappedQty, cancellationToken);
        return Ok(ApiResponse<ScrapReplenishmentPreviewDto>.Ok(result, "Tính toán định mức và đối soát tồn kho cấp bù phế phẩm thành công."));
    }
}
