using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.KanbanQc;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;

namespace WMMS.API.Controllers;

/// <summary>
/// [Dev-Iter 2] Controller Quản lý Điểm dừng Kiểm tra Hold Point &amp; Luồng Gửi yêu cầu / Phê duyệt Ghi đè Overdriver (SC-20, KBN02, GBR-05).
/// <para>
/// - Chức năng:
///   + <c>GET /api/v1/hold-point/batches/{batchId}/status</c>: Xem trạng thái trạm dừng kỹ thuật, tiến độ công đoạn và lý do bị chặn của Thẻ Lô Sản Xuất.
///   + <c>POST /api/v1/hold-point/override-requests</c>: Tổ trưởng SX (<c>LEADER</c>/<c>PLANNER</c>) gửi yêu cầu xin ghi đè / chạy trước Overdriver kèm lý do giải trình bắt buộc.
///   + <c>POST /api/v1/hold-point/override-requests/{batchId}/approve</c> &amp; <c>reject</c>: Chủ xưởng (<c>OWNER</c>) phê duyệt mở khóa hoặc từ chối khóa cứng thẻ tại trạm Hold Point.
///   + <c>POST /api/v1/hold-point/batches/{batchId}/advance</c>: Chuyển bước công đoạn tuần tự có kiểm soát 100% QC Pass (KBN01) và chặn tự động tại Hold Point (KBN02).
///   + <c>GET /api/v1/hold-point/overdrive-logs</c>: Tra cứu nhật ký phá khóa ngoại lệ (<c>overdrive_logs</c>).
/// - Móc nối: Gọi <see cref="IHoldPointService"/> (<c>HoldPointService</c>) -&gt; Tác động bảng <c>production_batches</c>, <c>batch_routing_steps</c>, <c>overdrive_logs</c>, <c>audit_logs</c>; dữ liệu <c>overdrive_logs</c> móc nối trực tiếp sang Báo cáo Thống kê Tần suất Overdriver &amp; Xuất Excel ở Iteration 3.
/// </para>
/// </summary>
[Route("api/v1/hold-point")]
[Authorize]
public class HoldPointController : MasterDataControllerBase
{
    private readonly IHoldPointService _holdPointService;

    public HoldPointController(IHoldPointService holdPointService)
    {
        _holdPointService = holdPointService;
    }

    /// <summary>
    /// SC-20: Xem chi tiết trạng thái điểm dừng Hold Point và tiến độ công đoạn của Thẻ Lô Sản Xuất.
    /// </summary>
    [HttpGet("batches/{batchId:long}/status")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Leader},{RoleConstants.Planner},{RoleConstants.Qc}")]
    [ProducesResponseType(typeof(ApiResponse<HoldPointStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBatchHoldPointStatusAsync(
        [FromRoute] long batchId,
        CancellationToken cancellationToken)
    {
        var result = await _holdPointService.GetBatchHoldPointStatusAsync(batchId, cancellationToken);
        return Ok(ApiResponse<HoldPointStatusDto>.Ok(result, "Lấy thông tin trạng thái trạm dừng kỹ thuật thành công."));
    }

    /// <summary>
    /// SC-20: Tổ trưởng SX (LEADER), ADMIN hoặc PLANNER gửi yêu cầu ghi đè / phá khóa điểm dừng Hold Point (KBN02).
    /// </summary>
    [HttpPost("override-requests")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Leader},{RoleConstants.Planner}")]
    [ProducesResponseType(typeof(ApiResponse<HoldPointOverrideResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestOverrideAsync(
        [FromBody] RequestHoldPointOverrideDto request,
        CancellationToken cancellationToken)
    {
        var result = await _holdPointService.RequestOverrideAsync(
            request,
            GetCurrentUserId(),
            GetClientIpAddress(),
            cancellationToken);

        return Ok(ApiResponse<HoldPointOverrideResultDto>.Ok(
            result,
            $"Đã gửi yêu cầu ghi đè điểm dừng Hold Point công đoạn {result.HoldPointStageSeq} tới Chủ xưởng."));
    }

    /// <summary>
    /// SC-20: Chủ xưởng (OWNER) hoặc ADMIN phê duyệt ghi đè điểm dừng Hold Point (KBN02, GBR-05).
    /// </summary>
    [HttpPost("override-requests/{batchId:long}/approve")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner}")]
    [ProducesResponseType(typeof(ApiResponse<HoldPointOverrideResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveOverrideAsync(
        [FromRoute] long batchId,
        [FromBody] ApproveHoldPointOverrideDto request,
        CancellationToken cancellationToken)
    {
        var result = await _holdPointService.ApproveOverrideAsync(
            batchId,
            request,
            GetCurrentUserId(),
            GetClientIpAddress(),
            cancellationToken);

        return Ok(ApiResponse<HoldPointOverrideResultDto>.Ok(
            result,
            $"Chủ xưởng đã phê duyệt ghi đè điểm dừng Hold Point cho Thẻ Lô '{result.BatchCode}'. Thẻ đã được mở khóa tiếp tục sản xuất."));
    }

    /// <summary>
    /// SC-20: Chủ xưởng (OWNER) hoặc ADMIN từ chối yêu cầu ghi đè điểm dừng Hold Point (KBN02).
    /// </summary>
    [HttpPost("override-requests/{batchId:long}/reject")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner}")]
    [ProducesResponseType(typeof(ApiResponse<HoldPointOverrideResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectOverrideAsync(
        [FromRoute] long batchId,
        [FromBody] RejectHoldPointOverrideDto request,
        CancellationToken cancellationToken)
    {
        var result = await _holdPointService.RejectOverrideAsync(
            batchId,
            request,
            GetCurrentUserId(),
            GetClientIpAddress(),
            cancellationToken);

        return Ok(ApiResponse<HoldPointOverrideResultDto>.Ok(
            result,
            $"Yêu cầu ghi đè điểm dừng Hold Point cho Thẻ Lô '{result.BatchCode}' đã bị từ chối. Thẻ bị khóa cứng tại trạm kiểm soát."));
    }

    /// <summary>
    /// SC-20: Xác nhận hoàn thành công đoạn hiện tại và chuyển lô sản xuất sang công đoạn kế tiếp (S -> S + 1).
    /// Kiểm soát chặt chẽ: 100% QC Pass và chặn dừng tự động tại các trạm kiểm soát Hold Point nếu thiếu vật tư.
    /// </summary>
    [HttpPost("batches/{batchId:long}/advance")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Leader}")]
    [ProducesResponseType(typeof(ApiResponse<AdvanceStageResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdvanceStageAsync(
        [FromRoute] long batchId,
        [FromBody] AdvanceStageRequestDto? request,
        CancellationToken cancellationToken)
    {
        var result = await _holdPointService.AdvanceStageAsync(
            batchId,
            request,
            GetCurrentUserId(),
            GetClientIpAddress(),
            cancellationToken);

        var message = result.IsCompleted
            ? $"Thẻ Lô '{result.BatchCode}' đã hoàn tất công đoạn cuối cùng ({result.NewStageName}) thành công!"
            : $"Thẻ Lô '{result.BatchCode}' đã chuyển thành công sang công đoạn {result.NewStageSeq} ({result.NewStageName}).";

        return Ok(ApiResponse<AdvanceStageResultDto>.Ok(result, message));
    }

    /// <summary>
    /// SC-20: Tra cứu lịch sử nhật ký phá khóa / ghi đè điểm dừng Hold Point (overdrive_logs).
    /// </summary>
    [HttpGet("overdrive-logs")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Leader},{RoleConstants.Planner}")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<OverdriveLogDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverdriveLogsAsync(
        [FromQuery] long? batchId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _holdPointService.GetOverdriveLogsAsync(batchId, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<OverdriveLogDto>>.Ok(result, "Lấy danh sách nhật ký ghi đè Hold Point thành công."));
    }
}
