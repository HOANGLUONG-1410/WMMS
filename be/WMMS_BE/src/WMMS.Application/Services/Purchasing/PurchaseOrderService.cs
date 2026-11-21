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
    private readonly IEmailService? _emailService;

    public PurchaseOrderService(
        IPurchaseOrderRepository purchaseOrderRepository,
        IPurchaseRequestRepository purchaseRequestRepository,
        IBomRepository bomRepository,
        IUnitOfWork unitOfWork,
        INotificationHubService? notificationHubService = null,
        IEmailService? emailService = null)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _purchaseRequestRepository = purchaseRequestRepository;
        _bomRepository = bomRepository;
        _unitOfWork = unitOfWork;
        _notificationHubService = notificationHubService;
        _emailService = emailService;
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
                    "Trạng thái đơn mua hàng không hợp lệ. Vui lòng chọn một trong các trạng thái: Đã phát hành, Nhận một phần, Đã nhận đủ hoặc Đã hủy.",
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
            throw new AppException("Bộ lọc tiến độ giao hàng (ETA) không hợp lệ. Vui lòng chọn Quá hạn hoặc Sắp đến hạn.", 400, MessageCodes.Msg13ValidationError);
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
            ?? throw AppException.NotFound("Không tìm thấy phiếu yêu cầu mua hàng được chọn.");

        // BR-PUR-02: Không được phát hành PO trên PR chưa được Chủ xưởng phê duyệt
        if (pr.PrStatus is PrStatusEnum.PENDING_APPROVAL or PrStatusEnum.REJECTED)
        {
            throw AppException.Forbidden(
                $"Phiếu yêu cầu mua hàng {pr.PrCode} chưa được phê duyệt. Đơn đặt hàng chỉ được phát hành sau khi Chủ xưởng đã phê duyệt phiếu yêu cầu mua hàng.",
                MessageCodes.Msg11ForbiddenRule);
        }

        if (pr.PrStatus == PrStatusEnum.PO_ISSUED)
        {
            throw AppException.Conflict(
                $"Phiếu yêu cầu mua hàng {pr.PrCode} đã được đặt hàng đủ toàn bộ số lượng trước đó.",
                MessageCodes.Msg08DuplicateConflict);
        }

        var itemsById = pr.Items.ToDictionary(i => i.PrItemId);
        foreach (var line in request.Lines)
        {
            if (!itemsById.ContainsKey(line.PrItemId))
            {
                throw new AppException(
                    $"Mặt hàng #{line.PrItemId} không thuộc về phiếu yêu cầu mua hàng {pr.PrCode}.",
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
                throw AppException.NotFound("Không tìm thấy nhà cung cấp đã chọn trong danh mục.");
            }

            if (!string.Equals(supplier.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException(
                    $"Nhà cung cấp '{supplier.SupplierName}' ({supplier.SupplierCode}) hiện đang ngừng giao dịch nên không thể tiếp nhận đơn đặt hàng mới.",
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
                    "Ngày nhà cung cấp hẹn giao hàng không được nằm trong quá khứ.",
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
                    $"Số lượng đặt hàng ({requested:0.####}) vượt quá số lượng còn lại cần mua ({Math.Max(0m, remaining):0.####}) của mặt hàng '{item.Material?.MaterialName ?? $"#{prItemId}"}'.",
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
                    Reason = $"Phát hành đơn đặt hàng nhà cung cấp {po.PoCode} từ phiếu yêu cầu {pr.PrCode} tới {supplier.SupplierName}.",
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
                ?? throw AppException.NotFound($"Không tìm thấy đơn đặt hàng {code} sau khi khởi tạo.");
            results.Add(MapToDetail(po, today));
            await NotifyAsync(po.PoCode, po.Supplier?.SupplierName ?? string.Empty, po.ExpectedArrivalDate, isNew: true, cancellationToken);
        }

        return results;
    }

    // =========================================================================
    // [SC-16 MỞ RỘNG]: PHÁT HÀNH ĐỒNG LOẠT ĐƠN ĐẶT HÀNG TỪ NHIỀU PR ĐÃ DUYỆT (BATCH ISSUE POS)
    // =========================================================================
    /// <inheritdoc />
    public async Task<List<PurchaseOrderDetailDto>> BatchIssuePurchaseOrdersAsync(
        BatchIssuePurchaseOrdersRequestDto? request,
        long purchasingUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var results = new List<PurchaseOrderDetailDto>();

        // Trường hợp 1: Người dùng gửi danh sách dòng chi tiết từ 1 hoặc nhiều PR
        if (request?.Lines != null && request.Lines.Count > 0)
        {
            foreach (var prGroup in request.Lines.GroupBy(l => l.PrId).OrderBy(g => g.Key))
            {
                var supplierIdsInGroup = prGroup.Select(x => x.SupplierId).Distinct().ToHashSet();
                var termsForGroup = (request.SupplierTerms ?? new List<CreatePoSupplierTermsDto>())
                    .Where(t => supplierIdsInGroup.Contains(t.SupplierId))
                    .ToList();

                // Nếu thiếu điều khoản ngày hẹn giao của NCC nào thì tự động lấy LeadTimeDays của NCC đó
                var missingSupplierIds = supplierIdsInGroup.Except(termsForGroup.Select(t => t.SupplierId)).ToList();
                if (missingSupplierIds.Count > 0)
                {
                    var suppliersMap = await _purchaseOrderRepository.GetSuppliersByIdsAsync(missingSupplierIds, cancellationToken);
                    foreach (var sid in missingSupplierIds)
                    {
                        var leadDays = suppliersMap.TryGetValue(sid, out var sup) ? Math.Max(1, sup.LeadTimeDays) : 7;
                        termsForGroup.Add(new CreatePoSupplierTermsDto
                        {
                            SupplierId = sid,
                            ExpectedArrivalDate = today.AddDays(leadDays),
                            Terms = "Thanh toán chuyển khoản trong 30 ngày sau khi nghiệm thu nhập kho."
                        });
                    }
                }

                var singleRequest = new CreatePurchaseOrdersRequestDto
                {
                    PrId = prGroup.Key,
                    Lines = prGroup.Select(l => new CreatePoLineDto
                    {
                        PrItemId = l.PrItemId,
                        SupplierId = l.SupplierId,
                        OrderedQty = l.OrderedQty,
                        UnitPrice = l.UnitPrice
                    }).ToList(),
                    SupplierTerms = termsForGroup
                };

                var createdForPr = await CreatePurchaseOrdersAsync(singleRequest, purchasingUserId, ipAddress, cancellationToken);
                results.AddRange(createdForPr);
            }

            return results;
        }

        // Trường hợp 2: Bấm nút "Phát hành PO hàng loạt" trên FE (tự động gom các PR đã được Chủ xưởng duyệt còn chờ đặt hàng)
        var approvedPrs = await GetApprovedRequisitionsAsync(cancellationToken);
        if (request?.PrIds != null && request.PrIds.Count > 0)
        {
            var filterSet = request.PrIds.ToHashSet();
            approvedPrs = approvedPrs.Where(p => filterSet.Contains(p.PrId)).ToList();
        }

        if (approvedPrs.Count == 0)
        {
            throw new AppException(
                "Hiện không có phiếu yêu cầu mua hàng (PR) nào đã duyệt đang chờ phát hành đơn đặt hàng.",
                400,
                MessageCodes.Msg13ValidationError);
        }

        var activeSuppliers = await _purchaseOrderRepository.GetActiveSuppliersAsync(cancellationToken);
        if (activeSuppliers.Count == 0)
        {
            throw new AppException(
                "Hệ thống chưa có nhà cung cấp nào đang hoạt động để tự động phát hành đơn đặt hàng.",
                400,
                MessageCodes.Msg13ValidationError);
        }

        var customTermsBySupplier = (request?.SupplierTerms ?? new List<CreatePoSupplierTermsDto>())
            .GroupBy(t => t.SupplierId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var reqPr in approvedPrs)
        {
            var linesToCreate = new List<CreatePoLineDto>();
            foreach (var line in reqPr.Lines.Where(l => l.RemainingQty > QtyTolerance))
            {
                var matchedSupplier = activeSuppliers.FirstOrDefault(s =>
                                          string.Equals(s.SupplyCategory, line.MaterialGroup, StringComparison.OrdinalIgnoreCase))
                                      ?? activeSuppliers.First();

                linesToCreate.Add(new CreatePoLineDto
                {
                    PrItemId = line.PrItemId,
                    SupplierId = matchedSupplier.SupplierId,
                    OrderedQty = line.RemainingQty,
                    UnitPrice = GetDefaultUnitPriceByGroup(line.MaterialGroup)
                });
            }

            if (linesToCreate.Count == 0)
            {
                continue;
            }

            var usedSupplierIds = linesToCreate.Select(l => l.SupplierId).Distinct().ToList();
            var termsList = usedSupplierIds.Select(sid =>
            {
                if (customTermsBySupplier.TryGetValue(sid, out var customTerm) && customTerm.ExpectedArrivalDate.Date >= today)
                {
                    return customTerm;
                }

                var sup = activeSuppliers.First(s => s.SupplierId == sid);
                return new CreatePoSupplierTermsDto
                {
                    SupplierId = sid,
                    ExpectedArrivalDate = today.AddDays(Math.Max(1, sup.LeadTimeDays)),
                    Terms = "Phát hành đơn mua hàng tự động theo định mức phê duyệt; thanh toán trong 30 ngày sau nghiệm thu."
                };
            }).ToList();

            var createdForPr = await CreatePurchaseOrdersAsync(
                new CreatePurchaseOrdersRequestDto
                {
                    PrId = reqPr.PrId,
                    Lines = linesToCreate,
                    SupplierTerms = termsList
                },
                purchasingUserId,
                ipAddress,
                cancellationToken);

            results.AddRange(createdForPr);
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
        var newEta = request.EffectiveEtaDate.Date;
        if (newEta == default || newEta < today)
        {
            throw new AppException("Ngày nhà cung cấp hẹn giao hàng không được bỏ trống hoặc nằm trong quá khứ.", 400, MessageCodes.Msg13ValidationError);
        }

        var po = await GetOrThrowAsync(poId, cancellationToken);
        if (po.PoStatus is PoStatusEnum.FULFILLED or PoStatusEnum.CANCELLED)
        {
            throw AppException.Conflict(
                $"Đơn mua hàng {po.PoCode} đã hoàn tất hoặc đã hủy nên không thể thay đổi lịch giao hàng.",
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
                Reason = notes ?? $"Cập nhật ngày hẹn giao hàng của đơn mua hàng {po.PoCode} từ {oldEta:yyyy-MM-dd} sang {newEta:yyyy-MM-dd}.",
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
                Reason = request.Notes ?? $"Cập nhật điều khoản thanh toán và giao hàng của đơn mua hàng {po.PoCode}.",
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
            throw new AppException(
                $"Trạng thái đơn mua hàng '{request.Status}' không hợp lệ. Vui lòng chọn: Đã phát hành, Nhận một phần, Đã nhận đủ hoặc Đã hủy.",
                400,
                MessageCodes.Msg13ValidationError);
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
                Reason = request.Reason ?? $"Cập nhật trạng thái đơn mua hàng {po.PoCode} sang {FormatPoStatusLabel(newStatus)}.",
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
    // [SC-16 MỞ RỘNG]: GỬI EMAIL ĐƠN ĐẶT HÀNG NGUYÊN LIỆU CHO NHÀ CUNG CẤP (UC-52)
    // =========================================================================
    /// <inheritdoc />
    public async Task<SendPurchaseOrderEmailResultDto> SendPurchaseOrderEmailAsync(
        long poId,
        SendPurchaseOrderEmailDto? request,
        long purchasingUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var po = await GetOrThrowAsync(poId, cancellationToken);
        var activeSuppliers = await _purchaseOrderRepository.GetActiveSuppliersAsync(cancellationToken);

        var deliveryAddress = !string.IsNullOrWhiteSpace(request?.DeliveryAddress)
            ? request.DeliveryAddress.Trim()
            : "Kho nguyên vật liệu — Công ty TNHH Nguyên Khoa Hà Nam, KCN Châu Sơn, TP. Phủ Lý, Tỉnh Hà Nam";

        var paymentNote = !string.IsNullOrWhiteSpace(request?.PaymentMethodNote)
            ? request.PaymentMethodNote.Trim()
            : (string.IsNullOrWhiteSpace(po.Terms)
                ? "Thanh toán trước 50% giá trị hợp đồng, 50% còn lại thanh toán sau khi giao hàng."
                : po.Terms);

        var now = DateTime.UtcNow;

        // Trường hợp người dùng chọn nhà cung cấp riêng cho từng loại vật liệu trong đơn
        if (request?.MaterialAssignments != null && request.MaterialAssignments.Count > 0)
        {
            var detailById = po.Details.ToDictionary(d => d.PoDetailId);
            var validAssignments = new List<(PurchaseOrderDetail Detail, long SupplierId)>();

            foreach (var item in request.MaterialAssignments)
            {
                if (item.SupplierId <= 0)
                {
                    throw new AppException(
                        "Vui lòng chọn nhà cung cấp cho tất cả các loại vật liệu trước khi gửi.",
                        400,
                        MessageCodes.Msg13ValidationError);
                }

                if (!detailById.TryGetValue(item.PoDetailId, out var detail))
                {
                    throw new AppException(
                        $"Không tìm thấy dòng vật tư #{item.PoDetailId} trong đơn đặt hàng {po.PoCode}.",
                        400,
                        MessageCodes.Msg13ValidationError);
                }

                validAssignments.Add((detail, item.SupplierId));
            }

            if (validAssignments.Count == 0)
            {
                throw new AppException(
                    "Vui lòng chọn nhà cung cấp cho từng loại vật liệu trước khi gửi.",
                    400,
                    MessageCodes.Msg13ValidationError);
            }

            var requestedSupplierIds = validAssignments.Select(x => x.SupplierId).Distinct().ToList();
            var suppliersById = await _purchaseOrderRepository.GetSuppliersByIdsAsync(requestedSupplierIds, cancellationToken);

            var anySentViaSmtp = false;
            var dispatchedSuppliers = new List<string>();
            var dispatchedEmails = new List<string>();
            var firstHtmlDoc = string.Empty;
            long firstSupplierId = 0;

            foreach (var group in validAssignments.GroupBy(x => x.SupplierId))
            {
                var supplier = activeSuppliers.FirstOrDefault(s => s.SupplierId == group.Key)
                    ?? (suppliersById.TryGetValue(group.Key, out var loadedSup) ? loadedSup : null);

                if (supplier == null)
                {
                    throw new AppException(
                        $"Không tìm thấy nhà cung cấp với mã ID = {group.Key}.",
                        400,
                        MessageCodes.Msg13ValidationError);
                }

                var targetEmail = supplier.ContactEmail?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(targetEmail))
                {
                    throw new AppException(
                        $"Nhà cung cấp '{supplier.SupplierName}' chưa có địa chỉ email liên hệ.",
                        400,
                        MessageCodes.Msg13ValidationError);
                }

                var supplierDetails = group.Select(x => x.Detail).OrderBy(d => d.PoDetailId).ToList();
                var htmlDoc = BuildPurchaseOrderHtmlDocument(po, supplier, supplierDetails, deliveryAddress, paymentNote);
                if (string.IsNullOrEmpty(firstHtmlDoc))
                {
                    firstHtmlDoc = htmlDoc;
                    firstSupplierId = supplier.SupplierId;
                }

                if (_emailService != null)
                {
                    var sent = await _emailService.SendPurchaseOrderEmailAsync(
                        targetEmail,
                        supplier.SupplierName,
                        po.PoCode,
                        htmlDoc,
                        cancellationToken);
                    anySentViaSmtp = anySentViaSmtp || sent;
                }

                dispatchedSuppliers.Add(supplier.SupplierName);
                dispatchedEmails.Add(targetEmail);
            }

            await _bomRepository.AddAuditLogAsync(new AuditLog
            {
                UserId = purchasingUserId > 0 ? purchasingUserId : null,
                ActionType = "PURCHASE_ORDER_UPDATED",
                EntityName = nameof(PurchaseOrder),
                EntityId = po.PoCode,
                BeforeState = JsonSerializer.Serialize(new { po.SupplierId, CurrentSupplier = po.Supplier?.SupplierName }),
                AfterState = JsonSerializer.Serialize(new
                {
                    DispatchedSuppliers = dispatchedSuppliers,
                    DispatchedEmails = dispatchedEmails,
                    SentViaSmtp = anySentViaSmtp
                }),
                Reason = $"Gửi email đơn đặt hàng nguyên vật liệu {po.PoCode} theo từng loại vật liệu tới {dispatchedSuppliers.Count} nhà cung cấp: {string.Join(", ", dispatchedSuppliers)}.",
                IpAddress = ipAddress,
                CreatedAt = now
            });
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new SendPurchaseOrderEmailResultDto
            {
                PoId = po.PoId,
                PoCode = po.PoCode,
                SupplierId = firstSupplierId,
                SupplierName = string.Join(", ", dispatchedSuppliers),
                RecipientEmail = string.Join(", ", dispatchedEmails),
                DispatchedSupplierCount = dispatchedSuppliers.Count,
                SentViaSmtp = anySentViaSmtp,
                SentAt = now,
                PreviewHtml = firstHtmlDoc
            };
        }

        Supplier? targetSupplier = null;
        if (request?.SupplierId is > 0)
        {
            targetSupplier = activeSuppliers.FirstOrDefault(s => s.SupplierId == request.SupplierId.Value);
            if (targetSupplier == null)
            {
                var map = await _purchaseOrderRepository.GetSuppliersByIdsAsync(new[] { request.SupplierId.Value }, cancellationToken);
                map.TryGetValue(request.SupplierId.Value, out targetSupplier);
            }
        }

        if (targetSupplier == null)
        {
            throw new AppException(
                "Vui lòng chọn nhà cung cấp trước khi gửi đơn đặt hàng.",
                400,
                MessageCodes.Msg13ValidationError);
        }

        var recipientEmail = !string.IsNullOrWhiteSpace(request?.RecipientEmail)
            ? request.RecipientEmail.Trim()
            : (targetSupplier.ContactEmail?.Trim() ?? string.Empty);

        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            throw new AppException(
                $"Nhà cung cấp '{targetSupplier.SupplierName}' chưa có địa chỉ email liên hệ. Vui lòng chọn nhà cung cấp khác.",
                400,
                MessageCodes.Msg13ValidationError);
        }

        var singleHtmlDoc = BuildPurchaseOrderHtmlDocument(
            po,
            targetSupplier,
            po.Details.OrderBy(d => d.PoDetailId).ToList(),
            deliveryAddress,
            paymentNote);

        var sentViaSmtp = false;
        if (_emailService != null)
        {
            sentViaSmtp = await _emailService.SendPurchaseOrderEmailAsync(
                recipientEmail,
                targetSupplier.SupplierName,
                po.PoCode,
                singleHtmlDoc,
                cancellationToken);
        }

        await _bomRepository.AddAuditLogAsync(new AuditLog
        {
            UserId = purchasingUserId > 0 ? purchasingUserId : null,
            ActionType = "PURCHASE_ORDER_UPDATED",
            EntityName = nameof(PurchaseOrder),
            EntityId = po.PoCode,
            BeforeState = JsonSerializer.Serialize(new { po.SupplierId, CurrentSupplier = po.Supplier?.SupplierName }),
            AfterState = JsonSerializer.Serialize(new
            {
                TargetSupplierId = targetSupplier.SupplierId,
                TargetSupplierName = targetSupplier.SupplierName,
                RecipientEmail = recipientEmail,
                SentViaSmtp = sentViaSmtp
            }),
            Reason = $"Gửi email đơn đặt hàng nguyên vật liệu {po.PoCode} tới nhà cung cấp {targetSupplier.SupplierName} ({recipientEmail}).",
            IpAddress = ipAddress,
            CreatedAt = now
        });
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SendPurchaseOrderEmailResultDto
        {
            PoId = po.PoId,
            PoCode = po.PoCode,
            SupplierId = targetSupplier.SupplierId,
            SupplierName = targetSupplier.SupplierName,
            RecipientEmail = recipientEmail,
            DispatchedSupplierCount = 1,
            SentViaSmtp = sentViaSmtp,
            SentAt = now,
            PreviewHtml = singleHtmlDoc
        };
    }

    /// <summary>
    /// Dựng biểu mẫu HTML "ĐƠN ĐẶT HÀNG" theo chuẩn văn bản hành chính Việt Nam (khớp mẫu Quốc hiệu, Tiêu ngữ, Bảng 6 cột STT - Tên mặt hàng - ĐVT - Số lượng - Đơn giá - Thành tiền, Tổng cộng, Thời gian/Địa điểm giao hàng, Phương thức thanh toán và chữ ký Giám đốc).
    /// </summary>
    private static string BuildPurchaseOrderHtmlDocument(
        PurchaseOrder po,
        Supplier supplier,
        IReadOnlyList<PurchaseOrderDetail> details,
        string deliveryAddress,
        string paymentNote)
    {
        var now = DateTime.Now;
        var rowsSb = new System.Text.StringBuilder();
        decimal grandTotal = 0m;

        for (var i = 0; i < details.Count; i++)
        {
            var d = details[i];
            var unitPrice = d.UnitPrice ?? GetDefaultUnitPriceByGroup(d.Material?.MaterialGroup.ToString());
            var lineTotal = Math.Round(d.OrderedQty * unitPrice, 0);
            grandTotal += lineTotal;

            var matName = d.Material != null
                ? $"{System.Net.WebUtility.HtmlEncode(d.Material.MaterialName)} ({System.Net.WebUtility.HtmlEncode(d.Material.MaterialCode)})"
                : $"Nguyên vật liệu #{d.MaterialId}";
            var uom = System.Net.WebUtility.HtmlEncode(d.Material?.UnitOfMeasure ?? "Đơn vị");

            rowsSb.Append($"""
                <tr>
                    <td style="border:1px solid #333;padding:8px;text-align:center;">{i + 1}</td>
                    <td style="border:1px solid #333;padding:8px;">{matName}</td>
                    <td style="border:1px solid #333;padding:8px;text-align:center;">{uom}</td>
                    <td style="border:1px solid #333;padding:8px;text-align:right;">{d.OrderedQty:0.##}</td>
                    <td style="border:1px solid #333;padding:8px;text-align:right;">{unitPrice:N0} đ</td>
                    <td style="border:1px solid #333;padding:8px;text-align:right;font-weight:bold;">{lineTotal:N0} đ</td>
                </tr>
                """);
        }

        var encodedSupplier = System.Net.WebUtility.HtmlEncode(supplier.SupplierName);
        var encodedPoCode = System.Net.WebUtility.HtmlEncode(po.PoCode);
        var encodedAddress = System.Net.WebUtility.HtmlEncode(deliveryAddress);
        var encodedPayment = System.Net.WebUtility.HtmlEncode(paymentNote);

        return $"""
            <div style="font-family:'Times New Roman',Times,serif;max-width:760px;margin:0 auto;padding:28px 36px;color:#111;background:#fff;line-height:1.55;font-size:15px;border:1px solid #d1d5db;">
                <div style="text-align:center;margin-bottom:18px;">
                    <div style="font-weight:bold;font-size:16px;text-transform:uppercase;">CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</div>
                    <div style="font-weight:bold;font-size:15px;text-decoration:underline;margin-top:2px;">Độc lập – Tự do – Hạnh phúc</div>
                </div>

                <div style="text-align:center;margin:22px 0 18px 0;">
                    <div style="font-weight:bold;font-size:20px;text-transform:uppercase;">ĐƠN ĐẶT HÀNG</div>
                    <div style="font-style:italic;font-size:14.5px;margin-top:4px;">Số: <strong>{encodedPoCode}</strong></div>
                </div>

                <p style="margin:8px 0;"><strong>Kính gửi:</strong> <strong>{encodedSupplier}</strong></p>
                <p style="margin:8px 0;"><strong>Công ty TNHH Nguyên Khoa Hà Nam</strong> có nhu cầu đặt hàng tại Quý công ty theo mẫu yêu cầu.</p>
                <p style="margin:8px 0 12px 0;">Nội dung đặt hàng như sau:</p>

                <table style="width:100%;border-collapse:collapse;margin:10px 0 16px 0;font-size:14.5px;">
                    <thead>
                        <tr style="background:#f3f4f6;font-weight:bold;text-align:center;">
                            <th style="border:1px solid #333;padding:8px;width:48px;">STT</th>
                            <th style="border:1px solid #333;padding:8px;">Tên mặt hàng</th>
                            <th style="border:1px solid #333;padding:8px;width:72px;">ĐVT</th>
                            <th style="border:1px solid #333;padding:8px;width:85px;">Số lượng</th>
                            <th style="border:1px solid #333;padding:8px;width:120px;">Đơn giá</th>
                            <th style="border:1px solid #333;padding:8px;width:135px;">Thành tiền</th>
                        </tr>
                    </thead>
                    <tbody>
                        {rowsSb}
                    </tbody>
                </table>

                <p style="margin:12px 0;font-size:15.5px;"><strong>Tổng cộng: {grandTotal:N0} VNĐ</strong></p>
                <p style="margin:8px 0;"><strong>Thời gian giao hàng:</strong> {po.ExpectedArrivalDate:dd/MM/yyyy}</p>
                <p style="margin:8px 0;"><strong>Địa điểm giao hàng:</strong> {encodedAddress}</p>
                <div style="margin:8px 0;">
                    <strong>Phương thức thanh toán:</strong>
                    <div style="margin-left:12px;margin-top:4px;">- Thanh toán bằng tiền mặt hoặc chuyển khoản</div>
                    <div style="margin-left:12px;margin-top:2px;">- {encodedPayment}</div>
                </div>

                <div style="display:flex;justify-content:flex-end;margin-top:26px;">
                    <div style="text-align:center;min-width:250px;">
                        <div style="font-style:italic;">Hà Nam, ngày {now:dd} tháng {now:MM} năm {now:yyyy}</div>
                        <div style="font-weight:bold;margin-top:6px;text-transform:uppercase;">GIÁM ĐỐC</div>
                        <div style="margin-top:48px;font-weight:bold;">Công ty TNHH Nguyên Khoa Hà Nam</div>
                    </div>
                </div>
            </div>
            """;
    }

    // =========================================================================
    // PRIVATE HELPER METHODS
    // =========================================================================

    private async Task<PurchaseOrder> GetOrThrowAsync(long poId, CancellationToken cancellationToken)
    {
        return await _purchaseOrderRepository.GetByIdAsync(poId, cancellationToken)
            ?? throw AppException.NotFound("Không tìm thấy đơn đặt hàng được yêu cầu.");
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

        dto.MaterialCategory = FormatMaterialCategory(po.Supplier?.SupplyCategory);
        dto.FulfillmentStatus = FormatPoStatusLabel(po.PoStatus);
        dto.EtaDaysLabel = po.PoStatus is PoStatusEnum.FULFILLED or PoStatusEnum.CANCELLED
            ? null
            : (dto.EtaDaysRemaining < 0
                ? $"Trễ {Math.Abs(dto.EtaDaysRemaining)} ngày"
                : (dto.EtaDaysRemaining == 0 ? "Giao hôm nay" : $"{dto.EtaDaysRemaining} ngày"));

        var detailList = po.Details.OrderBy(d => d.PoDetailId).ToList();
        if (detailList.Count == 0)
        {
            dto.OrderItemsSummary = "Chưa có chi tiết mặt hàng";
        }
        else if (detailList.Count == 1)
        {
            var first = detailList[0];
            dto.OrderItemsSummary = $"{first.OrderedQty:0.##} {first.Material?.UnitOfMeasure ?? "đơn vị"} {first.Material?.MaterialName ?? "vật tư"}";
        }
        else
        {
            var first = detailList[0];
            dto.OrderItemsSummary = $"{first.OrderedQty:0.##} {first.Material?.UnitOfMeasure ?? "đơn vị"} {first.Material?.MaterialName ?? "vật tư"} (+{detailList.Count - 1} mặt hàng khác)";
        }
    }

    private static string FormatMaterialCategory(string? category) =>
        (category?.Trim().ToUpperInvariant()) switch
        {
            "LUMBER" => "Gỗ nguyên khối",
            "HARDWARE" => "Phụ kiện kim khí",
            "FINISH_PAINT" => "Sơn & Hóa chất hoàn thiện",
            "PACKAGING" => "Bao bì đóng gói xuất khẩu",
            _ => string.IsNullOrWhiteSpace(category) ? "Vật tư sản xuất" : category
        };

    private static string FormatPoStatusLabel(PoStatusEnum status) =>
        status switch
        {
            PoStatusEnum.ISSUED => "Đã phát hành / Đang giao",
            PoStatusEnum.PARTIALLY_RECEIVED => "Đã nhận một phần",
            PoStatusEnum.FULFILLED => "Đã nhận đủ hàng",
            PoStatusEnum.CANCELLED => "Đã hủy",
            _ => status.ToString()
        };

    private static decimal GetDefaultUnitPriceByGroup(string? materialGroup) =>
        (materialGroup?.Trim().ToUpperInvariant()) switch
        {
            "LUMBER" => 12500000m,
            "FINISH_PAINT" => 185000m,
            "PACKAGING" => 45000m,
            _ => 15000m
        };

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
