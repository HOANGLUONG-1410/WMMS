using System.Text.Json;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.Purchasing;
using WMMS.Application.Interfaces.External;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Entities.InventoryPr;
using WMMS.Domain.Entities.WarehousePo;
using WMMS.Domain.Enums;
using WMMS.Domain.Exceptions;

namespace WMMS.Application.Services.Purchasing;

/// <summary>
/// Dịch vụ lập Đơn đặt hàng Nhà cung cấp (PO) gom theo Supplier &amp; Theo dõi tiến độ ETA (SC-16).
/// Phục vụ các use case: UC-49 (PR đã duyệt chờ đặt hàng), UC-50 (Danh sách PO), UC-51 (Chi tiết PO),
/// UC-52 (Tạo PO), UC-53 (Cập nhật lịch giao hàng).
/// Tuân thủ quy tắc:
/// - BR-PUR-02: Chỉ phát hành PO trên PR đã được Chủ xưởng phê duyệt (APPROVED).
/// - BR-PUR-03: Mỗi dòng PR phải gán Nhà cung cấp (đang ACTIVE); các dòng cùng Supplier được gom thành một PO.
/// - UC052.E1: Ngày hẹn giao không được ở quá khứ.
/// - GBR-05: Mọi thay đổi ghi nhật ký kiểm toán bất biến (<c>audit_logs</c>).
/// Trạng thái PO: ISSUED → PARTIALLY_RECEIVED → FULFILLED (cập nhật bởi phiếu nhập kho SC-17) hoặc CANCELLED.
/// </summary>
public class PurchaseOrderService : IPurchaseOrderService
{
    /// <summary>PO chưa nhận đủ có ETA trong vòng số ngày này được gắn cờ DUE_SOON.</summary>
    public const int DueSoonDays = 3;

    private const decimal QtyTolerance = 0.0001m;

    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IPurchaseRequestRepository _purchaseRequestRepository;
    private readonly IBomRepository _bomRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationHubService? _notificationHubService;

    public PurchaseOrderService(
        IPurchaseOrderRepository purchaseOrderRepository,
        IPurchaseRequestRepository purchaseRequestRepository,
        IBomRepository bomRepository,
        IUnitOfWork unitOfWork,
        INotificationHubService? notificationHubService = null)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _purchaseRequestRepository = purchaseRequestRepository;
        _bomRepository = bomRepository;
        _unitOfWork = unitOfWork;
        _notificationHubService = notificationHubService;
    }

    // =========================================================================
    // [SC-16 CORE]: PHÂN ĐOẠN 1 - LẤY DANH SÁCH PR ĐÃ DUYỆT CHỜ ĐẶT HÀNG (UC-49, SCR-19)
    // =========================================================================
    /// <inheritdoc />
    public async Task<List<ApprovedRequisitionDto>> GetApprovedRequisitionsAsync(CancellationToken cancellationToken = default)
    {
        var (prs, _) = await _purchaseRequestRepository.SearchAsync(
            new[] { PrStatusEnum.APPROVED }, null, null, null, 1, 100, cancellationToken);

        var ordered = await _purchaseOrderRepository.GetOrderedQtyByPrAsync(
            prs.Select(p => p.PrId).ToList(), cancellationToken);

        var result = new List<ApprovedRequisitionDto>();
        foreach (var pr in prs.OrderBy(p => p.AggregationDate).ThenBy(p => p.PrId))
        {
            var lines = new List<ApprovedRequisitionLineDto>();
            foreach (var item in pr.Items.OrderBy(i => i.Material?.MaterialCode))
            {
                var orderedQty = ordered.GetValueOrDefault((pr.PrId, item.MaterialId));
                var remaining = Math.Round(item.SuggestedOrderQty - orderedQty, 4);
                if (remaining <= QtyTolerance)
                {
                    continue;
                }

                lines.Add(new ApprovedRequisitionLineDto
                {
                    PrItemId = item.PrItemId,
                    MaterialId = item.MaterialId,
                    MaterialCode = item.Material?.MaterialCode ?? string.Empty,
                    MaterialName = item.Material?.MaterialName ?? string.Empty,
                    MaterialGroup = item.Material?.MaterialGroup.ToString() ?? string.Empty,
                    UnitOfMeasure = item.Material?.UnitOfMeasure ?? string.Empty,
                    SuggestedOrderQty = item.SuggestedOrderQty,
                    OrderedQty = orderedQty,
                    RemainingQty = remaining
                });
            }

            if (lines.Count == 0)
            {
                continue;
            }

            result.Add(new ApprovedRequisitionDto
            {
                PrId = pr.PrId,
                PrCode = pr.PrCode,
                AggregationDate = pr.AggregationDate,
                ReviewedAt = pr.ReviewedAt,
                Lines = lines
            });
        }

        return result;
    }

    // =========================================================================
    // [SC-16 CORE]: PHÂN ĐOẠN 2 - TRUY VẤN DANH SÁCH PO CÓ PHÂN TRANG & BỘ LỌC ETA (UC-50)
    // =========================================================================
    /// <inheritdoc />
    public async Task<PagedResult<PurchaseOrderListItemDto>> GetPurchaseOrdersAsync(
        PurchaseOrderFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var today = DateTime.UtcNow.Date;

        PoStatusEnum? status = null;
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            if (!Enum.TryParse<PoStatusEnum>(filter.Status.Trim(), ignoreCase: true, out var parsed))
            {
                throw new AppException(
                    "Trạng thái PO không hợp lệ. Các giá trị được phép: ISSUED, PARTIALLY_RECEIVED, FULFILLED, CANCELLED.",
                    400,
                    MessageCodes.Msg13ValidationError);
            }

            status = parsed;
        }

        DateTime? overdueBefore = null, dueSoonFrom = null, dueSoonTo = null;
        var etaFilter = filter.EtaStatus?.Trim().ToUpperInvariant();
        if (etaFilter == "OVERDUE")
        {
            overdueBefore = today;
        }
        else if (etaFilter == "DUE_SOON")
        {
            dueSoonFrom = today;
            dueSoonTo = today.AddDays(DueSoonDays);
        }
        else if (!string.IsNullOrEmpty(etaFilter))
        {
            throw new AppException("Trạng thái ETA không hợp lệ. Các giá trị được phép: OVERDUE, DUE_SOON.", 400, MessageCodes.Msg13ValidationError);
        }

        var (items, total) = await _purchaseOrderRepository.SearchAsync(
            filter.SupplierId, status, filter.FromEta, filter.ToEta,
            overdueBefore, dueSoonFrom, dueSoonTo, filter.SearchKeyword,
            pageNumber, pageSize, cancellationToken);

        return new PagedResult<PurchaseOrderListItemDto>
        {
            Items = items.Select(po => MapToListItem(po, today)).ToList(),
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderDetailDto> GetPurchaseOrderByIdAsync(long poId, CancellationToken cancellationToken = default)
    {
        var po = await GetOrThrowAsync(poId, cancellationToken);
        return MapToDetail(po, DateTime.UtcNow.Date);
    }

    // =========================================================================
    // [SC-16 CORE]: PHÂN ĐOẠN 3 - BÁO CÁO TỔNG HỢP KPI & TIẾN ĐỘ ETA (SCR-19)
    // =========================================================================
    /// <inheritdoc />
    public async Task<PurchaseOrderSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var c = await _purchaseOrderRepository.GetStatusCountsAsync(today, today.AddDays(DueSoonDays), cancellationToken);
        return new PurchaseOrderSummaryDto
        {
            IssuedCount = c.Issued,
            PartiallyReceivedCount = c.Partial,
            FulfilledCount = c.Fulfilled,
            CancelledCount = c.Cancelled,
            OverdueCount = c.Overdue,
            DueSoonCount = c.DueSoon
        };
    }

    // =========================================================================
    // [SC-16 CORE & BR-PUR-02, BR-PUR-03]: PHÂN ĐOẠN 4 - THUẬT TOÁN LẬP PO GOM THEO NHÀ CUNG CẤP (UC-52)
    // 1. Kiểm tra BR-PUR-02: PR bắt buộc phải ở trạng thái APPROVED.
    // 2. Kiểm tra BR-PUR-03: Mỗi dòng PR phải gán Supplier đang hoạt động (ACTIVE).
    // 3. Kiểm tra UC052.E1: Ngày hẹn giao hàng của NCC không được ở quá khứ (ExpectedArrivalDate >= Today).
    // 4. Kiểm tra số lượng: Không được đặt vượt số lượng còn lại có thể đặt của dòng PR.
    // 5. Gom nhóm theo Supplier: Mỗi NCC sinh một PO riêng biệt, mã PO chuẩn PO-SUP-yyyyMMdd-NNN.
    // 6. Ghi Audit Log bất biến (GBR-05) và phát SignalR real-time.
    // 7. Chuyển trạng thái PR sang PO_ISSUED nếu tất cả dòng đã đặt đủ 100%.
    // =========================================================================
    /// <inheritdoc />
    public async Task<List<PurchaseOrderDetailDto>> CreatePurchaseOrdersAsync(
        CreatePurchaseOrdersRequestDto request,
        long purchasingUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var pr = await _purchaseRequestRepository.GetByIdAsync(request.PrId, cancellationToken)
            ?? throw AppException.NotFound($"Không tìm thấy Phiếu yêu cầu mua hàng (PR) với mã ID = {request.PrId}.");

        // BR-PUR-02: Không được phát hành PO trên PR chưa được Chủ xưởng phê duyệt
        if (pr.PrStatus is PrStatusEnum.PENDING_APPROVAL or PrStatusEnum.REJECTED)
        {
            throw AppException.Forbidden(
                $"Phiếu PR {pr.PrCode} đang ở trạng thái {pr.PrStatus}. Đơn đặt hàng (PO) chỉ được phát hành từ PR đã được Chủ xưởng phê duyệt (APPROVED) (BR-PUR-02).",
                MessageCodes.Msg11ForbiddenRule);
        }

        if (pr.PrStatus == PrStatusEnum.PO_ISSUED)
        {
            throw AppException.Conflict(
                $"Phiếu PR {pr.PrCode} đã được đặt hàng toàn bộ trước đó (PO_ISSUED).",
                MessageCodes.Msg08DuplicateConflict);
        }

        var itemsById = pr.Items.ToDictionary(i => i.PrItemId);
        foreach (var line in request.Lines)
        {
            if (!itemsById.ContainsKey(line.PrItemId))
            {
                throw new AppException(
                    $"Dòng vật tư PR #{line.PrItemId} không thuộc về Phiếu PR {pr.PrCode}.",
                    400,
                    MessageCodes.Msg13ValidationError);
            }
        }

        // BR-PUR-03: Nhà cung cấp phải tồn tại và đang hoạt động
        var supplierIds = request.Lines.Select(l => l.SupplierId).Distinct().ToList();
        var suppliers = await _purchaseOrderRepository.GetSuppliersByIdsAsync(supplierIds, cancellationToken);
        foreach (var supplierId in supplierIds)
        {
            if (!suppliers.TryGetValue(supplierId, out var supplier))
            {
                throw AppException.NotFound($"Không tìm thấy Nhà cung cấp với mã ID = {supplierId}.");
            }

            if (!string.Equals(supplier.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException(
                    $"Nhà cung cấp {supplier.SupplierCode} đang ở trạng thái {supplier.Status} và không thể tiếp nhận Đơn đặt hàng mới (BR-PUR-03).",
                    400,
                    MessageCodes.Msg13ValidationError);
            }
        }

        var termsBySupplier = request.SupplierTerms.ToDictionary(t => t.SupplierId);
        var today = DateTime.UtcNow.Date;
        foreach (var terms in termsBySupplier.Values)
        {
            if (terms.ExpectedArrivalDate.Date < today)
            {
                throw new AppException(
                    "Ngày hẹn giao hàng của Nhà cung cấp không được ở quá khứ (UC052.E1).",
                    400,
                    MessageCodes.Msg13ValidationError);
            }
        }

        // Không đặt vượt lượng đề xuất còn lại của dòng PR
        var alreadyOrdered = await _purchaseOrderRepository.GetOrderedQtyByPrAsync(new[] { pr.PrId }, cancellationToken);
        var requestedByItem = request.Lines
            .GroupBy(l => l.PrItemId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.OrderedQty));

        foreach (var (prItemId, requested) in requestedByItem)
        {
            var item = itemsById[prItemId];
            var remaining = Math.Round(item.SuggestedOrderQty - alreadyOrdered.GetValueOrDefault((pr.PrId, item.MaterialId)), 4);
            if (requested > remaining + QtyTolerance)
            {
                throw new AppException(
                    $"Số lượng đặt hàng {requested:0.####} vượt quá số lượng còn lại có thể đặt ({Math.Max(0m, remaining):0.####}) của dòng PR #{prItemId}.",
                    400,
                    MessageCodes.Msg13ValidationError);
            }
        }

        var now = DateTime.UtcNow;
        var codePrefix = $"PO-SUP-{now:yyyyMMdd}-";
        var createdPoCodes = new List<string>();

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var sequence = await _purchaseOrderRepository.CountByCodePrefixAsync(codePrefix, cancellationToken);
            var createdOrders = new List<PurchaseOrder>();

            // Gom các dòng theo Supplier: mỗi Supplier một PO
            foreach (var group in request.Lines.GroupBy(l => l.SupplierId).OrderBy(g => g.Key))
            {
                sequence++;
                var terms = termsBySupplier[group.Key];
                var supplier = suppliers[group.Key];

                var po = new PurchaseOrder
                {
                    PoCode = $"{codePrefix}{sequence:D3}",
                    PrId = pr.PrId,
                    SupplierId = group.Key,
                    CreatedBy = purchasingUserId,
                    IssueDate = now,
                    ExpectedArrivalDate = terms.ExpectedArrivalDate.Date,
                    PoStatus = PoStatusEnum.ISSUED,
                    Terms = string.IsNullOrWhiteSpace(terms.Terms) ? null : terms.Terms.Trim(),
                    CreatedAt = now
                };

                foreach (var line in group.OrderBy(l => l.PrItemId))
                {
                    po.Details.Add(new PurchaseOrderDetail
                    {
                        MaterialId = itemsById[line.PrItemId].MaterialId,
                        OrderedQty = Math.Round(line.OrderedQty, 4),
                        ReceivedQty = 0m,
                        UnitPrice = Math.Round(line.UnitPrice, 2)
                    });
                }

                po.TotalAmount = Math.Round(po.Details.Sum(d => d.OrderedQty * (d.UnitPrice ?? 0m)), 2);

                await _purchaseOrderRepository.AddAsync(po, cancellationToken);
                createdOrders.Add(po);
                createdPoCodes.Add(po.PoCode);

                await _bomRepository.AddAuditLogAsync(new AuditLog
                {
                    UserId = purchasingUserId,
                    ActionType = AuditActionConstants.PurchaseOrderIssued,
                    EntityName = nameof(PurchaseOrder),
                    EntityId = po.PoCode,
                    BeforeState = null,
                    AfterState = JsonSerializer.Serialize(new
                    {
                        po.PoCode,
                        PrCode = pr.PrCode,
                        Supplier = supplier.SupplierCode,
                        ExpectedArrivalDate = po.ExpectedArrivalDate.ToString("yyyy-MM-dd"),
                        po.TotalAmount,
                        LinesCount = po.Details.Count,
                        po.Terms
                    }),
                    Reason = $"Phát hành Đơn đặt hàng nhà cung cấp {po.PoCode} từ PR {pr.PrCode} đã duyệt tới {supplier.SupplierName} (UC-52, BR-PUR-03).",
                    IpAddress = ipAddress,
                    CreatedAt = now
                });
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Khi mọi dòng PR đã được đặt đủ thì PR chuyển PO_ISSUED
            var fullyOrdered = pr.Items.All(item =>
            {
                var total = alreadyOrdered.GetValueOrDefault((pr.PrId, item.MaterialId)) +
                            requestedByItem.GetValueOrDefault(item.PrItemId);
                return total + QtyTolerance >= item.SuggestedOrderQty;
            });

            if (fullyOrdered)
            {
                await _purchaseRequestRepository.TryMarkPoIssuedAsync(pr.PrId, now, cancellationToken);
            }

            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

        var results = new List<PurchaseOrderDetailDto>();
        foreach (var code in createdPoCodes)
        {
            var po = await _purchaseOrderRepository.GetByCodeAsync(code, cancellationToken)
                ?? throw AppException.NotFound($"Không tìm thấy Đơn đặt hàng {code} sau khi khởi tạo.");
            results.Add(MapToDetail(po, today));
            await NotifyAsync(po.PoCode, po.Supplier?.SupplierName ?? string.Empty, po.ExpectedArrivalDate, isNew: true, cancellationToken);
        }

        return results;
    }

    // =========================================================================
    // [SC-16 CORE & UC-53]: PHÂN ĐOẠN 5 - CẬP NHẬT TIẾN ĐỘ ETA & LỊCH GIAO HÀNG (UC052.E1)
    // =========================================================================
    /// <inheritdoc />
    public async Task<PurchaseOrderDetailDto> UpdateEtaAsync(
        long poId,
        UpdatePoEtaDto request,
        long purchasingUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var newEta = request.ExpectedArrivalDate.Date;
        if (newEta < today)
        {
            throw new AppException("Ngày hẹn giao hàng của Nhà cung cấp không được ở quá khứ (UC052.E1).", 400, MessageCodes.Msg13ValidationError);
        }

        var po = await GetOrThrowAsync(poId, cancellationToken);
        if (po.PoStatus is PoStatusEnum.FULFILLED or PoStatusEnum.CANCELLED)
        {
            throw AppException.Conflict(
                $"Đơn hàng PO {po.PoCode} đang ở trạng thái {po.PoStatus}; không thể thay đổi lịch giao hàng nữa.",
                MessageCodes.Msg08DuplicateConflict);
        }

        var oldEta = po.ExpectedArrivalDate.Date;
        if (oldEta == newEta)
        {
            return MapToDetail(po, today);
        }

        var now = DateTime.UtcNow;
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            po.ExpectedArrivalDate = newEta;
            po.UpdatedAt = now;
            await _purchaseOrderRepository.UpdateAsync(po);

            await _bomRepository.AddAuditLogAsync(new AuditLog
            {
                UserId = purchasingUserId,
                ActionType = AuditActionConstants.PurchaseOrderUpdated,
                EntityName = nameof(PurchaseOrder),
                EntityId = po.PoCode,
                BeforeState = JsonSerializer.Serialize(new { ExpectedArrivalDate = oldEta.ToString("yyyy-MM-dd") }),
                AfterState = JsonSerializer.Serialize(new { ExpectedArrivalDate = newEta.ToString("yyyy-MM-dd"), Notes = notes }),
                Reason = notes ?? $"Cập nhật ngày hẹn giao hàng của PO {po.PoCode} từ {oldEta:yyyy-MM-dd} sang {newEta:yyyy-MM-dd} (UC-53).",
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

        await NotifyAsync(po.PoCode, po.Supplier?.SupplierName ?? string.Empty, newEta, isNew: false, cancellationToken);
        return MapToDetail(po, today);
    }

    // =========================================================================
    // [SC-16 CORE]: PHÂN ĐOẠN 6 - CẬP NHẬT ĐIỀU KHOẢN THANH TOÁN & QUẢN LÝ VÒNG ĐỜI PO
    // =========================================================================
    /// <inheritdoc />
    public async Task<PurchaseOrderDetailDto> UpdateTermsAsync(
        long poId,
        UpdatePoTermsDto request,
        long purchasingUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var po = await GetOrThrowAsync(poId, cancellationToken);
        var oldTerms = po.Terms;
        var newTerms = request.Terms?.Trim();

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            po.Terms = newTerms;
            po.UpdatedAt = DateTime.UtcNow;

            await _purchaseOrderRepository.UpdateAsync(po);

            await _bomRepository.AddAuditLogAsync(new AuditLog
            {
                UserId = purchasingUserId,
                ActionType = AuditActionConstants.PurchaseOrderUpdated,
                EntityName = nameof(PurchaseOrder),
                EntityId = po.PoCode,
                BeforeState = JsonSerializer.Serialize(new { Terms = oldTerms }),
                AfterState = JsonSerializer.Serialize(new { Terms = newTerms, Notes = request.Notes }),
                Reason = request.Notes ?? $"Cập nhật điều khoản thanh toán & giao hàng của PO {po.PoCode} (SC-16).",
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

        return MapToDetail(po, DateTime.UtcNow.Date);
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderDetailDto> UpdateStatusAsync(
        long poId,
        UpdatePoStatusDto request,
        long purchasingUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var po = await GetOrThrowAsync(poId, cancellationToken);
        if (!Enum.TryParse<PoStatusEnum>(request.Status, true, out var newStatus))
        {
            throw new AppException($"Trạng thái PO không hợp lệ: '{request.Status}'. Các giá trị hợp lệ gồm: ISSUED, PARTIALLY_RECEIVED, FULFILLED, CANCELLED.");
        }

        var oldStatus = po.PoStatus;
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            po.PoStatus = newStatus;
            po.UpdatedAt = DateTime.UtcNow;

            await _purchaseOrderRepository.UpdateAsync(po);

            await _bomRepository.AddAuditLogAsync(new AuditLog
            {
                UserId = purchasingUserId,
                ActionType = AuditActionConstants.PurchaseOrderUpdated,
                EntityName = nameof(PurchaseOrder),
                EntityId = po.PoCode,
                BeforeState = JsonSerializer.Serialize(new { Status = oldStatus.ToString() }),
                AfterState = JsonSerializer.Serialize(new { Status = newStatus.ToString(), Reason = request.Reason }),
                Reason = request.Reason ?? $"Chuyển đổi trạng thái vòng đời PO {po.PoCode} từ {oldStatus} sang {newStatus} (SC-16).",
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

        return MapToDetail(po, DateTime.UtcNow.Date);
    }

    // =========================================================================
    // PRIVATE HELPER METHODS
    // =========================================================================

    private async Task<PurchaseOrder> GetOrThrowAsync(long poId, CancellationToken cancellationToken)
    {
        return await _purchaseOrderRepository.GetByIdAsync(poId, cancellationToken)
            ?? throw AppException.NotFound($"Không tìm thấy Đơn đặt hàng (PO) với mã ID = {poId}.");
    }

    private async Task NotifyAsync(string poCode, string supplierName, DateTime eta, bool isNew, CancellationToken cancellationToken)
    {
        if (_notificationHubService is null)
        {
            return;
        }

        try
        {
            await _notificationHubService.NotifyPurchaseOrderChangedAsync(poCode, supplierName, eta, isNew, cancellationToken);
        }
        catch
        {
            // Lỗi thông báo real-time không được làm hỏng nghiệp vụ đã commit.
        }
    }

    /// <summary>Tình trạng ETA: CANCELLED, RECEIVED (đã nhận đủ), OVERDUE (quá hạn chưa đủ), DUE_SOON hoặc ON_TRACK.</summary>
    internal static string ComputeEtaStatus(PoStatusEnum status, DateTime eta, DateTime today)
    {
        if (status == PoStatusEnum.CANCELLED) return "CANCELLED";
        if (status == PoStatusEnum.FULFILLED) return "RECEIVED";

        var days = (eta.Date - today.Date).Days;
        if (days < 0) return "OVERDUE";
        return days <= DueSoonDays ? "DUE_SOON" : "ON_TRACK";
    }

    private static void FillListItem(PurchaseOrderListItemDto dto, PurchaseOrder po, DateTime today)
    {
        var ordered = po.Details.Sum(d => d.OrderedQty);
        var received = po.Details.Sum(d => Math.Min(d.ReceivedQty, d.OrderedQty));

        dto.PoId = po.PoId;
        dto.PoCode = po.PoCode;
        dto.PrId = po.PrId;
        dto.PrCode = po.PurchaseRequest?.PrCode;
        dto.SupplierId = po.SupplierId;
        dto.SupplierName = po.Supplier?.SupplierName ?? string.Empty;
        dto.SupplyCategory = po.Supplier?.SupplyCategory;
        dto.IssueDate = po.IssueDate;
        dto.ExpectedArrivalDate = po.ExpectedArrivalDate;
        dto.PoStatus = po.PoStatus.ToString();
        dto.TotalAmount = po.TotalAmount;
        dto.LinesCount = po.Details.Count;
        dto.TotalOrderedQty = Math.Round(ordered, 4);
        dto.TotalReceivedQty = Math.Round(received, 4);
        dto.ReceivedPercent = ordered > 0m ? Math.Round(received / ordered * 100m, 2) : 0m;
        dto.EtaDaysRemaining = (po.ExpectedArrivalDate.Date - today.Date).Days;
        dto.EtaStatus = ComputeEtaStatus(po.PoStatus, po.ExpectedArrivalDate, today);
    }

    private static PurchaseOrderListItemDto MapToListItem(PurchaseOrder po, DateTime today)
    {
        var dto = new PurchaseOrderListItemDto();
        FillListItem(dto, po, today);
        return dto;
    }

    private static PurchaseOrderDetailDto MapToDetail(PurchaseOrder po, DateTime today)
    {
        var detail = new PurchaseOrderDetailDto
        {
            SupplierCode = po.Supplier?.SupplierCode,
            SupplierEmail = po.Supplier?.ContactEmail,
            SupplierPhone = po.Supplier?.Phone,
            Terms = po.Terms,
            CreatedBy = po.CreatedBy,
            CreatorName = po.Creator?.FullName
        };
        FillListItem(detail, po, today);

        detail.Lines = po.Details
            .OrderBy(d => d.Material?.MaterialCode)
            .Select(d => new PurchaseOrderLineDto
            {
                PoDetailId = d.PoDetailId,
                MaterialId = d.MaterialId,
                MaterialCode = d.Material?.MaterialCode ?? string.Empty,
                MaterialName = d.Material?.MaterialName ?? string.Empty,
                UnitOfMeasure = d.Material?.UnitOfMeasure ?? string.Empty,
                OrderedQty = d.OrderedQty,
                ReceivedQty = d.ReceivedQty,
                RemainingQty = Math.Max(0m, Math.Round(d.OrderedQty - d.ReceivedQty, 4)),
                UnitPrice = d.UnitPrice,
                LineAmount = d.UnitPrice.HasValue ? Math.Round(d.OrderedQty * d.UnitPrice.Value, 2) : null
            })
            .ToList();

        detail.GoodsReceipts = po.GoodsReceipts
            .OrderByDescending(g => g.CommittedAt)
            .Select(g => new PurchaseOrderReceiptDto
            {
                GrnId = g.GrnId,
                GrnCode = g.GrnCode,
                CommittedAt = g.CommittedAt,
                WarehouseKeeperName = g.WarehouseKeeper?.FullName,
                Status = g.Status.ToString()
            })
            .ToList();

        return detail;
    }
}
