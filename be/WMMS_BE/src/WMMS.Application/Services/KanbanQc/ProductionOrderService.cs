using System.Text.Json;
using System.Text.RegularExpressions;
using WMMS.Application.DTOs.KanbanQc;
using WMMS.Application.Interfaces.External;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;
using WMMS.Domain.Entities.CatalogBom;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Entities.KanbanQc;
using WMMS.Domain.Enums;
using WMMS.Domain.Exceptions;

namespace WMMS.Application.Services.KanbanQc;

/// <summary>
/// Triển khai Dịch vụ Điều độ &amp; Phát hành Lệnh sản xuất, Tách lô thông minh (Phân hệ 8 - SC-18, SC-19, UC-62).
/// Thực thi trọn vẹn các quy tắc:
/// - BR-005 &amp; UC-62: Tách nhỏ đơn hàng thành các thẻ Kanban Lots theo công suất trạm hoặc phân bổ tùy chỉnh.
/// - BR-008: Chốt chặn đầu vào vật tư (Cards tạo ra ở trạng thái LOCKED cho đến khi xuất kho cấp phát vật tư ISSUED_TO_SHOP).
/// - QT-06 &amp; KBN02: Cơ chế Overdrive giải trình kỹ thuật (cho phép sơ chế gỗ thô trước trạm dừng Hold Point khi thiếu phụ kiện ốc vít/sơn).
/// - GBR-05: Ghi nhật ký kiểm toán bất biến (audit_logs) và đẩy sự kiện SignalR thời gian thực lên màn hình phân xưởng.
/// </summary>
public class ProductionOrderService : IProductionOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductionBatchRepository _batchRepository;
    private readonly IOrderItemRepository _orderItemRepository;
    private readonly IRoutingRepository _routingRepository;
    private readonly IBomRepository _bomRepository;
    private readonly INotificationHubService? _notificationHubService;

    public ProductionOrderService(
        IUnitOfWork unitOfWork,
        IProductionBatchRepository batchRepository,
        IOrderItemRepository orderItemRepository,
        IRoutingRepository routingRepository,
        IBomRepository bomRepository,
        INotificationHubService? notificationHubService = null)
    {
        _unitOfWork = unitOfWork;
        _batchRepository = batchRepository;
        _orderItemRepository = orderItemRepository;
        _routingRepository = routingRepository;
        _bomRepository = bomRepository;
        _notificationHubService = notificationHubService;
    }

    // =========================================================================
    // [SC-19 CORE]: PHÂN ĐOẠN 1 - THUẬT TOÁN TÁCH LÔ & PHÁT HÀNH LỆNH SẢN XUẤT (UC-62, BR-005)
    // Tách đơn hàng thành các thẻ Kanban Lots theo công suất trạm máy hoặc số lượng tùy chỉnh.
    // Kiểm tra chốt chặn 100% vật tư đầu vào (BR-008) hoặc kích hoạt cơ chế chạy trước Overdrive (QT-06, KBN02).
    // =========================================================================
    public async Task<ProductionOrderSummaryDto> GenerateProductionOrderAndSplitLotsAsync(
        GenerateBatchesRequestDto request,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        // [SC-19 CORE]: Bước 0 - Tải đồ thị dữ liệu dòng đơn hàng kèm thông tin MRP, BOM và vật tư
        var orderItem = await _orderItemRepository.GetByIdWithFullMrpGraphAsync(request.OrderItemId, cancellationToken);
        if (orderItem == null)
        {
            throw new NotFoundException($"Không tìm thấy dòng sản phẩm đơn hàng với ID #{request.OrderItemId}.");
        }

        var product = orderItem.Product;
        if (product == null)
        {
            throw new DomainException($"Không tìm thấy thông tin sản phẩm cho dòng đơn hàng #{request.OrderItemId}.", "PRODUCT_NOT_FOUND");
        }

        var customerOrder = orderItem.CustomerOrder;
        var poNumber = customerOrder?.PoNumber ?? $"PO-{orderItem.OrderId}";
        var spaceKey = customerOrder?.OrderSpace?.SpaceKey ?? $"SPACE-PO-{orderItem.OrderId}";

        // [SC-19 CORE]: Bước 1 - Kiểm tra điều kiện tiên quyết về BOM (PRE-1, BR-001)
        // Lệnh sản xuất yêu cầu BOM phải được bóc tách và phê duyệt trước (READY_FOR_PRODUCTION / APPROVED)
        var bomHeader = orderItem.BomHeader;
        if (bomHeader == null)
        {
            throw new DomainException(
                $"Sản phẩm '{product.ProductName}' (SKU: {product.ProductSku}) chưa có cấu hình định mức BOM. Vui lòng bóc tách BOM trước khi phát hành lệnh sản xuất.",
                "BOM_NOT_CONFIGURED");
        }

        bool isBomReady = bomHeader.BomStatus == BomStatusEnum.READY_FOR_PRODUCTION ||
                          bomHeader.BomStatus == BomStatusEnum.APPROVED;

        if (!isBomReady && !request.AllowOverdrive)
        {
            throw new DomainException(
                $"BOM của sản phẩm '{product.ProductName}' đang ở trạng thái {bomHeader.BomStatus}. Lệnh sản xuất yêu cầu BOM phải được phê duyệt trước (READY_FOR_PRODUCTION) hoặc kích hoạt chế độ Overdrive giải trình.",
                "BOM_NOT_APPROVED");
        }

        // [SC-19 CORE]: Bước 2 - Tính toán số lượng tồn chưa phát hành (Remaining Unreleased Quantity)
        // Đảm bảo không phát hành vượt quá tổng số lượng đã đặt của đơn hàng
        var existingBatches = await _batchRepository.GetByOrderItemIdAsync(request.OrderItemId, cancellationToken);
        int alreadyReleasedQty = existingBatches
            .Where(b => b.BatchType == BatchTypeEnum.MAIN_CARD)
            .Sum(b => b.BatchQty);

        int remainingQty = orderItem.OrderedQty - alreadyReleasedQty;
        if (remainingQty <= 0)
        {
            throw new DomainException(
                $"Sản phẩm #{request.OrderItemId} ({product.ProductSku}) đã được phát hành hết toàn bộ số lượng ({orderItem.OrderedQty} SP). Không còn số lượng chưa cấp phát.",
                "ALL_QTY_ALREADY_RELEASED");
        }

        // [SC-19 CORE]: Bước 3 - Thuật toán tách lô thông minh theo công suất trạm máy (BR-005, UC-62)
        // Ưu tiên chia theo công suất trạm máy (stationCapacity, mặc định 100 SP/lô); nếu có cấu hình chia tùy chỉnh thì kiểm tra tổng số lượng
        List<int> splits;
        int stationCapacity = request.MaxBatchSize.GetValueOrDefault(100);
        if (stationCapacity <= 0) stationCapacity = 100;

        if (request.CustomBatchSplits != null && request.CustomBatchSplits.Count > 0)
        {
            int sumSplits = request.CustomBatchSplits.Sum();
            if (sumSplits > remainingQty)
            {
                throw new DomainException(
                    $"Tổng số lượng trong các lô tùy chỉnh ({sumSplits} SP) vượt quá số lượng còn lại cần phát hành ({remainingQty} SP).",
                    "INVALID_SPLIT_QTY");
            }
            splits = request.CustomBatchSplits;
        }
        else
        {
            splits = new List<int>();
            int temp = remainingQty;
            while (temp > 0)
            {
                int batchSize = Math.Min(temp, stationCapacity);
                splits.Add(batchSize);
                temp -= batchSize;
            }
        }

        // [SC-19 CORE]: Bước 4 - Lấy quy trình công nghệ động & Thiết lập điểm dừng kỹ thuật Hold Point (SC-18, SC-20)
        var routing = await _routingRepository.GetActiveRoutingByProductIdAsync(product.ProductId);
        var routingStages = (routing?.RoutingStages != null && routing.RoutingStages.Any())
            ? routing.RoutingStages.OrderBy(s => s.StageSequence).ToList()
            : GetStandardWoodworkingStages();

        var holdPointStage = routingStages.FirstOrDefault(s => s.IsHoldPoint);
        int? holdPointSeq = holdPointStage?.StageSequence ?? (routingStages.Count >= 5 ? 5 : null);

        // [BR-008 & QT-06]: Bước 5 - Đánh giá chốt chặn 100% vật tư đã xuất kho & Cơ chế Overdrive giải trình kỹ thuật
        // - Đủ 100% vật tư đã xuất kho (ISSUED_TO_SHOP / RELEASED): thẻ lô sinh ra ở trạng thái IN_PROGRESS sẵn sàng gia công.
        // - Chưa đủ vật tư nhưng có giải trình Overdrive: thẻ sinh ra IN_PROGRESS, được phép sơ chế trước điểm dừng Hold Point.
        // - Chưa đủ vật tư và không Overdrive: thẻ lô bị KHÓA CỨNG (LOCKED - BR-008), chờ Thủ kho xuất vật tư mới mở khóa.
        bool isIssuedToShop = orderItem.MaterialAllocations.Any() &&
                              orderItem.MaterialAllocations.All(a =>
                                  a.AllocationStatus == AllocationStatusEnum.ISSUED_TO_SHOP ||
                                  a.AllocationStatus == AllocationStatusEnum.RELEASED);

        bool isOverdriver = false;
        bool goodsIssuedUnlocked = false;
        BatchStatusEnum initialBatchStatus;

        if (isIssuedToShop)
        {
            isOverdriver = false;
            goodsIssuedUnlocked = true;
            initialBatchStatus = BatchStatusEnum.IN_PROGRESS;
        }
        else
        {
            if (request.AllowOverdrive)
            {
                if (string.IsNullOrWhiteSpace(request.Justification))
                {
                    throw new DomainException(
                        "Cơ chế chạy trước Overdrive bắt buộc phải cung cấp lý do giải trình kỹ thuật (QT-06, KBN02).",
                        "OVERDRIVE_JUSTIFICATION_REQUIRED");
                }
                isOverdriver = true;
                goodsIssuedUnlocked = false;
                initialBatchStatus = BatchStatusEnum.IN_PROGRESS; // Cho phép thực thi các công đoạn trước Hold Point
            }
            else
            {
                isOverdriver = false;
                goodsIssuedUnlocked = false;
                initialBatchStatus = BatchStatusEnum.LOCKED; // Chốt chặn đầu vào BR-008
            }
        }

        // [SC-19 CORE]: Bước 6 - Giao dịch nguyên tử (ACID Transaction) sinh các thẻ Kanban Lot & Danh sách công đoạn Routing Steps
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            string poClean = Regex.Replace(poNumber, @"[^A-Za-z0-9]", "");
            var createdBatches = new List<ProductionBatch>();
            int baseBatchIndex = existingBatches.Count;

            for (int i = 0; i < splits.Count; i++)
            {
                int batchIndex = baseBatchIndex + i + 1;
                string batchCode = $"LOT-{poClean}-{orderItem.OrderItemId:D2}-{batchIndex:D2}";

                var batch = new ProductionBatch
                {
                    BatchCode = batchCode,
                    OrderItemId = orderItem.OrderItemId,
                    BatchType = BatchTypeEnum.MAIN_CARD,
                    BatchQty = splits[i],
                    CurrentStageSeq = 1,
                    IsOverdriver = isOverdriver,
                    HoldPointStageSeq = holdPointSeq,
                    GoodsIssuedUnlocked = goodsIssuedUnlocked,
                    BatchStatus = initialBatchStatus,
                    CreatedAt = DateTime.UtcNow
                };

                await _batchRepository.AddAsync(batch, cancellationToken);

                var steps = routingStages.Select(stg => new BatchRoutingStep
                {
                    ProductionBatch = batch,
                    StageSequence = stg.StageSequence,
                    StageName = stg.StageName,
                    IsHoldPoint = stg.IsHoldPoint,
                    TargetQty = splits[i],
                    PassedQty = 0,
                    DefectQty = 0,
                    StepStatus = (stg.StageSequence == 1 && initialBatchStatus == BatchStatusEnum.IN_PROGRESS)
                        ? StepStatusEnum.IN_PROGRESS
                        : StepStatusEnum.WAITING
                }).ToList();

                await _batchRepository.AddStepsRangeAsync(steps, cancellationToken);

                if (isOverdriver && holdPointSeq.HasValue)
                {
                    batch.OverdriveLogs.Add(new OverdriveLog
                    {
                        ProductionBatch = batch,
                        RequestedBy = currentUserId,
                        ApprovedBy = currentUserId,
                        Justification = request.Justification?.Trim() ?? "Overdrive requested due to delivery deadline risk.",
                        HoldPointStageSeq = holdPointSeq.Value,
                        ApprovedAt = DateTime.UtcNow
                    });
                }

                createdBatches.Add(batch);
            }

            // 7. Cập nhật trạng thái dòng đơn hàng và không gian đơn hàng sang IN_PRODUCTION
            orderItem.Status = OrderItemStatusEnum.IN_PROD;
            await _orderItemRepository.UpdateAsync(orderItem);

            if (customerOrder != null && customerOrder.Status != OrderStatusEnum.IN_PRODUCTION)
            {
                customerOrder.Status = OrderStatusEnum.IN_PRODUCTION;
                await _orderItemRepository.UpdateOrderAsync(customerOrder);
            }

            if (customerOrder?.OrderSpace != null && customerOrder.OrderSpace.Status != SpaceStatusEnum.ACTIVE)
            {
                customerOrder.OrderSpace.Status = SpaceStatusEnum.ACTIVE;
                await _orderItemRepository.UpdateOrderSpaceAsync(customerOrder.OrderSpace);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 8. Ghi nhật ký kiểm toán bất biến GBR-05
            var auditPayload = JsonSerializer.Serialize(new
            {
                OrderItemId = orderItem.OrderItemId,
                ProductSku = product.ProductSku,
                TotalReleasedQty = splits.Sum(),
                BatchesCount = createdBatches.Count,
                Batches = createdBatches.Select(b => new { b.BatchId, b.BatchCode, b.BatchQty, b.BatchStatus }),
                IsOverdriver = isOverdriver,
                HoldPointStageSeq = holdPointSeq,
                Justification = request.Justification
            });

            await _bomRepository.AddAuditLogAsync(new AuditLog
            {
                UserId = currentUserId,
                ActionType = AuditActionConstants.ProductionOrderReleased,
                EntityName = "ProductionBatch",
                EntityId = orderItem.OrderItemId.ToString(),
                BeforeState = null,
                AfterState = auditPayload,
                Reason = $"Released production order and split {splits.Count} lots ({splits.Sum()} units) for {product.ProductSku} (Overdrive: {isOverdriver}) (SC-19, UC-62).",
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync();

            // 9. Phát thông báo SignalR thời gian thực tới toàn xưởng
            if (_notificationHubService != null)
            {
                await _notificationHubService.NotifyProductionOrderReleasedAsync(
                    orderItem.OrderItemId,
                    poNumber,
                    product.ProductSku,
                    createdBatches.Count,
                    splits.Sum(),
                    isOverdriver,
                    cancellationToken);
            }

            var batchDtos = createdBatches.Select(b => MapBatchToDto(b, routingStages)).ToList();

            return new ProductionOrderSummaryDto
            {
                OrderId = orderItem.OrderId,
                PoNumber = poNumber,
                SpaceKey = spaceKey,
                OrderItemId = orderItem.OrderItemId,
                ProductSku = product.ProductSku,
                ProductName = product.ProductName,
                TotalOrderedQty = orderItem.OrderedQty,
                ReleasedQty = splits.Sum(),
                TotalBatchesCount = createdBatches.Count,
                StationCapacity = stationCapacity,
                IsOverdriver = isOverdriver,
                HoldPointStageSeq = holdPointSeq,
                GoodsIssuedUnlocked = goodsIssuedUnlocked,
                Message = isOverdriver
                    ? $"Đã phát hành Lệnh sản xuất {poNumber} thành công ({createdBatches.Count} lô, {splits.Sum()} SP) ở chế độ Overdrive. Thẻ lô được phép sơ chế trước điểm dừng Hold Point công đoạn {holdPointSeq}."
                    : (goodsIssuedUnlocked
                        ? $"Đã phát hành Lệnh sản xuất {poNumber} thành công ({createdBatches.Count} lô, {splits.Sum()} SP). Thẻ lô ở trạng thái IN_PROGRESS sẵn sàng gia công."
                        : $"Đã tạo {createdBatches.Count} thẻ lô Kanban ({splits.Sum()} SP) ở trạng thái LOCKED (Chốt chặn đầu vào BR-008). Thẻ sẽ tự động mở khóa khi Thủ kho bấm xuất kho cấp phát vật tư."),
                Batches = batchDtos
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<List<ProductionBatchDto>> GetBatchesByOrderItemIdAsync(long orderItemId, CancellationToken cancellationToken = default)
    {
        var batches = await _batchRepository.GetByOrderItemIdAsync(orderItemId, cancellationToken);
        return batches.Select(b => MapBatchToDto(b)).ToList();
    }

    public async Task<List<ProductionBatchDto>> GetBatchesByOrderIdAsync(long orderId, CancellationToken cancellationToken = default)
    {
        var batches = await _batchRepository.GetByOrderIdAsync(orderId, cancellationToken);
        return batches.Select(b => MapBatchToDto(b)).ToList();
    }

    public async Task<List<ProductionBatchDto>> GetBatchesBySpaceKeyAsync(string spaceKey, CancellationToken cancellationToken = default)
    {
        var batches = await _batchRepository.GetBySpaceKeyAsync(spaceKey, cancellationToken);
        return batches.Select(b => MapBatchToDto(b)).ToList();
    }

    public async Task<ProductionBatchDto?> GetBatchByIdAsync(long batchId, CancellationToken cancellationToken = default)
    {
        var batch = await _batchRepository.GetByIdAsync(batchId, cancellationToken);
        return batch == null ? null : MapBatchToDto(batch);
    }

    public List<StandardWorkstationDto> GetStandardWorkstations()
    {
        return new List<StandardWorkstationDto>
        {
            new()
            {
                WorkstationCode = "WC-01-SAW",
                WorkstationName = "Trạm Pha phôi & Xẻ gỗ thô (Rough Mill)",
                DefaultStageSequence = 1,
                StandardCapacityPerHour = 120,
                Description = "Máy cưa bàn trượt, máy cưa nhiều lưỡi định hình phôi gỗ xẻ thanh và ván công nghiệp MDF/Plywood."
            },
            new()
            {
                WorkstationCode = "WC-02-SAND",
                WorkstationName = "Trạm Bào 4 mặt & Chà nhám phẳng mịn (Planing & Sanding)",
                DefaultStageSequence = 3,
                StandardCapacityPerHour = 100,
                Description = "Máy bào 4 mặt cao tốc, máy chà nhám thùng tạo độ phẳng mịn bề mặt đạt độ nhám Ra <= 3.2µm."
            },
            new()
            {
                WorkstationCode = "WC-03-CNC",
                WorkstationName = "Trạm Khoan chốt & Phay mộng âm dương CNC (CNC Tenoning & Boring)",
                DefaultStageSequence = 4,
                StandardCapacityPerHour = 80,
                Description = "Gia công liên kết chốt gỗ, khoan lỗ cam chốt, phay mộng âm dương chính xác theo bản vẽ CAD."
            },
            new()
            {
                WorkstationCode = "WC-04-ASSY",
                WorkstationName = "Trạm Lắp ráp kết cấu & Vào mộng dán keo ép vam (Assembly & Clamping)",
                DefaultStageSequence = 5,
                StandardCapacityPerHour = 60,
                Description = "Lắp ráp khung bàn ghế, bơm keo dán mộng chuyên dụng và ép vam giữ lực cố định. Điểm dừng kỹ thuật (Hold Point) bắt buộc."
            },
            new()
            {
                WorkstationCode = "WC-05-PAINT",
                WorkstationName = "Trạm Phun sơn lót PU & Sơn phủ hoàn thiện (Finishing & Painting)",
                DefaultStageSequence = 6,
                StandardCapacityPerHour = 75,
                Description = "Phun sơn lót PU 2 thành phần, sấy khô và phủ bóng/mờ bảo vệ bề mặt chống trầy xước ẩm mốc."
            },
            new()
            {
                WorkstationCode = "WC-06-PACK",
                WorkstationName = "Trạm KCS xuất xưởng & Đóng thùng Carton (KCS & Packing)",
                DefaultStageSequence = 7,
                StandardCapacityPerHour = 150,
                Description = "Kiểm tra chất lượng xuất xưởng 100%, dán nhãn tem kiểm định KCS, bọc xốp màng PE và đóng thùng carton 5 lớp."
            }
        };
    }

    public List<StandardQualityGateDto> GetStandardQualityGates()
    {
        return new List<StandardQualityGateDto>
        {
            new()
            {
                QualityGateCode = "QC-GATE-01",
                QualityGateName = "Cổng kiểm soát độ ẩm & Kích thước phôi (Rough Mill Gate)",
                StageSequence = 1,
                IsHoldPoint = false,
                InspectionType = "In-Process Self-Check",
                InspectionCriteria = "Độ ẩm gỗ tự nhiên < 12%, kích thước phôi sai lệch chiều dài/dày không quá ±1.0mm."
            },
            new()
            {
                QualityGateCode = "QC-GATE-02",
                QualityGateName = "Cổng kiểm soát độ nhẵn & Độ phẳng bề mặt (Sanding Gate)",
                StageSequence = 3,
                IsHoldPoint = false,
                InspectionType = "In-Process Self-Check",
                InspectionCriteria = "Bề mặt gỗ phẳng đều, không vết lẹm dao bào, độ nhám Ra <= 3.2µm."
            },
            new()
            {
                QualityGateCode = "QC-GATE-03",
                QualityGateName = "Cổng kiểm soát độ khít mộng & Tim lỗ khoan (CNC Joinery Gate)",
                StageSequence = 4,
                IsHoldPoint = false,
                InspectionType = "In-Process Self-Check",
                InspectionCriteria = "Mộng âm dương khít chặt (khe hở <= 0.2mm), tim lỗ cam chốt chuẩn xác theo bản vẽ CAD."
            },
            new()
            {
                QualityGateCode = "QC-GATE-04",
                QualityGateName = "Cổng KCS bắt buộc Kết cấu khung & Mộng dán keo (Hold Point Assembly Gate)",
                StageSequence = 5,
                IsHoldPoint = true,
                InspectionType = "100% Mandatory Gate (Hold Point)",
                InspectionCriteria = "Độ vuông góc chéo sai lệch <= 1.0mm, keo ghép mộng ngấu 100%, kết cấu chịu lực đạt chuẩn trước khi chuyển sang sơn."
            },
            new()
            {
                QualityGateCode = "QC-GATE-05",
                QualityGateName = "Cổng kiểm soát màng sơn & Màu sắc hoàn thiện (Finish Coating Gate)",
                StageSequence = 6,
                IsHoldPoint = false,
                InspectionType = "In-Process Self-Check",
                InspectionCriteria = "Màng sơn phủ bóng đều màu, không da cam, không bọt khí rộp, độ bám dính đạt cấp 1."
            },
            new()
            {
                QualityGateCode = "QC-GATE-06",
                QualityGateName = "Cổng KCS xuất xưởng & Đóng gói hoàn thiện (Final QC Gate)",
                StageSequence = 7,
                IsHoldPoint = false,
                InspectionType = "100% Mandatory Gate",
                InspectionCriteria = "Kiểm tra ngoại quan 100% không trầy xước, đầy đủ tem mác barcode, phụ kiện ốc vít kèm theo và đóng thùng đạt chuẩn."
            }
        };
    }

    // =========================================================================
    // [SC-19 CORE & QT-05]: PHÂN ĐOẠN 2 - CƯỠNG CHẾ TUẦN TỰ (S -> S+1) & 100% QC PASS TRÊN KANBAN
    // Cưỡng chế quy tắc:
    // 1. Công đoạn hiện tại phải đạt 100% sản phẩm Pass (PassedQty >= BatchQty hoặc StepStatus == PASSED).
    // 2. Chuyển tuần tự State Machine: Target = Current + 1; cấm nhảy cóc và cấm lùi bước.
    // 3. Chốt chặn Hold Point Barrier: Lô Overdrive tiến tới Hold Point mà phụ kiện chưa xuất kho sẽ bị KHÓA CỨNG (HOLD_POINT_BLOCKED).
    // =========================================================================
    /// <inheritdoc />
    public async Task<ProductionBatchDto> AdvanceStepAsync(
        long batchId,
        AdvanceStepRequestDto request,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _batchRepository.GetByIdAsync(batchId, cancellationToken);
        if (batch == null)
        {
            throw new NotFoundException($"Không tìm thấy thẻ lô sản xuất với ID #{batchId}.");
        }

        if (batch.BatchStatus == BatchStatusEnum.LOCKED)
        {
            throw new DomainException(
                "Thẻ lô sản xuất đang ở trạng thái LOCKED (Chốt chặn đầu vào BR-008). Thủ kho cần bấm xác nhận xuất kho cấp phát vật tư để bắt đầu gia công.",
                "BATCH_LOCKED_MATERIAL_REQUIRED");
        }

        if (batch.BatchStatus == BatchStatusEnum.HOLD_POINT_BLOCKED)
        {
            throw new DomainException(
                $"Thẻ lô đang bị khóa cứng tại chốt chặn Hold Point công đoạn {batch.HoldPointStageSeq} do chưa xuất kho đủ phụ kiện ốc vít/sơn. Cần bổ sung vật tư để mở khóa.",
                "HOLD_POINT_BLOCKED");
        }

        if (batch.BatchStatus == BatchStatusEnum.DONE)
        {
            throw new DomainException("Thẻ lô đã hoàn tất toàn bộ các công đoạn sản xuất (DONE).", "BATCH_ALREADY_DONE");
        }

        var steps = batch.BatchRoutingSteps.OrderBy(s => s.StageSequence).ToList();
        int currentSeq = batch.CurrentStageSeq;
        var currentStep = steps.FirstOrDefault(s => s.StageSequence == currentSeq);
        if (currentStep == null)
        {
            throw new DomainException($"Không tìm thấy thông tin công đoạn hiện tại {currentSeq} của thẻ lô.", "STEP_NOT_FOUND");
        }

        // 1. Kiểm tra điều kiện 100% QC Pass (BR-008, QT-05)
        bool is100Passed = currentStep.StepStatus == StepStatusEnum.PASSED || currentStep.PassedQty >= batch.BatchQty;
        if (!is100Passed)
        {
            throw new DomainException(
                $"Quy tắc kiểm định 100% (BR-008, QT-05): Công đoạn {currentSeq} ({currentStep.StageName}) chưa đạt 100% sản phẩm đạt chuẩn ({currentStep.PassedQty}/{batch.BatchQty} SP). Bắt buộc KCS kiểm tra đạt 100% mới được chuyển bước.",
                "QC_GATE_INCOMPLETE");
        }

        // 2. Cưỡng chế tuần tự State Machine: Chỉ được chuyển sang S + 1, không được nhảy cóc
        int targetSeq = request.TargetStageSeq.GetValueOrDefault(currentSeq + 1);
        if (targetSeq <= currentSeq)
        {
            throw new DomainException(
                $"Công đoạn đích ({targetSeq}) phải lớn hơn công đoạn hiện tại ({currentSeq}). Không được chuyển lùi công đoạn.",
                "INVALID_TARGET_STAGE");
        }

        if (targetSeq > currentSeq + 1)
        {
            throw new DomainException(
                $"Cưỡng chế tuần tự luồng sản xuất (Sequential State Machine - QT-05): Không được phép nhảy cóc từ công đoạn {currentSeq} sang {targetSeq}. Thẻ lô phải di chuyển tuần tự qua từng trạm máy.",
                "STAGE_JUMP_FORBIDDEN");
        }

        // 3. Kiểm tra chốt chặn Critical Hold Point Barrier đối với lô chạy Overdrive (QT-06, KBN02, SC-20)
        if (batch.IsOverdriver && !batch.GoodsIssuedUnlocked && batch.HoldPointStageSeq.HasValue && targetSeq >= batch.HoldPointStageSeq.Value)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                currentStep.StepStatus = StepStatusEnum.PASSED;
                currentStep.CompletedAt = DateTime.UtcNow;

                var holdStep = steps.FirstOrDefault(s => s.StageSequence == targetSeq);
                if (holdStep != null)
                {
                    holdStep.StepStatus = StepStatusEnum.BLOCKED;
                }

                batch.BatchStatus = BatchStatusEnum.HOLD_POINT_BLOCKED;
                batch.UpdatedAt = DateTime.UtcNow;

                await _batchRepository.UpdateAsync(batch);
                await _batchRepository.UpdateStepAsync(currentStep);
                if (holdStep != null)
                {
                    await _batchRepository.UpdateStepAsync(holdStep);
                }

                await _bomRepository.AddAuditLogAsync(new AuditLog
                {
                    UserId = currentUserId,
                    ActionType = "HOLD_POINT_BARRIER_LOCKED",
                    EntityName = "ProductionBatch",
                    EntityId = batch.BatchCode,
                    BeforeState = $"Stage_{currentSeq}_IN_PROGRESS",
                    AfterState = $"Stage_{targetSeq}_HOLD_POINT_BLOCKED",
                    Reason = $"Thẻ lô Overdrive {batch.BatchCode} tự động bị khóa cứng tại chốt chặn Hold Point công đoạn {targetSeq} ({holdStep?.StageName}) do phụ kiện chưa xuất kho (QT-06, KBN02).",
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

            throw new DomainException(
                $"Chốt chặn kỹ thuật Hold Point (QT-06, KBN02): Thẻ lô Overdrive {batch.BatchCode} đã hoàn thành công đoạn {currentSeq} nhưng tự động bị KHÓA CỨNG trước công đoạn {targetSeq} do phụ kiện ốc vít/sơn chưa được xuất kho cấp phát.",
                "HOLD_POINT_BARRIER_BLOCKED");
        }

        // 4. Chuyển bước hợp lệ sang S + 1
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            currentStep.StepStatus = StepStatusEnum.PASSED;
            currentStep.CompletedAt = DateTime.UtcNow;
            await _batchRepository.UpdateStepAsync(currentStep);

            int maxSeq = steps.Max(s => s.StageSequence);
            if (targetSeq <= maxSeq)
            {
                batch.CurrentStageSeq = targetSeq;
                var nextStep = steps.FirstOrDefault(s => s.StageSequence == targetSeq);
                if (nextStep != null)
                {
                    nextStep.StepStatus = StepStatusEnum.IN_PROGRESS;
                    await _batchRepository.UpdateStepAsync(nextStep);
                }
            }
            else
            {
                // Đã qua công đoạn cuối cùng (Đóng gói xuất xưởng) -> Lô hoàn tất
                batch.BatchStatus = BatchStatusEnum.DONE;
                batch.UpdatedAt = DateTime.UtcNow;

                // Kiểm tra xem tất cả các lô của OrderItem đã xong chưa
                var allBatches = await _batchRepository.GetByOrderItemIdAsync(batch.OrderItemId, cancellationToken);
                bool allDone = allBatches.Where(b => b.BatchId != batch.BatchId).All(b => b.BatchStatus == BatchStatusEnum.DONE);
                if (allDone)
                {
                    var orderItem = await _orderItemRepository.GetByIdAsync(batch.OrderItemId);
                    if (orderItem != null)
                    {
                        orderItem.Status = OrderItemStatusEnum.DONE;
                        await _orderItemRepository.UpdateAsync(orderItem);

                        var customerOrder = await _orderItemRepository.GetOrderByIdAsync(orderItem.OrderId, cancellationToken);
                        if (customerOrder != null && customerOrder.OrderItems.All(oi => oi.Status == OrderItemStatusEnum.DONE))
                        {
                            customerOrder.Status = OrderStatusEnum.COMPLETED;
                            await _orderItemRepository.UpdateOrderAsync(customerOrder);
                        }
                    }
                }
            }

            batch.UpdatedAt = DateTime.UtcNow;
            await _batchRepository.UpdateAsync(batch);

            await _bomRepository.AddAuditLogAsync(new AuditLog
            {
                UserId = currentUserId,
                ActionType = AuditActionConstants.KanbanStepUpdated,
                EntityName = "ProductionBatch",
                EntityId = batch.BatchCode,
                BeforeState = $"Stage_{currentSeq}",
                AfterState = targetSeq <= maxSeq ? $"Stage_{targetSeq}" : "DONE",
                Reason = request.Notes ?? $"Chuyển bước thẻ Kanban {batch.BatchCode} từ công đoạn {currentSeq} sang {(targetSeq <= maxSeq ? $"công đoạn {targetSeq}" : "HOÀN TẤT (DONE)")} sau khi đạt 100% QC Pass (QT-05).",
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync();

            return MapBatchToDto(batch);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    // =========================================================================
    // [KBN01 & QT-06]: PHÂN ĐOẠN 3 - KIỂM ĐỊNH QC 100% & THUẬT TOÁN TÁCH NHÁNH 97%-3%
    // 1. Cưỡng chế quy tắc 100% kiểm định (InspectedQty == BatchQty).
    // 2. Nếu đạt chuẩn 100% (FailQty == 0): Bước hoàn tất (PASSED).
    // 3. Nếu phát sinh lỗi (FailQty > 0):
    //    - Thẻ chính cập nhật BatchQty = PassQty và hoàn tất bước hiện tại.
    //    - Đồng bộ TargetQty = PassQty cho tất cả các công đoạn sau của thẻ chính.
    //    - Sinh thẻ nhánh phụ viền cam (REWORK_SUB_CARD) với BatchQty = FailQty để sửa chữa độc lập.
    // =========================================================================
    /// <inheritdoc />
    public async Task<QcInspectionResultDto> RecordQcInspectionAsync(
        long batchId,
        RecordQcInspectionDto request,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _batchRepository.GetByIdAsync(batchId, cancellationToken);
        if (batch == null)
        {
            throw new NotFoundException($"Không tìm thấy thẻ lô sản xuất với ID #{batchId}.");
        }

        if (batch.BatchStatus == BatchStatusEnum.DONE)
        {
            throw new DomainException("Thẻ lô đã hoàn tất sản xuất, không thể ghi nhận KCS bổ sung.", "BATCH_DONE");
        }

        var steps = batch.BatchRoutingSteps.OrderBy(s => s.StageSequence).ToList();
        var step = steps.FirstOrDefault(s => s.BatchStepId == request.BatchStepId);
        if (step == null)
        {
            throw new NotFoundException($"Không tìm thấy công đoạn thực thi với ID #{request.BatchStepId}.");
        }

        // Quy tắc kiểm định 100% (BR-008): Phải kiểm tra đúng bằng số lượng hiện có của thẻ lô
        if (request.InspectedQty != batch.BatchQty)
        {
            throw new DomainException(
                $"Quy tắc kiểm định 100% (BR-008): Số lượng kiểm định ({request.InspectedQty} SP) phải bằng đúng 100% số lượng của thẻ lô ({batch.BatchQty} SP). Không chấp nhận kiểm định xác suất.",
                "QC_100_RULE_VIOLATION");
        }

        if (request.PassQty + request.FailQty != request.InspectedQty)
        {
            throw new DomainException(
                $"Tổng số lượng đạt Pass ({request.PassQty}) + lỗi Fail ({request.FailQty}) phải bằng đúng tổng số lượng kiểm định ({request.InspectedQty}).",
                "QC_SUM_MISMATCH");
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var inspection = new QcInspection
            {
                BatchStepId = request.BatchStepId,
                InspectorId = currentUserId,
                InspectedQty = request.InspectedQty,
                PassQty = request.PassQty,
                FailQty = request.FailQty,
                ResultStatus = request.FailQty == 0
                    ? QcResultStatusEnum.FULL_PASS_100
                    : (request.PassQty > 0 ? QcResultStatusEnum.BRANCHED_PARTIAL : QcResultStatusEnum.FULL_REJECT),
                InspectedAt = DateTime.UtcNow
            };

            await _unitOfWork.QcInspections.AddInspectionAsync(inspection, cancellationToken);

            if (request.FailQty == 0)
            {
                // Trường hợp 100% đạt chuẩn
                step.PassedQty = request.PassQty;
                step.DefectQty = 0;
                step.StepStatus = StepStatusEnum.PASSED;
                await _batchRepository.UpdateStepAsync(step);

                await _bomRepository.AddAuditLogAsync(new AuditLog
                {
                    UserId = currentUserId,
                    ActionType = AuditActionConstants.QcInspectionRecorded,
                    EntityName = "QcInspection",
                    EntityId = batch.BatchCode,
                    BeforeState = $"Stage_{step.StageSequence}_IN_PROGRESS",
                    AfterState = $"Stage_{step.StageSequence}_100_PASS",
                    Reason = $"Kiểm định KCS đạt chuẩn 100% ({request.PassQty}/{request.InspectedQty} SP) tại công đoạn {step.StageName} cho thẻ {batch.BatchCode} (BR-008).",
                    CreatedAt = DateTime.UtcNow
                });

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync();

                return new QcInspectionResultDto
                {
                    QcId = inspection.QcId,
                    BatchStepId = step.BatchStepId,
                    BatchId = batch.BatchId,
                    InspectedQty = request.InspectedQty,
                    PassQty = request.PassQty,
                    FailQty = 0,
                    IsBranched = false,
                    Message = $"Ghi nhận kiểm định đạt chuẩn 100% ({request.PassQty} SP). Thẻ chính {batch.BatchCode} đủ điều kiện chuyển sang công đoạn tiếp theo."
                };
            }
            else
            {
                // Trường hợp phát sinh sản phẩm lỗi -> Thuật toán tách nhánh 97%-3% (KBN01, QT-06)
                var disposition = (request.Disposition?.Trim().ToUpperInvariant() == "SCRAP_REPLENISH")
                    ? DefectDispositionEnum.SCRAP_REPLENISH
                    : DefectDispositionEnum.REWORK_SUB_CARD;

                // 1. Cập nhật Thẻ chính với số lượng đạt chuẩn (PassQty)
                batch.BatchQty = request.PassQty;
                step.PassedQty = request.PassQty;
                step.DefectQty = request.FailQty;
                step.StepStatus = StepStatusEnum.PASSED;
                await _batchRepository.UpdateAsync(batch);
                await _batchRepository.UpdateStepAsync(step);

                // Khắc phục lỗ hổng Report 4: Cập nhật TargetQty = PassQty cho TẤT CẢ các công đoạn sau của thẻ chính
                foreach (var downstreamStep in steps.Where(s => s.StageSequence > step.StageSequence))
                {
                    downstreamStep.TargetQty = request.PassQty;
                    await _batchRepository.UpdateStepAsync(downstreamStep);
                }

                // 2. Sinh thẻ nhánh phụ viền cam (Sub-Card) để sửa chữa
                string subBatchCode = $"SUB-{batch.BatchCode}-R{step.StageSequence}";
                var subBatch = new ProductionBatch
                {
                    BatchCode = subBatchCode,
                    OrderItemId = batch.OrderItemId,
                    ParentBatchId = batch.BatchId,
                    BatchType = BatchTypeEnum.REWORK_SUB_CARD,
                    BatchQty = request.FailQty,
                    CurrentStageSeq = step.StageSequence,
                    IsOverdriver = batch.IsOverdriver,
                    HoldPointStageSeq = batch.HoldPointStageSeq,
                    GoodsIssuedUnlocked = batch.GoodsIssuedUnlocked,
                    BatchStatus = BatchStatusEnum.IN_PROGRESS,
                    CreatedAt = DateTime.UtcNow
                };

                await _batchRepository.AddAsync(subBatch, cancellationToken);

                // Sinh các routing steps cho thẻ phụ (tại công đoạn lỗi và các công đoạn sau)
                var subSteps = steps.Where(s => s.StageSequence >= step.StageSequence).Select(s => new BatchRoutingStep
                {
                    ProductionBatch = subBatch,
                    StageSequence = s.StageSequence,
                    StageName = s.StageName,
                    IsHoldPoint = s.IsHoldPoint,
                    TargetQty = request.FailQty,
                    PassedQty = 0,
                    DefectQty = 0,
                    StepStatus = s.StageSequence == step.StageSequence ? StepStatusEnum.IN_PROGRESS : StepStatusEnum.WAITING
                }).ToList();

                await _batchRepository.AddStepsRangeAsync(subSteps, cancellationToken);

                // 3. Ghi nhận DefectLog
                var defectLog = new DefectLog
                {
                    QcInspection = inspection,
                    DefectCode = request.DefectCode ?? "DEF-ERR-01",
                    DefectName = request.DefectName ?? "Lỗi gia công chi tiết cần sửa chữa rework",
                    DefectQty = request.FailQty,
                    Disposition = disposition,
                    SpawnedSubBatch = subBatch
                };

                await _unitOfWork.QcInspections.AddDefectLogsRangeAsync(new[] { defectLog }, cancellationToken);

                await _bomRepository.AddAuditLogAsync(new AuditLog
                {
                    UserId = currentUserId,
                    ActionType = AuditActionConstants.KanbanSubBatchSpawned,
                    EntityName = "ProductionBatch",
                    EntityId = subBatch.BatchCode,
                    BeforeState = $"MainCard_{batch.BatchCode}_Qty_{request.InspectedQty}",
                    AfterState = $"MainCard_{request.PassQty}_SubCard_{subBatch.BatchCode}_{request.FailQty}",
                    Reason = $"Thực hiện tách nhánh 97%-3% (KBN01): Thẻ chính {batch.BatchCode} tiếp tục với {request.PassQty} SP, sinh thẻ nhánh phụ {subBatch.BatchCode} ({request.FailQty} SP) để sửa chữa.",
                    CreatedAt = DateTime.UtcNow
                });

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync();

                return new QcInspectionResultDto
                {
                    QcId = inspection.QcId,
                    BatchStepId = step.BatchStepId,
                    BatchId = batch.BatchId,
                    InspectedQty = request.InspectedQty,
                    PassQty = request.PassQty,
                    FailQty = request.FailQty,
                    IsBranched = true,
                    SpawnedSubBatchId = subBatch.BatchId,
                    SpawnedSubBatchCode = subBatch.BatchCode,
                    Message = $"Kích hoạt tách nhánh 97%-3% thành công! Thẻ chính {batch.BatchCode} cập nhật còn {request.PassQty} SP đạt chuẩn để tiếp tục; đã sinh thẻ nhánh phụ {subBatch.BatchCode} ({request.FailQty} SP) để sửa chữa tại {step.StageName}."
                };
            }
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    // =========================================================================
    // [SC-20 & UC-63]: PHÂN ĐOẠN 4 - LUỒNG NGOẠI LỆ OVERDRIVE: YÊU CẦU, PHÊ DUYỆT & MỞ KHÓA
    // =========================================================================
    /// <inheritdoc />
    public async Task<OverdriveOperationResultDto> RequestOverdriveAsync(
        long batchId,
        RequestOverdriveDto request,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _batchRepository.GetByIdAsync(batchId, cancellationToken);
        if (batch == null)
        {
            throw new NotFoundException($"Không tìm thấy thẻ lô sản xuất với ID #{batchId}.");
        }

        if (string.IsNullOrWhiteSpace(request.Justification))
        {
            throw new DomainException("Lý do giải trình kỹ thuật và thời hạn ETD là bắt buộc khi gửi yêu cầu Overdrive (UC-63).", "OVERDRIVE_JUSTIFICATION_REQUIRED");
        }

        int holdPointSeq = request.HoldPointStageSeq.GetValueOrDefault(5);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var log = new OverdriveLog
            {
                BatchId = batch.BatchId,
                RequestedBy = currentUserId,
                ApprovedBy = currentUserId, // Pending approval
                Justification = request.Justification.Trim(),
                HoldPointStageSeq = holdPointSeq,
                ApprovedAt = DateTime.UtcNow
            };

            await _unitOfWork.QcInspections.AddOverdriveLogAsync(log, cancellationToken);

            await _bomRepository.AddAuditLogAsync(new AuditLog
            {
                UserId = currentUserId,
                ActionType = "OVERDRIVE_REQUESTED",
                EntityName = "ProductionBatch",
                EntityId = batch.BatchCode,
                BeforeState = $"Status_{batch.BatchStatus}",
                AfterState = $"Overdrive_Requested_HoldPoint_{holdPointSeq}",
                Reason = $"Tổ trưởng sản xuất gửi yêu cầu kích hoạt Overdrive cho lô {batch.BatchCode}: '{request.Justification.Trim()}' (UC-63).",
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync();

            return new OverdriveOperationResultDto
            {
                BatchId = batch.BatchId,
                BatchCode = batch.BatchCode,
                IsOverdriver = batch.IsOverdriver,
                HoldPointStageSeq = holdPointSeq,
                BatchStatus = batch.BatchStatus.ToString(),
                Message = $"Đã gửi yêu cầu chạy Overdrive cho lô {batch.BatchCode} tới Chủ xưởng phê duyệt (Hold Point công đoạn {holdPointSeq})."
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<OverdriveOperationResultDto> ApproveOverdriveAsync(
        long batchId,
        ApproveOverdriveDto request,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _batchRepository.GetByIdAsync(batchId, cancellationToken);
        if (batch == null)
        {
            throw new NotFoundException($"Không tìm thấy thẻ lô sản xuất với ID #{batchId}.");
        }

        if (!request.Approved)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                await _bomRepository.AddAuditLogAsync(new AuditLog
                {
                    UserId = currentUserId,
                    ActionType = "OVERDRIVE_REJECTED",
                    EntityName = "ProductionBatch",
                    EntityId = batch.BatchCode,
                    BeforeState = $"Status_{batch.BatchStatus}",
                    AfterState = "OVERDRIVE_REJECTED",
                    Reason = request.Notes ?? $"Chủ xưởng từ chối yêu cầu Overdrive của lô {batch.BatchCode} (KBN02).",
                    CreatedAt = DateTime.UtcNow
                });

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync();

                return new OverdriveOperationResultDto
                {
                    BatchId = batch.BatchId,
                    BatchCode = batch.BatchCode,
                    IsOverdriver = false,
                    HoldPointStageSeq = batch.HoldPointStageSeq,
                    BatchStatus = batch.BatchStatus.ToString(),
                    Message = $"Chủ xưởng đã TỪ CHỐI yêu cầu Overdrive của lô {batch.BatchCode}. Thẻ lô giữ nguyên trạng thái bị khóa."
                };
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }

        int holdPointSeq = request.HoldPointStageSeq.GetValueOrDefault(batch.HoldPointStageSeq.GetValueOrDefault(5));

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            batch.IsOverdriver = true;
            batch.HoldPointStageSeq = holdPointSeq;
            batch.BatchStatus = BatchStatusEnum.IN_PROGRESS;
            batch.UpdatedAt = DateTime.UtcNow;

            var step1 = batch.BatchRoutingSteps.FirstOrDefault(s => s.StageSequence == 1);
            if (step1 != null && step1.StepStatus == StepStatusEnum.WAITING)
            {
                step1.StepStatus = StepStatusEnum.IN_PROGRESS;
                await _batchRepository.UpdateStepAsync(step1);
            }

            await _batchRepository.UpdateAsync(batch);

            var overdriveLog = new OverdriveLog
            {
                BatchId = batch.BatchId,
                RequestedBy = currentUserId,
                ApprovedBy = currentUserId,
                Justification = request.Notes ?? "Phê duyệt bởi Chủ xưởng kèm thiết lập điểm dừng kỹ thuật Hold Point (KBN02).",
                HoldPointStageSeq = holdPointSeq,
                ApprovedAt = DateTime.UtcNow
            };

            await _unitOfWork.QcInspections.AddOverdriveLogAsync(overdriveLog, cancellationToken);

            await _bomRepository.AddAuditLogAsync(new AuditLog
            {
                UserId = currentUserId,
                ActionType = AuditActionConstants.HoldPointOverdriveApproved,
                EntityName = "ProductionBatch",
                EntityId = batch.BatchCode,
                BeforeState = "LOCKED",
                AfterState = $"OVERDRIVE_APPROVED_HOLD_POINT_{holdPointSeq}",
                Reason = request.Notes ?? $"Chủ xưởng phê duyệt kích hoạt Overdrive cho lô {batch.BatchCode} tới trạm dừng Hold Point {holdPointSeq} (KBN02, QT-06).",
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync();

            return new OverdriveOperationResultDto
            {
                BatchId = batch.BatchId,
                BatchCode = batch.BatchCode,
                IsOverdriver = true,
                HoldPointStageSeq = holdPointSeq,
                BatchStatus = batch.BatchStatus.ToString(),
                Message = $"Chủ xưởng đã PHÊ DUYỆT Overdrive cho lô {batch.BatchCode}. Thẻ lô đã được mở khóa sang IN_PROGRESS và cho phép gia công trước trạm dừng Hold Point công đoạn {holdPointSeq}."
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<OverdriveOperationResultDto> UnlockHoldPointAsync(
        long batchId,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _batchRepository.GetByIdAsync(batchId, cancellationToken);
        if (batch == null)
        {
            throw new NotFoundException($"Không tìm thấy thẻ lô sản xuất với ID #{batchId}.");
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            batch.GoodsIssuedUnlocked = true;
            if (batch.BatchStatus == BatchStatusEnum.HOLD_POINT_BLOCKED)
            {
                batch.BatchStatus = BatchStatusEnum.IN_PROGRESS;
                var currentStep = batch.BatchRoutingSteps.FirstOrDefault(s => s.StageSequence == batch.CurrentStageSeq);
                if (currentStep != null && currentStep.StepStatus == StepStatusEnum.BLOCKED)
                {
                    currentStep.StepStatus = StepStatusEnum.IN_PROGRESS;
                    await _batchRepository.UpdateStepAsync(currentStep);
                }
            }

            batch.UpdatedAt = DateTime.UtcNow;
            await _batchRepository.UpdateAsync(batch);

            await _bomRepository.AddAuditLogAsync(new AuditLog
            {
                UserId = currentUserId,
                ActionType = AuditActionConstants.KanbanGoodsIssuedUnlocked,
                EntityName = "ProductionBatch",
                EntityId = batch.BatchCode,
                BeforeState = "HOLD_POINT_BLOCKED",
                AfterState = "IN_PROGRESS",
                Reason = $"Mở khóa chốt chặn Hold Point cho thẻ lô {batch.BatchCode} sau khi Thủ kho bấm xuất kho phụ kiện bổ sung (SC-20).",
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync();

            return new OverdriveOperationResultDto
            {
                BatchId = batch.BatchId,
                BatchCode = batch.BatchCode,
                IsOverdriver = batch.IsOverdriver,
                HoldPointStageSeq = batch.HoldPointStageSeq,
                BatchStatus = batch.BatchStatus.ToString(),
                Message = $"Đã mở khóa chốt chặn Hold Point cho thẻ lô {batch.BatchCode} thành công. Dây chuyền tiếp tục lắp ráp và hoàn thiện."
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    private static ProductionBatchDto MapBatchToDto(
        ProductionBatch batch,
        IEnumerable<RoutingStage>? routingStages = null)
    {
        var steps = batch.BatchRoutingSteps.OrderBy(s => s.StageSequence).ToList();
        var currentStep = steps.FirstOrDefault(s => s.StageSequence == batch.CurrentStageSeq);

        return new ProductionBatchDto
        {
            BatchId = batch.BatchId,
            BatchCode = batch.BatchCode,
            OrderItemId = batch.OrderItemId,
            ParentBatchId = batch.ParentBatchId,
            BatchType = batch.BatchType.ToString(),
            BatchQty = batch.BatchQty,
            CurrentStageSeq = batch.CurrentStageSeq,
            CurrentStageName = currentStep?.StageName ?? $"Công đoạn {batch.CurrentStageSeq}",
            CurrentWorkstationCode = InferWorkstationCode(batch.CurrentStageSeq, currentStep?.StageName ?? string.Empty),
            IsOverdriver = batch.IsOverdriver,
            HoldPointStageSeq = batch.HoldPointStageSeq,
            GoodsIssuedUnlocked = batch.GoodsIssuedUnlocked,
            BatchStatus = batch.BatchStatus.ToString(),
            CreatedAt = batch.CreatedAt,
            Steps = steps.Select(MapStepToDto).ToList()
        };
    }

    private static BatchRoutingStepDto MapStepToDto(BatchRoutingStep step)
    {
        var wsCode = InferWorkstationCode(step.StageSequence, step.StageName);
        var wsName = InferWorkstationName(step.StageSequence, step.StageName);
        var gateCode = $"QC-GATE-0{Math.Clamp(step.StageSequence, 1, 9)}";
        var gateName = InferQualityGateName(step.StageSequence, step.StageName, step.IsHoldPoint);
        var criteria = InferInspectionCriteria(step.StageSequence, step.StageName, step.IsHoldPoint);

        return new BatchRoutingStepDto
        {
            BatchStepId = step.BatchStepId,
            BatchId = step.BatchId,
            StageSequence = step.StageSequence,
            StageName = step.StageName,
            WorkstationCode = wsCode,
            WorkstationName = wsName,
            QualityGateCode = gateCode,
            QualityGateName = gateName,
            InspectionCriteria = criteria,
            IsHoldPoint = step.IsHoldPoint,
            TargetQty = step.TargetQty,
            PassedQty = step.PassedQty,
            DefectQty = step.DefectQty,
            StepStatus = step.StepStatus.ToString(),
            CompletedAt = step.CompletedAt
        };
    }

    private static string InferWorkstationCode(int seq, string stageName)
    {
        var lower = (stageName ?? string.Empty).ToLowerInvariant();
        if (lower.Contains("pha phôi") || lower.Contains("xẻ") || lower.Contains("cắt")) return "WC-01-SAW";
        if (lower.Contains("bào") || lower.Contains("nhám")) return "WC-02-SAND";
        if (lower.Contains("khoan") || lower.Contains("mộng") || lower.Contains("cnc")) return "WC-03-CNC";
        if (lower.Contains("lắp ráp") || lower.Contains("dựng")) return "WC-04-ASSY";
        if (lower.Contains("sơn") || lower.Contains("pu") || lower.Contains("màu")) return "WC-05-PAINT";
        if (lower.Contains("đóng gói") || lower.Contains("tem") || lower.Contains("carton")) return "WC-06-PACK";
        return $"WC-0{Math.Clamp(seq, 1, 9)}";
    }

    private static string InferWorkstationName(int seq, string stageName)
    {
        var code = InferWorkstationCode(seq, stageName);
        return code switch
        {
            "WC-01-SAW" => "Trạm Pha phôi & Xẻ gỗ thô",
            "WC-02-SAND" => "Trạm Bào 4 mặt & Chà nhám phẳng",
            "WC-03-CNC" => "Trạm Khoan chốt & Phay mộng âm dương CNC",
            "WC-04-ASSY" => "Trạm Lắp ráp kết cấu & Vào mộng dán keo",
            "WC-05-PAINT" => "Trạm Phun sơn lót PU & Sơn hoàn thiện",
            "WC-06-PACK" => "Trạm KCS xuất xưởng & Đóng thùng carton",
            _ => $"Trạm máy công đoạn {seq}"
        };
    }

    /// <inheritdoc />
    public async Task<ReinspectSubBatchResultDto> ReinspectSubBatchAsync(
        long subBatchId,
        ReinspectSubBatchDto request,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var subBatch = await _batchRepository.GetByIdAsync(subBatchId, cancellationToken);
        if (subBatch == null)
        {
            throw new NotFoundException($"Không tìm thấy thẻ nhánh phụ có ID {subBatchId}.");
        }

        if (subBatch.BatchType != BatchTypeEnum.REWORK_SUB_CARD && subBatch.BatchType != BatchTypeEnum.REPLENISH_LOT)
        {
            throw new DomainException("Chỉ áp dụng quy trình nghiệm thu lại cho thẻ nhánh phụ (REWORK_SUB_CARD hoặc REPLENISH_LOT).", "INVALID_SUB_BATCH_TYPE");
        }

        if (subBatch.BatchStatus == BatchStatusEnum.DONE)
        {
            throw new DomainException("Thẻ nhánh phụ này đã hoàn tất hoặc đã được gộp/chuyển tiếp trước đó.", "SUB_BATCH_ALREADY_DONE");
        }

        var steps = subBatch.BatchRoutingSteps.OrderBy(s => s.StageSequence).ToList();
        var step = steps.FirstOrDefault(s => s.BatchStepId == request.BatchStepId);
        if (step == null)
        {
            throw new NotFoundException($"Không tìm thấy công đoạn thẻ phụ có ID {request.BatchStepId}.");
        }

        if (request.InspectedQty != subBatch.BatchQty)
        {
            throw new DomainException(
                $"Quy tắc kiểm định 100% (BR-008): Số lượng nghiệm thu lại ({request.InspectedQty} SP) phải bằng đúng 100% số lượng của thẻ nhánh ({subBatch.BatchQty} SP).",
                "QC_100_RULE_VIOLATION");
        }

        if (request.PassQty + request.FailQty != request.InspectedQty)
        {
            throw new DomainException(
                $"Tổng số lượng đạt ({request.PassQty}) + lỗi ({request.FailQty}) phải bằng đúng tổng số lượng nghiệm thu lại ({request.InspectedQty}).",
                "QC_SUM_MISMATCH");
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var inspection = new QcInspection
            {
                BatchStepId = step.BatchStepId,
                InspectorId = currentUserId,
                InspectedQty = request.InspectedQty,
                PassQty = request.PassQty,
                FailQty = request.FailQty,
                ResultStatus = request.FailQty == 0
                    ? QcResultStatusEnum.FULL_PASS_100
                    : (request.PassQty > 0 ? QcResultStatusEnum.BRANCHED_PARTIAL : QcResultStatusEnum.FULL_REJECT),
                InspectedAt = DateTime.UtcNow
            };

            await _unitOfWork.QcInspections.AddInspectionAsync(inspection, cancellationToken);

            step.PassedQty = request.PassQty;
            step.DefectQty = request.FailQty;
            step.StepStatus = request.PassQty > 0 ? StepStatusEnum.PASSED : StepStatusEnum.BLOCKED;
            await _batchRepository.UpdateStepAsync(step);

            if (request.FailQty > 0)
            {
                // Nếu vẫn có phôi không đạt sau sửa chữa, giảm số lượng thẻ phụ xuống số lượng Pass thực tế
                subBatch.BatchQty = request.PassQty;
                foreach (var ds in steps.Where(s => s.StageSequence > step.StageSequence))
                {
                    ds.TargetQty = request.PassQty;
                    await _batchRepository.UpdateStepAsync(ds);
                }
            }

            if (subBatch.BatchQty == 0)
            {
                subBatch.BatchStatus = BatchStatusEnum.DONE;
            }
            await _batchRepository.UpdateAsync(subBatch);

            await _bomRepository.AddAuditLogAsync(new AuditLog
            {
                UserId = currentUserId,
                ActionType = AuditActionConstants.ReworkReinspected,
                EntityName = "ProductionBatch",
                EntityId = subBatch.BatchCode,
                BeforeState = $"SubCard_{subBatch.BatchCode}_Stage_{step.StageSequence}_REPAIRING",
                AfterState = $"SubCard_{subBatch.BatchCode}_Pass_{request.PassQty}_Fail_{request.FailQty}",
                Reason = $"Nghiệm thu lại KCS thẻ nhánh {subBatch.BatchCode} tại {step.StageName}: {request.PassQty}/{request.InspectedQty} SP đạt chuẩn. Ghi chú: {request.Notes ?? "Không có"}.",
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync();

            return new ReinspectSubBatchResultDto
            {
                QcId = inspection.QcId,
                SubBatchId = subBatch.BatchId,
                SubBatchCode = subBatch.BatchCode,
                InspectedQty = request.InspectedQty,
                PassQty = request.PassQty,
                FailQty = request.FailQty,
                IsFullyPassed = request.FailQty == 0,
                SubBatchStatus = subBatch.BatchStatus.ToString(),
                Message = request.PassQty > 0
                    ? $"Nghiệm thu lại thành công! Thẻ phụ {subBatch.BatchCode} có {request.PassQty} SP đạt chuẩn, sẵn sàng gộp vào thẻ chính (Merge Lot) hoặc chuyển tiếp độc lập."
                    : $"Nghiệm thu lại không đạt: toàn bộ {request.FailQty} SP không thể sửa chữa và phải hủy bỏ."
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<MergeSubBatchResultDto> MergeSubBatchAsync(
        long subBatchId,
        MergeSubBatchDto request,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var subBatch = await _batchRepository.GetByIdAsync(subBatchId, cancellationToken);
        if (subBatch == null)
        {
            throw new NotFoundException($"Không tìm thấy thẻ nhánh phụ có ID {subBatchId}.");
        }

        if (subBatch.BatchType != BatchTypeEnum.REWORK_SUB_CARD && subBatch.BatchType != BatchTypeEnum.REPLENISH_LOT)
        {
            throw new DomainException("Chỉ có thể gộp lô đối với thẻ nhánh phụ (REWORK_SUB_CARD hoặc REPLENISH_LOT).", "INVALID_SUB_BATCH_TYPE");
        }

        if (subBatch.BatchStatus == BatchStatusEnum.DONE)
        {
            throw new DomainException("Thẻ nhánh phụ này đã hoàn tất hoặc đã được gộp trước đó.", "SUB_BATCH_ALREADY_MERGED");
        }

        if (subBatch.BatchQty <= 0)
        {
            throw new DomainException("Thẻ nhánh phụ không còn số lượng đạt chuẩn (BatchQty <= 0) để gộp lô.", "EMPTY_SUB_BATCH_QTY");
        }

        if (!subBatch.ParentBatchId.HasValue)
        {
            throw new DomainException("Thẻ nhánh phụ không có thông tin thẻ chính (ParentBatchId bị null).", "MISSING_PARENT_BATCH");
        }

        var mainBatch = await _batchRepository.GetByIdAsync(subBatch.ParentBatchId.Value, cancellationToken);
        if (mainBatch == null)
        {
            throw new NotFoundException($"Không tìm thấy thẻ chính cha có ID {subBatch.ParentBatchId.Value}.");
        }

        if (mainBatch.BatchStatus == BatchStatusEnum.DONE)
        {
            throw new DomainException($"Thẻ chính {mainBatch.BatchCode} đã hoàn tất sản xuất (DONE). Vui lòng cho thẻ phụ chuyển tiếp độc lập.", "MAIN_BATCH_ALREADY_DONE");
        }

        // Quy tắc nghiệp vụ SC-20b: Chỉ cho phép gộp khi cả hai thẻ đang ở CÙNG công đoạn
        if (mainBatch.CurrentStageSeq != subBatch.CurrentStageSeq)
        {
            throw new DomainException(
                $"Không thể gộp lô vì hai thẻ đang ở hai công đoạn khác nhau! Thẻ chính đang ở công đoạn #{mainBatch.CurrentStageSeq}, thẻ phụ đang ở công đoạn #{subBatch.CurrentStageSeq}. Hãy cho thẻ phụ chuyển tiếp độc lập (Continue Independently).",
                "STAGE_MISMATCH_FOR_MERGE");
        }

        // Kiểm tra công đoạn hiện tại của thẻ phụ phải đạt chuẩn KCS (PASSED)
        var subCurrentStep = subBatch.BatchRoutingSteps.FirstOrDefault(s => s.StageSequence == subBatch.CurrentStageSeq);
        if (subCurrentStep == null || subCurrentStep.StepStatus != StepStatusEnum.PASSED)
        {
            throw new DomainException("Thẻ nhánh phụ chưa được nghiệm thu đạt chuẩn (PASSED) tại công đoạn hiện tại, không thể gộp lô.", "SUB_BATCH_NOT_PASSED");
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            int mergeQty = subBatch.BatchQty;
            int oldMainQty = mainBatch.BatchQty;
            int newMainQty = oldMainQty + mergeQty;

            // 1. Cập nhật thẻ chính
            mainBatch.BatchQty = newMainQty;
            var mainSteps = mainBatch.BatchRoutingSteps.OrderBy(s => s.StageSequence).ToList();
            foreach (var step in mainSteps.Where(s => s.StageSequence >= mainBatch.CurrentStageSeq))
            {
                step.TargetQty += mergeQty;
                if (step.StageSequence == mainBatch.CurrentStageSeq)
                {
                    step.PassedQty += mergeQty;
                }
                await _batchRepository.UpdateStepAsync(step);
            }
            await _batchRepository.UpdateAsync(mainBatch);

            // 2. Đóng thẻ phụ (Chuyển sang trạng thái DONE và ghi nhận đã tái hòa nhập)
            subBatch.BatchStatus = BatchStatusEnum.DONE;
            foreach (var step in subBatch.BatchRoutingSteps.Where(s => s.StageSequence > subBatch.CurrentStageSeq))
            {
                step.StepStatus = StepStatusEnum.PASSED;
                await _batchRepository.UpdateStepAsync(step);
            }
            await _batchRepository.UpdateAsync(subBatch);

            // 3. Ghi vết Audit Log bất biến (GBR-05)
            await _bomRepository.AddAuditLogAsync(new AuditLog
            {
                UserId = currentUserId,
                ActionType = AuditActionConstants.SubBatchMerged,
                EntityName = "ProductionBatch",
                EntityId = mainBatch.BatchCode,
                BeforeState = $"Main_{mainBatch.BatchCode}_Qty_{oldMainQty};Sub_{subBatch.BatchCode}_Qty_{mergeQty}",
                AfterState = $"Main_{mainBatch.BatchCode}_MergedNewQty_{newMainQty};Sub_{subBatch.BatchCode}_DONE",
                Reason = $"Tái hòa nhập / Gộp lô (Merge Lot) {mergeQty} SP từ thẻ phụ {subBatch.BatchCode} vào thẻ chính {mainBatch.BatchCode} tại công đoạn #{mainBatch.CurrentStageSeq}. Lý do: {request?.Reason ?? "Hoàn thành sửa chữa KCS"}.",
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync();

            return new MergeSubBatchResultDto
            {
                SubBatchId = subBatch.BatchId,
                SubBatchCode = subBatch.BatchCode,
                MainBatchId = mainBatch.BatchId,
                MainBatchCode = mainBatch.BatchCode,
                MergedQty = mergeQty,
                MainBatchNewQty = newMainQty,
                CurrentStageSeq = mainBatch.CurrentStageSeq,
                MainBatchStatus = mainBatch.BatchStatus.ToString(),
                SubBatchStatus = subBatch.BatchStatus.ToString(),
                Message = $"Gộp lô (Merge Lot) thành công! Đã hòa nhập {mergeQty} SP từ {subBatch.BatchCode} vào thẻ chính {mainBatch.BatchCode}. Tổng số lượng thẻ chính hiện tại: {newMainQty} SP."
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ContinueSubBatchResultDto> ContinueSubBatchIndependentlyAsync(
        long subBatchId,
        ContinueSubBatchIndependentlyDto request,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var subBatch = await _batchRepository.GetByIdAsync(subBatchId, cancellationToken);
        if (subBatch == null)
        {
            throw new NotFoundException($"Không tìm thấy thẻ nhánh phụ có ID {subBatchId}.");
        }

        if (subBatch.BatchType != BatchTypeEnum.REWORK_SUB_CARD && subBatch.BatchType != BatchTypeEnum.REPLENISH_LOT)
        {
            throw new DomainException("Chỉ áp dụng chuyển tiếp độc lập cho thẻ nhánh phụ (REWORK_SUB_CARD hoặc REPLENISH_LOT).", "INVALID_SUB_BATCH_TYPE");
        }

        if (subBatch.BatchStatus == BatchStatusEnum.DONE)
        {
            throw new DomainException("Thẻ nhánh phụ này đã hoàn tất sản xuất, không thể chuyển tiếp thêm.", "SUB_BATCH_ALREADY_DONE");
        }

        var steps = subBatch.BatchRoutingSteps.OrderBy(s => s.StageSequence).ToList();
        var currentStep = steps.FirstOrDefault(s => s.StageSequence == subBatch.CurrentStageSeq);
        if (currentStep == null)
        {
            throw new DomainException($"Không tìm thấy công đoạn #{subBatch.CurrentStageSeq} trong quy trình của thẻ phụ.", "STAGE_NOT_FOUND");
        }

        if (currentStep.StepStatus != StepStatusEnum.PASSED)
        {
            throw new DomainException($"Công đoạn hiện tại ({currentStep.StageName}) chưa được nghiệm thu đạt chuẩn (PASSED). Không được chuyển tiếp độc lập.", "STEP_NOT_PASSED");
        }

        var nextStep = steps.FirstOrDefault(s => s.StageSequence > subBatch.CurrentStageSeq);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            if (nextStep == null)
            {
                // Đã là công đoạn cuối cùng -> Hoàn tất thẻ phụ
                subBatch.BatchStatus = BatchStatusEnum.DONE;
                await _batchRepository.UpdateAsync(subBatch);

                await _bomRepository.AddAuditLogAsync(new AuditLog
                {
                    UserId = currentUserId,
                    ActionType = AuditActionConstants.SubBatchContinued,
                    EntityName = "ProductionBatch",
                    EntityId = subBatch.BatchCode,
                    BeforeState = $"SubCard_{subBatch.BatchCode}_Stage_{subBatch.CurrentStageSeq}_IN_PROGRESS",
                    AfterState = $"SubCard_{subBatch.BatchCode}_DONE",
                    Reason = $"Thẻ nhánh phụ {subBatch.BatchCode} hoàn tất công đoạn đóng thùng cuối cùng độc lập.",
                    CreatedAt = DateTime.UtcNow
                });

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync();

                return new ContinueSubBatchResultDto
                {
                    SubBatchId = subBatch.BatchId,
                    SubBatchCode = subBatch.BatchCode,
                    NextStageSeq = subBatch.CurrentStageSeq,
                    NextStageName = currentStep.StageName,
                    BatchQty = subBatch.BatchQty,
                    BatchStatus = subBatch.BatchStatus.ToString(),
                    Message = $"Thẻ nhánh phụ {subBatch.BatchCode} đã hoàn tất công đoạn cuối cùng thành công (DONE)."
                };
            }

            // Chuyển sang công đoạn tiếp theo
            subBatch.CurrentStageSeq = nextStep.StageSequence;
            nextStep.StepStatus = StepStatusEnum.IN_PROGRESS;
            await _batchRepository.UpdateStepAsync(nextStep);
            await _batchRepository.UpdateAsync(subBatch);

            await _bomRepository.AddAuditLogAsync(new AuditLog
            {
                UserId = currentUserId,
                ActionType = AuditActionConstants.SubBatchContinued,
                EntityName = "ProductionBatch",
                EntityId = subBatch.BatchCode,
                BeforeState = $"SubCard_{subBatch.BatchCode}_Stage_{currentStep.StageSequence}",
                AfterState = $"SubCard_{subBatch.BatchCode}_Stage_{nextStep.StageSequence}_IN_PROGRESS",
                Reason = $"Thẻ nhánh phụ {subBatch.BatchCode} chuyển tiếp độc lập sang {nextStep.StageName}. Ghi chú: {request?.Notes ?? "Điều độ phân xưởng"}.",
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync();

            return new ContinueSubBatchResultDto
            {
                SubBatchId = subBatch.BatchId,
                SubBatchCode = subBatch.BatchCode,
                NextStageSeq = nextStep.StageSequence,
                NextStageName = nextStep.StageName,
                BatchQty = subBatch.BatchQty,
                BatchStatus = subBatch.BatchStatus.ToString(),
                Message = $"Thẻ nhánh phụ {subBatch.BatchCode} đã chuyển tiếp độc lập sang công đoạn #{nextStep.StageSequence} ({nextStep.StageName}) thành công."
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    private static string InferQualityGateName(int seq, string stageName, bool isHoldPoint)
    {
        if (isHoldPoint) return "QC Hold Point Gate (100% Inspection bắt buộc)";
        var lower = (stageName ?? string.Empty).ToLowerInvariant();
        if (lower.Contains("carton") || lower.Contains("xuất xưởng") || lower.Contains("đóng gói"))
            return "Final Packing QC Gate (100% Gate)";
        return "In-Process Self-Check";
    }

    private static string InferInspectionCriteria(int seq, string stageName, bool isHoldPoint)
    {
        if (isHoldPoint)
            return "Độ vuông góc chéo sai lệch <= 1.0mm, keo ghép mộng ngấu 100%, kết cấu chịu lực đạt chuẩn.";
        var lower = (stageName ?? string.Empty).ToLowerInvariant();
        if (lower.Contains("pha phôi") || lower.Contains("cắt"))
            return "Độ ẩm gỗ tự nhiên < 12%, kích thước phôi sai lệch không quá ±1.0mm.";
        if (lower.Contains("bào") || lower.Contains("nhám"))
            return "Bề mặt phẳng đều, không vết lẹm dao bào, độ nhám Ra <= 3.2µm.";
        if (lower.Contains("khoan") || lower.Contains("mộng"))
            return "Mộng âm dương khít chặt (khe hở <= 0.2mm), tim lỗ cam chốt chuẩn theo bản vẽ CAD.";
        if (lower.Contains("sơn") || lower.Contains("pu"))
            return "Màng sơn phủ bóng đều màu, không da cam, không bọt khí rộp.";
        if (lower.Contains("carton") || lower.Contains("xuất xưởng"))
            return "Kiểm tra ngoại quan 100% không trầy xước, dán tem phụ kiện và barcode đạt chuẩn.";
        return "Kiểm tra chất lượng bán thành phẩm theo tiêu chuẩn kỹ thuật xưởng mộc.";
    }

    private static List<RoutingStage> GetStandardWoodworkingStages()
    {
        return new List<RoutingStage>
        {
            new() { StageSequence = 1, StageName = "1. Pha phôi gỗ thô", IsHoldPoint = false },
            new() { StageSequence = 2, StageName = "2. Cắt/Xẻ định hình chi tiết", IsHoldPoint = false },
            new() { StageSequence = 3, StageName = "3. Bào 4 mặt & Chà nhám phẳng mịn", IsHoldPoint = false },
            new() { StageSequence = 4, StageName = "4. Khoan lỗ chốt & Phay mộng âm dương", IsHoldPoint = false },
            new() { StageSequence = 5, StageName = "5. Lắp ráp kết cấu & Vào mộng dán keo ép vam", IsHoldPoint = true },
            new() { StageSequence = 6, StageName = "6. Phun sơn lót PU & Sơn phủ hoàn thiện", IsHoldPoint = false },
            new() { StageSequence = 7, StageName = "7. Kiểm tra xuất xưởng KCS & Đóng thùng carton", IsHoldPoint = false }
        };
    }
}

