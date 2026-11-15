using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.KanbanQc;

namespace WMMS.Application.Interfaces.Services;

/// <summary>
/// Giao diện dịch vụ kiểm soát điểm dừng Hold Point và quy trình phê duyệt ghi đè Overdriver (SC-20, KBN02, GBR-05).
/// </summary>
public interface IHoldPointService
{
    /// <summary>
    /// Lấy chi tiết trạng thái kiểm soát Hold Point của Thẻ Lô Sản Xuất (bao gồm các công đoạn, lịch sử ghi đè, và cờ được phép chuyển tiếp).
    /// </summary>
    Task<HoldPointStatusDto> GetBatchHoldPointStatusAsync(long batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tổ trưởng SX (LEADER), ADMIN hoặc PLANNER gửi yêu cầu ghi đè / phá khóa điểm dừng Hold Point (KBN02).
    /// </summary>
    Task<HoldPointOverrideResultDto> RequestOverrideAsync(
        RequestHoldPointOverrideDto request,
        long requesterUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chủ xưởng (OWNER) hoặc ADMIN phê duyệt yêu cầu ghi đè điểm dừng Hold Point (KBN02).
    /// </summary>
    Task<HoldPointOverrideResultDto> ApproveOverrideAsync(
        long batchId,
        ApproveHoldPointOverrideDto request,
        long approverUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chủ xưởng (OWNER) hoặc ADMIN từ chối yêu cầu ghi đè điểm dừng Hold Point (KBN02).
    /// </summary>
    Task<HoldPointOverrideResultDto> RejectOverrideAsync(
        long batchId,
        RejectHoldPointOverrideDto request,
        long rejecterUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tổ trưởng SX (LEADER) hoặc ADMIN kéo thẻ chuyển công đoạn tiếp theo (Advance Stage / Move Kanban Card).
    /// Kiểm soát chặn nghiêm ngặt: yêu cầu 100% QC Pass và chặn nếu gặp trạm Hold Point mà vật tư chưa xuất kho (KBN01, KBN02).
    /// </summary>
    Task<AdvanceStageResultDto> AdvanceStageAsync(
        long batchId,
        AdvanceStageRequestDto? request,
        long currentUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tra cứu lịch sử các lần ghi đè điểm dừng Hold Point (phân trang và lọc theo Thẻ Lô).
    /// </summary>
    Task<PagedResult<OverdriveLogDto>> GetOverdriveLogsAsync(
        long? batchId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
