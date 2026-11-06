using System.Text.Json;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.InventoryPr;
using WMMS.Application.Interfaces.External;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Entities.InventoryPr;
using WMMS.Domain.Enums;
using WMMS.Domain.Exceptions;

namespace WMMS.Application.Services.InventoryPr;

/// <summary>
/// Dịch vụ Quản lý Phiếu Yêu cầu Mua hàng gộp (PR) &amp; Luồng phê duyệt của Chủ xưởng (SC-15).
/// Phục vụ các use case:
/// - UC-45 Xem danh sách PR, UC-46 Xem chi tiết PR (SCR-18).
/// - UC-47 Phê duyệt PR, UC-48 Từ chối PR.
/// Tuân thủ quy tắc:
/// - BR-PUR-02: Chỉ Chủ xưởng (OWNER) được phê duyệt/từ chối; Purchasing chỉ thấy PR đã duyệt và không được phát hành PO trên PR chưa duyệt.
/// - GBR-05: Mọi quyết định ghi nhật ký kiểm toán bất biến (<c>audit_logs</c>) kèm trạng thái trước/sau.
/// - UC047.E1: Xung đột đồng thời (PR đã được xử lý ở phiên khác) trả HTTP 409.
/// Luồng trạng thái: PENDING_APPROVAL → APPROVED | REJECTED (APPROVED → PO_ISSUED thuộc phân hệ Purchasing).
/// </summary>
public class PurchaseRequestService : IPurchaseRequestService
{
    private static readonly PrStatusEnum[] ApprovedVisibleStatuses = { PrStatusEnum.APPROVED, PrStatusEnum.PO_ISSUED };

    private readonly IPurchaseRequestRepository _purchaseRequestRepository;
    private readonly IBomRepository _bomRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationHubService? _notificationHubService;

    public PurchaseRequestService(
        IPurchaseRequestRepository purchaseRequestRepository,
        IBomRepository bomRepository,
        IUnitOfWork unitOfWork,
        INotificationHubService? notificationHubService = null)
    {
        _purchaseRequestRepository = purchaseRequestRepository;
        _bomRepository = bomRepository;
        _unitOfWork = unitOfWork;
        _notificationHubService = notificationHubService;
    }

    /// <inheritdoc />
    public async Task<PagedResult<PurchaseRequestListItemDto>> GetPurchaseRequestsAsync(
        PurchaseRequestFilterDto filter,
        bool restrictToApprovedOnly,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        PrStatusEnum[]? statuses = null;
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            if (!Enum.TryParse<PrStatusEnum>(filter.Status.Trim(), ignoreCase: true, out var parsed))
            {
                throw new AppException(
                    "Invalid PR status. Allowed values: PENDING_APPROVAL, APPROVED, REJECTED, PO_ISSUED.",
                    400,
                    MessageCodes.Msg13ValidationError);
            }

            statuses = new[] { parsed };
        }

        // BR-PUR-02: Purchasing chỉ được thấy PR đã được Chủ xưởng phê duyệt
        if (restrictToApprovedOnly)
        {
            statuses = statuses is null
                ? ApprovedVisibleStatuses
                : statuses.Where(s => ApprovedVisibleStatuses.Contains(s)).ToArray();

            if (statuses.Length == 0)
            {
                return new PagedResult<PurchaseRequestListItemDto> { PageNumber = pageNumber, PageSize = pageSize };
            }
        }

        var (items, total) = await _purchaseRequestRepository.SearchAsync(
            statuses,
            filter.FromDate,
            filter.ToDate,
            filter.SearchKeyword,
            pageNumber,
            pageSize,
            cancellationToken);

        return new PagedResult<PurchaseRequestListItemDto>
        {
            Items = items.Select(MapToListItem).ToList(),
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<PurchaseRequestDetailDto> GetPurchaseRequestByIdAsync(
        long prId,
        bool restrictToApprovedOnly,
        CancellationToken cancellationToken = default)
    {
        var pr = await GetOrThrowAsync(prId, cancellationToken);

        if (restrictToApprovedOnly && !ApprovedVisibleStatuses.Contains(pr.PrStatus))
        {
            throw AppException.Forbidden(
                "Purchasing Staff can only view PRs that have been approved by the Factory Owner (BR-PUR-02).",
                MessageCodes.Msg11ForbiddenRule);
        }

        return MapToDetail(pr);
    }

    /// <inheritdoc />
    public async Task<PurchaseRequestDetailDto> ApprovePurchaseRequestAsync(
        long prId,
        ApprovePurchaseRequestDto request,
        long ownerUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var notes = string.IsNullOrWhiteSpace(request?.Notes) ? null : request.Notes.Trim();
        if (notes != null && notes.Length > 500)
        {
            throw new AppException(
                "Approval notes must not exceed 500 characters.",
                400,
                MessageCodes.Msg13ValidationError);
        }

        var decided = await DecideAsync(
            prId,
            PrStatusEnum.APPROVED,
            rejectionReason: null,
            reasonForAudit: notes ?? $"Factory Owner approved consolidated PR #{prId} (UC-47, BR-PUR-02).",
            actionType: AuditActionConstants.ConsolidatedPrApproved,
            ownerUserId,
            ipAddress,
            cancellationToken);

        await NotifyAsync(decided.PrCode, approved: true, notes, cancellationToken);
        return decided;
    }

    /// <inheritdoc />
    public async Task<PurchaseRequestDetailDto> RejectPurchaseRequestAsync(
        long prId,
        RejectPurchaseRequestDto request,
        long ownerUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new AppException(
                "Rejection reason is required (UC048.E1).",
                400,
                MessageCodes.Msg01RequiredField);
        }

        var reason = request.Reason.Trim();
        if (reason.Length > 500)
        {
            throw new AppException(
                "Rejection reason must not exceed 500 characters.",
                400,
                MessageCodes.Msg13ValidationError);
        }

        var decided = await DecideAsync(
            prId,
            PrStatusEnum.REJECTED,
            rejectionReason: reason,
            reasonForAudit: reason,
            actionType: AuditActionConstants.ConsolidatedPrRejected,
            ownerUserId,
            ipAddress,
            cancellationToken);

        // Phần thiếu hụt của PR bị từ chối không còn nằm trong PR nào đang chờ duyệt,
        // nên lần chạy phân bổ/gom kế tiếp của KHVT sẽ đưa lại vào PR mới (UC-48 POST-3).
        await NotifyAsync(decided.PrCode, approved: false, reason, cancellationToken);
        return decided;
    }

    // =========================================================================
    // PRIVATE
    // =========================================================================

    private async Task<PurchaseRequestDetailDto> DecideAsync(
        long prId,
        PrStatusEnum newStatus,
        string? rejectionReason,
        string reasonForAudit,
        string actionType,
        long ownerUserId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var pr = await GetOrThrowAsync(prId, cancellationToken);
        var previousStatus = pr.PrStatus;

        if (previousStatus != PrStatusEnum.PENDING_APPROVAL)
        {
            throw AppException.Conflict(
                $"PR {pr.PrCode} is already {previousStatus} and can no longer be reviewed (UC047.E1).",
                MessageCodes.Msg08DuplicateConflict);
        }

        var now = DateTime.UtcNow;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Chuyển trạng thái nguyên tử: chỉ thành công nếu PR vẫn đang PENDING_APPROVAL (chống 2 phiên cùng duyệt)
            var transitioned = await _purchaseRequestRepository.TryTransitionStatusAsync(
                prId,
                PrStatusEnum.PENDING_APPROVAL,
                newStatus,
                ownerUserId,
                rejectionReason,
                now,
                cancellationToken);

            if (!transitioned)
            {
                throw AppException.Conflict(
                    $"PR {pr.PrCode} was already processed in another session (UC047.E1).",
                    MessageCodes.Msg08DuplicateConflict);
            }

            await _bomRepository.AddAuditLogAsync(new AuditLog
            {
                UserId = ownerUserId,
                ActionType = actionType,
                EntityName = "PurchaseRequest",
                EntityId = pr.PrCode,
                BeforeState = JsonSerializer.Serialize(new { PrStatus = previousStatus.ToString() }),
                AfterState = JsonSerializer.Serialize(new
                {
                    PrStatus = newStatus.ToString(),
                    ReviewedBy = ownerUserId,
                    ReviewedAt = now,
                    RejectionReason = rejectionReason,
                    ItemsCount = pr.Items.Count
                }),
                Reason = reasonForAudit,
                IpAddress = ipAddress,
                CreatedAt = now
            });

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

        var refreshed = await GetOrThrowAsync(prId, cancellationToken);
        return MapToDetail(refreshed);
    }

    private async Task NotifyAsync(string prCode, bool approved, string? detail, CancellationToken cancellationToken)
    {
        if (_notificationHubService is null)
        {
            return;
        }

        try
        {
            await _notificationHubService.NotifyPurchaseRequestDecidedAsync(prCode, approved, detail, cancellationToken);
        }
        catch
        {
            // Lỗi thông báo real-time không được làm hỏng quyết định đã commit.
        }
    }

    private async Task<PurchaseRequest> GetOrThrowAsync(long prId, CancellationToken cancellationToken)
    {
        return await _purchaseRequestRepository.GetByIdAsync(prId, cancellationToken)
            ?? throw AppException.NotFound($"Purchase Request with ID = {prId} was not found.");
    }

    private static PurchaseRequestListItemDto MapToListItem(PurchaseRequest pr)
    {
        var dto = new PurchaseRequestListItemDto();
        FillListItem(dto, pr);
        return dto;
    }

    private static void FillListItem(PurchaseRequestListItemDto dto, PurchaseRequest pr)
    {
        dto.PrId = pr.PrId;
        dto.PrCode = pr.PrCode;
        dto.AggregationDate = pr.AggregationDate;
        dto.PrStatus = pr.PrStatus.ToString();
        dto.ItemsCount = pr.Items.Count;
        dto.TotalShortageQty = Math.Round(pr.Items.Sum(i => i.TotalShortageQty), 4);
        dto.TotalSuggestedOrderQty = Math.Round(pr.Items.Sum(i => i.SuggestedOrderQty), 4);
        dto.CreatedBy = pr.CreatedBy;
        dto.CreatorName = pr.Creator?.FullName;
        dto.ApprovedBy = pr.ApprovedBy;
        dto.ApproverName = pr.Approver?.FullName;
        dto.ReviewedAt = pr.ReviewedAt;
        dto.CreatedAt = pr.CreatedAt;
    }

    private static PurchaseRequestDetailDto MapToDetail(PurchaseRequest pr)
    {
        var detail = new PurchaseRequestDetailDto
        {
            RejectionReason = pr.RejectionReason,
            PurchaseOrdersCount = pr.PurchaseOrders.Count
        };
        FillListItem(detail, pr);

        foreach (var item in pr.Items.OrderBy(i => i.Material?.MaterialCode))
        {
            var sources = ParseSourceOrders(item.SourceOrderIdsJson);
            detail.Items.Add(new PurchaseRequestItemDto
            {
                PrItemId = item.PrItemId,
                MaterialId = item.MaterialId,
                MaterialCode = item.Material?.MaterialCode ?? string.Empty,
                MaterialName = item.Material?.MaterialName ?? string.Empty,
                UnitOfMeasure = item.Material?.UnitOfMeasure ?? string.Empty,
                TotalShortageQty = item.TotalShortageQty,
                SuggestedOrderQty = item.SuggestedOrderQty,
                AvailableStockQty = item.Material?.MaterialStock?.AvailableQty ?? 0m,
                EarliestEtd = sources.Select(s => s.EtdDate).Where(e => !string.IsNullOrWhiteSpace(e)).OrderBy(e => e).FirstOrDefault(),
                SourceOrders = sources
            });
        }

        detail.ContributingSpaceKeys = detail.Items
            .SelectMany(i => i.SourceOrders)
            .Select(s => s.SpaceKey)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k!)
            .Distinct()
            .OrderBy(k => k)
            .ToList();

        return detail;
    }

    /// <summary>Đọc <c>source_order_ids_json</c> (mảng đối tượng do MrpAllocationService ghi); bỏ qua nếu JSON hỏng.</summary>
    private static List<PurchaseRequestSourceOrderDto> ParseSourceOrders(string? json)
    {
        var result = new List<PurchaseRequestSourceOrderDto>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                result.Add(new PurchaseRequestSourceOrderDto
                {
                    OrderId = GetLong(el, "OrderId"),
                    PoNumber = GetString(el, "PoNumber"),
                    SpaceKey = GetString(el, "SpaceKey"),
                    OrderItemId = GetLong(el, "OrderItemId"),
                    ProductSku = GetString(el, "ProductSku"),
                    EtdDate = GetString(el, "EtdDate"),
                    ShortageQty = GetDecimal(el, "ShortageQty") ?? 0m
                });
            }
        }
        catch (JsonException)
        {
            // JSON không hợp lệ: trả danh sách rỗng thay vì làm hỏng màn hình chi tiết PR
        }

        return result;
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static long? GetLong(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var v) ? v : null;

    private static decimal? GetDecimal(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var v) ? v : null;
}
