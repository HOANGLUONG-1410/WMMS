using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.KanbanQc;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;

namespace WMMS.API.Controllers;

/// <summary>
/// Controller Quản lý điểm dừng Hold Point và Luồng gửi yêu cầu &amp; Phê duyệt ghi đè Overdriver (SC-20, KBN02, GBR-05).
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
        return Ok(ApiResponse<HoldPointStatusDto>.Ok(result, "Lấy thông tin trạng thái Hold Point thành công (SC-20)."));
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
    /// SC-20: Kéo thẻ chuyển công đoạn tiếp theo (Advance Stage / Move Kanban Card).
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
