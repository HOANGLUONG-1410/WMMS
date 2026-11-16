using WMMS.Application.DTOs.ProductionTracking;
using WMMS.Application.Interfaces.External;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;
using WMMS.Domain.Entities.KanbanQc;
using WMMS.Domain.Enums;
using WMMS.Domain.Exceptions;

namespace WMMS.Application.Services.ProductionTracking;

/// <summary>
/// Dịch vụ Dashboard Tổng quan Sản xuất cho Chủ xưởng &amp; Báo cáo Đối chiếu Hao hụt Gỗ Thực tế so với Định mức BOM (Iteration 3).
/// Cung cấp số liệu thời gian thực về WIP theo từng trạm công đoạn, cảnh báo đơn hàng trễ hạn ETD, tỷ lệ thu hồi thành phẩm (Yield Rate)
/// và phân tích chênh lệch giữa tỷ lệ hao hụt thực tế tại xưởng so với định mức hao hụt tự nhiên đã duyệt trong BOM (`ScrapRatePercent`).
/// </summary>
public class ProductionDashboardService : IProductionDashboardService
{
    private const int DefaultEtdWarningDays = 5;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductionReportExcelExporter _excelExporter;

    public ProductionDashboardService(
        IUnitOfWork unitOfWork,
        IProductionReportExcelExporter excelExporter)
    {
        _unitOfWork = unitOfWork;
        _excelExporter = excelExporter;
    }

    /// <inheritdoc />
    public async Task<ProductionOverviewDashboardDto> GetProductionOverviewDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        var orders = await _unitOfWork.OrderItems.GetAllOrdersWithProgressGraphAsync(cancellationToken);
        var allBatches = await _unitOfWork.ProductionBatches.GetAllActiveBatchesAsync(null, cancellationToken);
        var warningDays = await GetEtdWarningDaysAsync();

        var orderProgressList = orders
            .Select(o => OrderProgressService.BuildOrderProgressDto(o, warningDays))
            .OrderBy(o => o.EtdDate)
            .ThenBy(o => o.OrderId)
            .ToList();

        var totalCompletedOrders = orderProgressList.Count(o => o.OrderStatus == OrderStatusEnum.COMPLETED.ToString());
        var totalInProductionOrders = orderProgressList.Count(o => o.OrderStatus == OrderStatusEnum.IN_PRODUCTION.ToString());
        var totalActiveOrders = orderProgressList.Count(o => o.OrderStatus != OrderStatusEnum.COMPLETED.ToString());
        var overdueOrdersCount = orderProgressList.Count(o => o.EtdRiskLevel == "OVERDUE");
        var warningOrdersCount = orderProgressList.Count(o => o.EtdRiskLevel == "WARNING");

        var totalBatches = allBatches.Count;
        var doneBatchesCount = allBatches.Count(b => b.BatchStatus == BatchStatusEnum.DONE);
        var blockedBatchesCount = allBatches.Count(b => b.BatchStatus == BatchStatusEnum.HOLD_POINT_BLOCKED);
        var activeBatchesCount = allBatches.Count(b => b.BatchStatus != BatchStatusEnum.DONE);
        var reworkAndReplenishCount = allBatches.Count(b =>
            b.BatchType == BatchTypeEnum.REWORK_SUB_CARD ||
            b.BatchType == BatchTypeEnum.REPLENISH_LOT);

        // Tính tỷ lệ thu hồi đạt chuẩn (Yield Rate) và tỷ lệ lỗi (Defect Rate) trên các thẻ chính (MAIN_CARD)
        var mainBatches = allBatches.Where(b => b.BatchType == BatchTypeEnum.MAIN_CARD).ToList();
        var totalInitialMainUnits = mainBatches.Sum(GetInitialBatchQty);
        var totalDefectsRecorded = allBatches
            .SelectMany(b => b.BatchRoutingSteps)
            .Sum(s => s.QcInspections.Count > 0 ? s.QcInspections.Sum(q => q.FailQty) : s.DefectQty);

        decimal overallDefectRate = 0m;
        decimal overallYieldRate = 100m;

        if (totalInitialMainUnits > 0)
        {
            overallDefectRate = Math.Min(100m, Math.Round((decimal)totalDefectsRecorded * 100m / totalInitialMainUnits, 2));
            overallYieldRate = Math.Max(0m, Math.Round(100m - overallDefectRate, 2));
        }
        else if (allBatches.Count == 0)
        {
            overallYieldRate = 0m;
            overallDefectRate = 0m;
        }

        // Thống kê mật độ WIP theo từng trạm công đoạn
        var wipByStage = allBatches
            .Where(b => b.BatchStatus != BatchStatusEnum.DONE)
            .GroupBy(GetCurrentStageLabel)
            .Select(g => new StageWipSummaryDto
            {
                StageCode = g.Key,
                ActiveBatchesCount = g.Count(),
                BlockedBatchesCount = g.Count(b => b.BatchStatus == BatchStatusEnum.HOLD_POINT_BLOCKED),
                TotalUnitsInStage = g.Sum(b => b.BatchQty)
            })
            .OrderByDescending(s => s.BlockedBatchesCount)
            .ThenByDescending(s => s.TotalUnitsInStage)
            .ThenBy(s => s.StageCode)
            .ToList();

        return new ProductionOverviewDashboardDto
        {
            TotalActiveOrders = totalActiveOrders,
            TotalCompletedOrders = totalCompletedOrders,
            TotalInProductionOrders = totalInProductionOrders,
            OverdueOrdersCount = overdueOrdersCount,
            EtdWarningOrdersCount = warningOrdersCount,
            TotalBatches = totalBatches,
            ActiveBatchesCount = activeBatchesCount,
            DoneBatchesCount = doneBatchesCount,
            HoldPointBlockedBatchesCount = blockedBatchesCount,
            ReworkAndReplenishBatchesCount = reworkAndReplenishCount,
            OverallYieldRatePercent = overallYieldRate,
            OverallDefectRatePercent = overallDefectRate,
            WorkInProgressByStage = wipByStage,
            PriorityOrders = orderProgressList
                .Where(o => o.OrderStatus != OrderStatusEnum.COMPLETED.ToString())
                .Take(10)
                .ToList()
        };
    }

    /// <inheritdoc />
    public async Task<ScrapVarianceReportDto> GetScrapVarianceReportAsync(
        long? orderId = null,
        CancellationToken cancellationToken = default)
    {
        var orders = await _unitOfWork.OrderItems.GetAllOrdersWithProgressGraphAsync(cancellationToken);
        if (orderId.HasValue)
        {
            var singleOrder = orders.FirstOrDefault(o => o.OrderId == orderId.Value)
                              ?? await _unitOfWork.OrderItems.GetOrderWithFullProgressGraphAsync(orderId.Value, cancellationToken);
            if (singleOrder == null)
            {
                throw AppException.NotFound(
                    $"Không tìm thấy đơn hàng với mã định danh ID = {orderId.Value} để phân tích hao hụt.");
            }

            orders = new List<Domain.Entities.Orders.CustomerOrder> { singleOrder };
        }

        var itemVariances = new List<OrderItemScrapVarianceDto>();

        foreach (var order in orders)
        {
            foreach (var item in order.OrderItems.OrderBy(oi => oi.OrderItemId))
            {
                // Tính định mức hao hụt tự nhiên đã duyệt trong BOM (BR-002: BomHeader.ScrapRatePercent lưu dạng % ví dụ 3.00m)
                decimal plannedScrapPercent = 0m;
                if (item.BomHeader != null)
                {
                    var rawScrap = item.BomHeader.ScrapRatePercent;
                    plannedScrapPercent = rawScrap > 0m && rawScrap < 1m
                        ? Math.Round(rawScrap * 100m, 2)
                        : Math.Round(rawScrap, 2);
                }

                var batches = item.ProductionBatches.ToList();
                var mainBatches = batches.Where(b => b.BatchType == BatchTypeEnum.MAIN_CARD).ToList();
                var totalInitialQty = mainBatches.Count > 0
                    ? mainBatches.Sum(GetInitialBatchQty)
                    : item.OrderedQty;

                var totalGoodCompletedQty = batches
                    .Where(b => b.BatchStatus == BatchStatusEnum.DONE)
                    .Sum(b => b.BatchQty);

                var totalDefectQty = batches
                    .SelectMany(b => b.BatchRoutingSteps)
                    .Sum(s => s.QcInspections.Count > 0 ? s.QcInspections.Sum(q => q.FailQty) : s.DefectQty);

                var totalReworkQty = batches
                    .Where(b => b.BatchType == BatchTypeEnum.REWORK_SUB_CARD)
                    .Sum(GetInitialBatchQty);

                var totalReplenishScrapQty = batches
                    .Where(b => b.BatchType == BatchTypeEnum.REPLENISH_LOT)
                    .Sum(GetInitialBatchQty);

                var irrecoverableScrapQty = Math.Max(totalReplenishScrapQty, Math.Max(0, totalDefectQty - totalReworkQty));

                decimal actualDefectRatePercent = totalInitialQty > 0
                    ? Math.Round((decimal)totalDefectQty * 100m / totalInitialQty, 2)
                    : 0m;

                decimal actualIrrecoverableScrapRatePercent = totalInitialQty > 0
                    ? Math.Round((decimal)irrecoverableScrapQty * 100m / totalInitialQty, 2)
                    : 0m;

                var scrapVariancePercent = Math.Round(actualDefectRatePercent - plannedScrapPercent, 2);
                var isExceeding = actualDefectRatePercent > plannedScrapPercent;

                string evaluation;
                if (batches.Count == 0)
                {
                    evaluation = "Chưa phát hành lô sản xuất (chưa phát sinh dữ liệu hao hụt thực tế).";
                }
                else if (isExceeding)
                {
                    evaluation = $"CẢNH BÁO VƯỢT ĐỊNH MỨC: Tỷ lệ lỗi/hao hụt thực tế ({actualDefectRatePercent}%) vượt định mức BOM ({plannedScrapPercent}%) là +{scrapVariancePercent}%.";
                }
                else
                {
                    evaluation = $"TRONG NGƯỠNG AN TOÀN: Tỷ lệ lỗi/hao hụt thực tế ({actualDefectRatePercent}%) nằm trong định mức hao hụt BOM ({plannedScrapPercent}%).";
                }

                itemVariances.Add(new OrderItemScrapVarianceDto
                {
                    OrderItemId = item.OrderItemId,
                    OrderId = order.OrderId,
                    PoNumber = order.PoNumber,
                    ProductCode = item.Product?.ProductSku ?? string.Empty,
                    ProductName = item.Product?.ProductName ?? string.Empty,
                    OrderedQty = item.OrderedQty,
                    TotalInitialBatchQty = totalInitialQty,
                    TotalGoodCompletedQty = totalGoodCompletedQty,
                    TotalDefectQty = totalDefectQty,
                    TotalReworkQty = totalReworkQty,
                    TotalScrappedQty = irrecoverableScrapQty,
                    PlannedBomScrapRatePercent = plannedScrapPercent,
                    ActualDefectRatePercent = actualDefectRatePercent,
                    ActualIrrecoverableScrapRatePercent = actualIrrecoverableScrapRatePercent,
                    ScrapVariancePercent = scrapVariancePercent,
                    IsExceedingPlannedScrap = isExceeding,
                    VarianceEvaluation = evaluation
                });
            }
        }

        var avgPlanned = itemVariances.Count > 0
            ? Math.Round(itemVariances.Average(x => x.PlannedBomScrapRatePercent), 2)
            : 0m;
        var avgActual = itemVariances.Count > 0
            ? Math.Round(itemVariances.Average(x => x.ActualDefectRatePercent), 2)
            : 0m;

        return new ScrapVarianceReportDto
        {
            GeneratedAt = DateTime.UtcNow,
            TotalAnalyzedOrderItems = itemVariances.Count,
            AveragePlannedBomScrapRatePercent = avgPlanned,
            AverageActualDefectScrapRatePercent = avgActual,
            OverallScrapVariancePercent = Math.Round(avgActual - avgPlanned, 2),
            ExceededThresholdItemsCount = itemVariances.Count(x => x.IsExceedingPlannedScrap),
            Items = itemVariances
        };
    }

    /// <inheritdoc />
    public async Task<DefectAnalyticsReportDto> GetDefectAnalyticsReportAsync(
        QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var activeFilter = await ValidateAndNormalizeFilterAsync(filter, cancellationToken);

        var defectLogs = await _unitOfWork.QcInspections.GetAllDefectLogsWithAnalyticsGraphAsync(
            activeFilter.FromDate,
            activeFilter.ToDate,
            activeFilter.OrderId,
            cancellationToken);

        if (activeFilter.StageSequence.HasValue)
        {
            defectLogs = defectLogs
                .Where(dl => dl.QcInspection?.BatchRoutingStep?.StageSequence == activeFilter.StageSequence.Value)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(activeFilter.WoodType))
        {
            var woodKeyword = activeFilter.WoodType.Trim();
            defectLogs = defectLogs
                .Where(dl => ResolveWoodType(dl).Contains(woodKeyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var totalEvents = defectLogs.Count;
        var totalDefectUnits = defectLogs.Sum(dl => dl.DefectQty);
        var totalReworkUnits = defectLogs
            .Where(dl => dl.Disposition == DefectDispositionEnum.REWORK_SUB_CARD)
            .Sum(dl => dl.DefectQty);
        var totalScrapUnits = defectLogs
            .Where(dl => dl.Disposition == DefectDispositionEnum.SCRAP_REPLENISH)
            .Sum(dl => dl.DefectQty);

        decimal reworkShare = totalDefectUnits > 0
            ? Math.Round((decimal)totalReworkUnits * 100m / totalDefectUnits, 2)
            : 0m;
        decimal scrapShare = totalDefectUnits > 0
            ? Math.Round((decimal)totalScrapUnits * 100m / totalDefectUnits, 2)
            : 0m;

        // 1. Thống kê lỗi theo Trạm máy / Công đoạn (Defects by Station)
        var byStation = defectLogs
            .GroupBy(dl =>
            {
                var step = dl.QcInspection?.BatchRoutingStep;
                var seq = step?.StageSequence ?? 0;
                var name = !string.IsNullOrWhiteSpace(step?.StageName)
                    ? step!.StageName.Trim()
                    : (!string.IsNullOrWhiteSpace(dl.DefectCatalog?.StandardStage)
                        ? dl.DefectCatalog!.StandardStage!.Trim()
                        : $"Trạm công đoạn #{seq}");
                return new { StageSequence = seq, StationName = name };
            })
            .Select(g =>
            {
                var stationDefectQty = g.Sum(x => x.DefectQty);
                var stationReworkQty = g.Where(x => x.Disposition == DefectDispositionEnum.REWORK_SUB_CARD).Sum(x => x.DefectQty);
                var stationScrapQty = g.Where(x => x.Disposition == DefectDispositionEnum.SCRAP_REPLENISH).Sum(x => x.DefectQty);

                var breakdown = g
                    .GroupBy(x => x.DefectCode)
                    .Select(cg =>
                    {
                        var codeQty = cg.Sum(x => x.DefectQty);
                        var first = cg.First();
                        var defectName = !string.IsNullOrWhiteSpace(first.DefectName)
                            ? first.DefectName
                            : (first.DefectCatalog?.DefectName ?? cg.Key);
                        return new StationDefectCodeBreakdownDto
                        {
                            DefectCode = cg.Key,
                            DefectName = defectName,
                            OccurrencesCount = cg.Count(),
                            DefectQty = codeQty,
                            ShareInStationPercent = stationDefectQty > 0
                                ? Math.Round((decimal)codeQty * 100m / stationDefectQty, 2)
                                : 0m
                        };
                    })
                    .OrderByDescending(b => b.DefectQty)
                    .ThenByDescending(b => b.OccurrencesCount)
                    .ThenBy(b => b.DefectCode)
                    .ToList();

                var topCode = breakdown.FirstOrDefault();
                var topCatalog = g.FirstOrDefault(x => x.DefectCode == topCode?.DefectCode)?.DefectCatalog;

                return new StationDefectAnalyticsDto
                {
                    StageSequence = g.Key.StageSequence,
                    StationName = g.Key.StationName,
                    TotalDefectEventsCount = g.Count(),
                    TotalDefectQty = stationDefectQty,
                    ReworkQty = stationReworkQty,
                    ScrapReplenishQty = stationScrapQty,
                    ShareOfTotalDefectsPercent = totalDefectUnits > 0
                        ? Math.Round((decimal)stationDefectQty * 100m / totalDefectUnits, 2)
                        : 0m,
                    TopDefectCode = topCode?.DefectCode ?? string.Empty,
                    TopDefectName = topCode?.DefectName ?? string.Empty,
                    RootCauseSummary = topCatalog?.RootCause ?? "Cần kiểm tra cữ máy, độ ẩm phôi gỗ và thao tác vận hành tại trạm.",
                    RecommendedAction = topCatalog?.RecommendedAction ?? "Hiệu chuẩn lại thiết bị gia công và kiểm tra KCS 100% đầu chuyền.",
                    DefectBreakdown = breakdown
                };
            })
            .OrderByDescending(s => s.TotalDefectQty)
            .ThenByDescending(s => s.TotalDefectEventsCount)
            .ThenBy(s => s.StageSequence)
            .ToList();

        // 2. Thống kê lỗi theo Loại gỗ (Defects by Wood Type)
        var byWoodType = defectLogs
            .GroupBy(ResolveWoodType)
            .Select(g =>
            {
                var woodDefectQty = g.Sum(x => x.DefectQty);
                var woodReworkQty = g.Where(x => x.Disposition == DefectDispositionEnum.REWORK_SUB_CARD).Sum(x => x.DefectQty);
                var woodScrapQty = g.Where(x => x.Disposition == DefectDispositionEnum.SCRAP_REPLENISH).Sum(x => x.DefectQty);

                var topDefect = g
                    .GroupBy(x => new
                    {
                        x.DefectCode,
                        Name = !string.IsNullOrWhiteSpace(x.DefectName) ? x.DefectName : (x.DefectCatalog?.DefectName ?? x.DefectCode)
                    })
                    .OrderByDescending(cg => cg.Sum(x => x.DefectQty))
                    .ThenByDescending(cg => cg.Count())
                    .FirstOrDefault();

                var topStation = g
                    .GroupBy(x => x.QcInspection?.BatchRoutingStep?.StageName ?? x.DefectCatalog?.StandardStage ?? "Trạm gia công")
                    .OrderByDescending(sg => sg.Sum(x => x.DefectQty))
                    .FirstOrDefault();

                var distinctProducts = g
                    .Select(x => x.QcInspection?.BatchRoutingStep?.ProductionBatch?.OrderItem?.ProductId ?? 0)
                    .Where(pid => pid > 0)
                    .Distinct()
                    .Count();

                return new WoodTypeDefectAnalyticsDto
                {
                    WoodType = g.Key,
                    TotalDefectEventsCount = g.Count(),
                    TotalDefectQty = woodDefectQty,
                    ReworkQty = woodReworkQty,
                    ScrapReplenishQty = woodScrapQty,
                    ShareOfTotalDefectsPercent = totalDefectUnits > 0
                        ? Math.Round((decimal)woodDefectQty * 100m / totalDefectUnits, 2)
                        : 0m,
                    AffectedProductsCount = distinctProducts,
                    MostCommonDefectCode = topDefect?.Key.DefectCode ?? string.Empty,
                    MostCommonDefectName = topDefect?.Key.Name ?? string.Empty,
                    MostProblematicStationName = topStation?.Key ?? string.Empty
                };
            })
            .OrderByDescending(w => w.TotalDefectQty)
            .ThenByDescending(w => w.TotalDefectEventsCount)
            .ThenBy(w => w.WoodType)
            .ToList();

        // 3. Thống kê Pareto theo mã lỗi chuẩn (Defects by Catalog Code)
        var byCatalogCode = defectLogs
            .GroupBy(dl => dl.DefectCode)
            .Select(g =>
            {
                var qty = g.Sum(x => x.DefectQty);
                var first = g.First();
                var catalog = first.DefectCatalog;
                return new DefectCatalogSummaryDto
                {
                    DefectCode = g.Key,
                    DefectName = !string.IsNullOrWhiteSpace(first.DefectName)
                        ? first.DefectName
                        : (catalog?.DefectName ?? g.Key),
                    StandardStage = catalog?.StandardStage ?? (first.QcInspection?.BatchRoutingStep?.StageName ?? string.Empty),
                    RootCause = catalog?.RootCause ?? string.Empty,
                    RecommendedAction = catalog?.RecommendedAction ?? string.Empty,
                    OccurrencesCount = g.Count(),
                    TotalDefectQty = qty,
                    ShareOfTotalDefectsPercent = totalDefectUnits > 0
                        ? Math.Round((decimal)qty * 100m / totalDefectUnits, 2)
                        : 0m
                };
            })
            .OrderByDescending(c => c.TotalDefectQty)
            .ThenByDescending(c => c.OccurrencesCount)
            .ThenBy(c => c.DefectCode)
            .ToList();

        var topStationItem = byStation.FirstOrDefault();
        var topWoodItem = byWoodType.FirstOrDefault();

        string assessment = totalDefectUnits == 0
            ? "Không phát sinh lỗi chất lượng KCS nào trong phạm vi truy vấn. Toàn bộ các trạm gia công đạt chuẩn 100%."
            : $"Phát hiện tổng cộng {totalDefectUnits} SP lỗi qua {totalEvents} lượt ghi nhận KCS (Sửa chữa Rework: {totalReworkUnits} SP [{reworkShare}%], Hủy bù phôi: {totalScrapUnits} SP [{scrapShare}%]). " +
              $"Trạm máy phát sinh lỗi cao nhất: '{topStationItem?.StationName}' ({topStationItem?.TotalDefectQty} SP - chiếm {topStationItem?.ShareOfTotalDefectsPercent}%, lỗi chính: {topStationItem?.TopDefectName}). " +
              $"Loại gỗ phát sinh lỗi nhiều nhất: '{topWoodItem?.WoodType}' ({topWoodItem?.TotalDefectQty} SP).";

        return new DefectAnalyticsReportDto
        {
            GeneratedAt = DateTime.UtcNow,
            TotalDefectEventsCount = totalEvents,
            TotalDefectUnits = totalDefectUnits,
            TotalReworkUnits = totalReworkUnits,
            TotalScrapReplenishUnits = totalScrapUnits,
            ReworkSharePercent = reworkShare,
            ScrapSharePercent = scrapShare,
            TopDefectStationName = topStationItem?.StationName ?? string.Empty,
            TopDefectStationQty = topStationItem?.TotalDefectQty ?? 0,
            TopDefectStationSharePercent = topStationItem?.ShareOfTotalDefectsPercent ?? 0m,
            TopDefectWoodType = topWoodItem?.WoodType ?? string.Empty,
            TopDefectWoodQty = topWoodItem?.TotalDefectQty ?? 0,
            SummaryAssessment = assessment,
            DefectsByStation = byStation,
            DefectsByWoodType = byWoodType,
            DefectsByCatalogCode = byCatalogCode
        };
    }

    /// <inheritdoc />
    public async Task<OverdriveAnalyticsReportDto> GetOverdriveAnalyticsReportAsync(
        QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var activeFilter = await ValidateAndNormalizeFilterAsync(filter, cancellationToken);

        var overdriveLogs = await _unitOfWork.QcInspections.GetAllOverdriveLogsWithAnalyticsGraphAsync(
            activeFilter.FromDate,
            activeFilter.ToDate,
            activeFilter.OrderId,
            cancellationToken);

        if (activeFilter.StageSequence.HasValue)
        {
            overdriveLogs = overdriveLogs
                .Where(ol => ol.HoldPointStageSeq == activeFilter.StageSequence.Value)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(activeFilter.WoodType))
        {
            var woodKeyword = activeFilter.WoodType.Trim();
            overdriveLogs = overdriveLogs
                .Where(ol => (ol.ProductionBatch?.OrderItem?.Product?.WoodSpecies ?? string.Empty)
                    .Contains(woodKeyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var allBatches = activeFilter.OrderId.HasValue
            ? await _unitOfWork.ProductionBatches.GetByOrderIdAsync(activeFilter.OrderId.Value, cancellationToken)
            : await _unitOfWork.ProductionBatches.GetAllActiveBatchesAsync(null, cancellationToken);

        var totalSystemBatchesCount = allBatches.Count;
        var distinctOverdrivenBatchIds = overdriveLogs
            .Select(ol => ol.BatchId)
            .Union(allBatches.Where(b => b.IsOverdriver).Select(b => b.BatchId))
            .Distinct()
            .ToList();

        var totalOverdrivenBatchesCount = activeFilter.StageSequence.HasValue || !string.IsNullOrWhiteSpace(activeFilter.WoodType) || activeFilter.FromDate.HasValue || activeFilter.ToDate.HasValue
            ? overdriveLogs.Select(ol => ol.BatchId).Distinct().Count()
            : distinctOverdrivenBatchIds.Count;

        var totalApprovals = overdriveLogs.Count;
        decimal overdrivenBatchRate = totalSystemBatchesCount > 0
            ? Math.Round((decimal)totalOverdrivenBatchesCount * 100m / totalSystemBatchesCount, 2)
            : 0m;

        var affectedOrdersCount = overdriveLogs
            .Select(ol => ol.ProductionBatch?.OrderItem?.OrderId ?? 0)
            .Where(oid => oid > 0)
            .Distinct()
            .Count();

        // 1. Thống kê theo Quản lý / Chủ xưởng phê duyệt (By Approver)
        var byApprover = overdriveLogs
            .GroupBy(ol => ol.ApprovedBy)
            .Select(g =>
            {
                var approver = g.First().Approver;
                var count = g.Count();
                return new OverdriveByApproverDto
                {
                    ApproverUserId = g.Key,
                    ApproverUsername = approver?.Username ?? $"user_{g.Key}",
                    ApproverFullName = !string.IsNullOrWhiteSpace(approver?.FullName)
                        ? approver!.FullName
                        : (approver?.Username ?? $"User #{g.Key}"),
                    TotalApprovalsCount = count,
                    DistinctBatchesCount = g.Select(x => x.BatchId).Distinct().Count(),
                    SharePercent = totalApprovals > 0
                        ? Math.Round((decimal)count * 100m / totalApprovals, 2)
                        : 0m,
                    LastApprovedAt = g.Max(x => x.ApprovedAt)
                };
            })
            .OrderByDescending(a => a.TotalApprovalsCount)
            .ThenBy(a => a.ApproverUserId)
            .ToList();

        // 2. Thống kê theo Tổ trưởng / Kế hoạch gửi yêu cầu (By Requester)
        var byRequester = overdriveLogs
            .GroupBy(ol => ol.RequestedBy)
            .Select(g =>
            {
                var requester = g.First().Requester;
                var count = g.Count();
                return new OverdriveByRequesterDto
                {
                    RequesterUserId = g.Key,
                    RequesterUsername = requester?.Username ?? $"user_{g.Key}",
                    RequesterFullName = !string.IsNullOrWhiteSpace(requester?.FullName)
                        ? requester!.FullName
                        : (requester?.Username ?? $"User #{g.Key}"),
                    TotalRequestsApprovedCount = count,
                    DistinctBatchesCount = g.Select(x => x.BatchId).Distinct().Count(),
                    SharePercent = totalApprovals > 0
                        ? Math.Round((decimal)count * 100m / totalApprovals, 2)
                        : 0m
                };
            })
            .OrderByDescending(r => r.TotalRequestsApprovedCount)
            .ThenBy(r => r.RequesterUserId)
            .ToList();

        // 3. Thống kê theo Trạm dừng kỹ thuật Hold Point (By Hold Point Stage)
        var byHoldPointStage = overdriveLogs
            .GroupBy(ol => ol.HoldPointStageSeq)
            .Select(g =>
            {
                var count = g.Count();
                var stageName = g
                    .Select(ol => ResolveHoldPointStageName(ol.ProductionBatch, g.Key))
                    .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                    ?? $"Trạm Hold Point #{g.Key}";

                return new OverdriveByHoldPointStageDto
                {
                    HoldPointStageSeq = g.Key,
                    StageName = stageName,
                    OverdriveCount = count,
                    DistinctBatchesCount = g.Select(x => x.BatchId).Distinct().Count(),
                    SharePercent = totalApprovals > 0
                        ? Math.Round((decimal)count * 100m / totalApprovals, 2)
                        : 0m,
                    SampleJustifications = g
                        .Select(x => x.Justification)
                        .Where(j => !string.IsNullOrWhiteSpace(j))
                        .Distinct()
                        .Take(5)
                        .ToList()
                };
            })
            .OrderByDescending(s => s.OverdriveCount)
            .ThenByDescending(s => s.DistinctBatchesCount)
            .ThenBy(s => s.HoldPointStageSeq)
            .ToList();

        var topStage = byHoldPointStage.FirstOrDefault();
        var topApprover = byApprover.FirstOrDefault();

        // 4. Danh sách chi tiết các lần phá khóa gần nhất
        var recentLogs = overdriveLogs
            .Take(activeFilter.TopN)
            .Select(ol => new OverdriveEventItemDto
            {
                OverdriveId = ol.OverdriveId,
                BatchId = ol.BatchId,
                BatchCode = ol.ProductionBatch?.BatchCode ?? string.Empty,
                OrderId = ol.ProductionBatch?.OrderItem?.OrderId ?? 0,
                PoNumber = ol.ProductionBatch?.OrderItem?.CustomerOrder?.PoNumber ?? string.Empty,
                ProductCode = ol.ProductionBatch?.OrderItem?.Product?.ProductSku ?? string.Empty,
                ProductName = ol.ProductionBatch?.OrderItem?.Product?.ProductName ?? string.Empty,
                HoldPointStageSeq = ol.HoldPointStageSeq,
                StageName = ResolveHoldPointStageName(ol.ProductionBatch, ol.HoldPointStageSeq),
                RequestedBy = ol.RequestedBy,
                RequesterName = ol.Requester?.FullName ?? ol.Requester?.Username ?? $"User #{ol.RequestedBy}",
                ApprovedBy = ol.ApprovedBy,
                ApproverName = ol.Approver?.FullName ?? ol.Approver?.Username ?? $"User #{ol.ApprovedBy}",
                Justification = ol.Justification,
                ApprovedAt = ol.ApprovedAt
            })
            .ToList();

        string governanceEvaluation = totalApprovals == 0
            ? "Không phát sinh trường hợp phá khóa ngoại lệ (Overdriver) nào tại các trạm Hold Point. Vật tư và phụ kiện được cung ứng đúng tiến độ chuẩn."
            : $"Ghi nhận {totalApprovals} lần Quản lý/Chủ xưởng phê duyệt phá khóa ngoại lệ Hold Point trên {totalOverdrivenBatchesCount} lô sản xuất (chiếm {overdrivenBatchRate}% tổng số lô). " +
              $"Trạm dừng bị phá khóa nhiều nhất: '{topStage?.StageName}' ({topStage?.OverdriveCount} lần). " +
              $"Người phê duyệt nhiều nhất: '{topApprover?.ApproverFullName}' ({topApprover?.TotalApprovalsCount} lần).";

        return new OverdriveAnalyticsReportDto
        {
            GeneratedAt = DateTime.UtcNow,
            TotalOverdriveApprovalsCount = totalApprovals,
            TotalOverdrivenBatchesCount = totalOverdrivenBatchesCount,
            TotalSystemBatchesCount = totalSystemBatchesCount,
            OverdrivenBatchRatePercent = overdrivenBatchRate,
            AffectedOrdersCount = affectedOrdersCount,
            MostOverdrivenHoldPointSeq = topStage?.HoldPointStageSeq,
            MostOverdrivenHoldPointName = topStage?.StageName ?? string.Empty,
            MostOverdrivenStageCount = topStage?.OverdriveCount ?? 0,
            GovernanceEvaluation = governanceEvaluation,
            ByApprover = byApprover,
            ByRequester = byRequester,
            ByHoldPointStage = byHoldPointStage,
            RecentOverdriveLogs = recentLogs
        };
    }

    /// <inheritdoc />
    public async Task<QualityAndOverdriveAnalyticsDto> GetQualityAndOverdriveSummaryAsync(
        QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var defectReport = await GetDefectAnalyticsReportAsync(filter, cancellationToken);
        var overdriveReport = await GetOverdriveAnalyticsReportAsync(filter, cancellationToken);

        return new QualityAndOverdriveAnalyticsDto
        {
            GeneratedAt = DateTime.UtcNow,
            DefectAnalytics = defectReport,
            OverdriveAnalytics = overdriveReport
        };
    }

    /// <inheritdoc />
    public async Task<ExcelExportFileDto> ExportProductionStatisticsExcelAsync(
        QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var activeFilter = await ValidateAndNormalizeFilterAsync(filter, cancellationToken);
        var overview = await GetProductionOverviewDashboardAsync(cancellationToken);
        var scrapReport = await GetScrapVarianceReportAsync(activeFilter.OrderId, cancellationToken);
        var orderProgressList = await BuildOrderProgressListForExportAsync(activeFilter.OrderId, cancellationToken);

        return _excelExporter.ExportProductionStatisticsExcel(overview, orderProgressList, scrapReport);
    }

    /// <inheritdoc />
    public async Task<ExcelExportFileDto> ExportQualityInspectionExcelAsync(
        QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var activeFilter = await ValidateAndNormalizeFilterAsync(filter, cancellationToken);
        var defectAnalytics = await GetDefectAnalyticsReportAsync(activeFilter, cancellationToken);
        var inspectionRows = await BuildQcInspectionExportRowsAsync(activeFilter, cancellationToken);
        var defectRows = await BuildDefectLogExportRowsAsync(activeFilter, cancellationToken);

        return _excelExporter.ExportQualityInspectionSheetExcel(inspectionRows, defectRows, defectAnalytics);
    }

    /// <inheritdoc />
    public async Task<ExcelExportFileDto> ExportWorkshopDispatchHistoryExcelAsync(
        QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var activeFilter = await ValidateAndNormalizeFilterAsync(filter, cancellationToken);
        var overdriveAnalytics = await GetOverdriveAnalyticsReportAsync(activeFilter, cancellationToken);
        var dispatchRows = await BuildWorkshopDispatchExportRowsAsync(activeFilter, cancellationToken);

        return _excelExporter.ExportWorkshopDispatchHistoryExcel(dispatchRows, overdriveAnalytics);
    }

    /// <inheritdoc />
    public async Task<ExcelExportFileDto> ExportComprehensiveWorkshopWorkbookExcelAsync(
        QualityAnalyticsFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var activeFilter = await ValidateAndNormalizeFilterAsync(filter, cancellationToken);
        var overview = await GetProductionOverviewDashboardAsync(cancellationToken);
        var orderProgressList = await BuildOrderProgressListForExportAsync(activeFilter.OrderId, cancellationToken);
        var scrapReport = await GetScrapVarianceReportAsync(activeFilter.OrderId, cancellationToken);
        var defectAnalytics = await GetDefectAnalyticsReportAsync(activeFilter, cancellationToken);
        var inspectionRows = await BuildQcInspectionExportRowsAsync(activeFilter, cancellationToken);
        var defectRows = await BuildDefectLogExportRowsAsync(activeFilter, cancellationToken);
        var dispatchRows = await BuildWorkshopDispatchExportRowsAsync(activeFilter, cancellationToken);
        var overdriveAnalytics = await GetOverdriveAnalyticsReportAsync(activeFilter, cancellationToken);

        return _excelExporter.ExportComprehensiveWorkshopWorkbookExcel(
            overview,
            orderProgressList,
            scrapReport,
            inspectionRows,
            defectRows,
            defectAnalytics,
            dispatchRows,
            overdriveAnalytics);
    }

    private async Task<List<OrderProductionProgressDto>> BuildOrderProgressListForExportAsync(
        long? orderId,
        CancellationToken cancellationToken)
    {
        var orders = await _unitOfWork.OrderItems.GetAllOrdersWithProgressGraphAsync(cancellationToken);
        if (orderId.HasValue)
        {
            orders = orders.Where(o => o.OrderId == orderId.Value).ToList();
        }

        var warningDays = await GetEtdWarningDaysAsync();
        return orders
            .Select(o => OrderProgressService.BuildOrderProgressDto(o, warningDays))
            .OrderBy(o => o.EtdDate)
            .ThenBy(o => o.OrderId)
            .ToList();
    }

    private async Task<List<QcInspectionStageExportRowDto>> BuildQcInspectionExportRowsAsync(
        QualityAnalyticsFilterDto activeFilter,
        CancellationToken cancellationToken)
    {
        var inspections = await _unitOfWork.QcInspections.GetAllInspectionsWithFullGraphAsync(
            activeFilter.FromDate,
            activeFilter.ToDate,
            activeFilter.OrderId,
            cancellationToken);

        if (activeFilter.StageSequence.HasValue)
        {
            inspections = inspections
                .Where(qi => qi.BatchRoutingStep?.StageSequence == activeFilter.StageSequence.Value)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(activeFilter.WoodType))
        {
            var woodKeyword = activeFilter.WoodType.Trim();
            inspections = inspections
                .Where(qi => (qi.BatchRoutingStep?.ProductionBatch?.OrderItem?.Product?.WoodSpecies ?? "Gỗ tự nhiên tiêu chuẩn")
                    .Contains(woodKeyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return inspections.Select(qi =>
        {
            var step = qi.BatchRoutingStep;
            var batch = step?.ProductionBatch;
            var orderItem = batch?.OrderItem;
            var product = orderItem?.Product;
            var defectSummary = qi.DefectLogs.Count > 0
                ? string.Join("; ", qi.DefectLogs.Select(dl => $"{dl.DefectCode}: {dl.DefectName} ({dl.DefectQty} SP - {dl.Disposition})"))
                : "Đạt chuẩn 100%";

            return new QcInspectionStageExportRowDto
            {
                QcId = qi.QcId,
                BatchCode = batch?.BatchCode ?? string.Empty,
                BatchType = batch?.BatchType.ToString() ?? string.Empty,
                PoNumber = orderItem?.CustomerOrder?.PoNumber ?? string.Empty,
                ProductCode = product?.ProductSku ?? string.Empty,
                ProductName = product?.ProductName ?? string.Empty,
                WoodType = !string.IsNullOrWhiteSpace(product?.WoodSpecies) ? product!.WoodSpecies! : "Gỗ tự nhiên tiêu chuẩn",
                StageSequence = step?.StageSequence ?? 0,
                StageName = step?.StageName ?? string.Empty,
                IsHoldPoint = step?.IsHoldPoint ?? false,
                InspectedQty = qi.InspectedQty,
                PassQty = qi.PassQty,
                FailQty = qi.FailQty,
                ResultStatus = qi.ResultStatus.ToString(),
                InspectorName = qi.Inspector?.FullName ?? qi.Inspector?.Username ?? $"KCS #{qi.InspectorId}",
                InspectedAt = qi.InspectedAt,
                DefectSummary = defectSummary
            };
        }).ToList();
    }

    private async Task<List<DefectLogExportRowDto>> BuildDefectLogExportRowsAsync(
        QualityAnalyticsFilterDto activeFilter,
        CancellationToken cancellationToken)
    {
        var defectLogs = await _unitOfWork.QcInspections.GetAllDefectLogsWithAnalyticsGraphAsync(
            activeFilter.FromDate,
            activeFilter.ToDate,
            activeFilter.OrderId,
            cancellationToken);

        if (activeFilter.StageSequence.HasValue)
        {
            defectLogs = defectLogs
                .Where(dl => dl.QcInspection?.BatchRoutingStep?.StageSequence == activeFilter.StageSequence.Value)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(activeFilter.WoodType))
        {
            var woodKeyword = activeFilter.WoodType.Trim();
            defectLogs = defectLogs
                .Where(dl => ResolveWoodType(dl).Contains(woodKeyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return defectLogs.Select(dl =>
        {
            var qi = dl.QcInspection;
            var step = qi?.BatchRoutingStep;
            var batch = step?.ProductionBatch;
            var orderItem = batch?.OrderItem;
            return new DefectLogExportRowDto
            {
                DefectLogId = dl.DefectLogId,
                QcId = dl.QcId,
                BatchCode = batch?.BatchCode ?? string.Empty,
                PoNumber = orderItem?.CustomerOrder?.PoNumber ?? string.Empty,
                ProductCode = orderItem?.Product?.ProductSku ?? string.Empty,
                ProductName = orderItem?.Product?.ProductName ?? string.Empty,
                WoodType = ResolveWoodType(dl),
                StageSequence = step?.StageSequence ?? 0,
                StageName = step?.StageName ?? (dl.DefectCatalog?.StandardStage ?? string.Empty),
                DefectCode = dl.DefectCode,
                DefectName = !string.IsNullOrWhiteSpace(dl.DefectName) ? dl.DefectName : (dl.DefectCatalog?.DefectName ?? dl.DefectCode),
                DefectQty = dl.DefectQty,
                Disposition = dl.Disposition.ToString(),
                SpawnedSubBatchCode = dl.SpawnedSubBatch?.BatchCode ?? string.Empty,
                InspectorName = qi?.Inspector?.FullName ?? qi?.Inspector?.Username ?? string.Empty,
                InspectedAt = qi?.InspectedAt ?? DateTime.UtcNow
            };
        }).ToList();
    }

    private async Task<List<WorkshopDispatchExportRowDto>> BuildWorkshopDispatchExportRowsAsync(
        QualityAnalyticsFilterDto activeFilter,
        CancellationToken cancellationToken)
    {
        var batches = activeFilter.OrderId.HasValue
            ? await _unitOfWork.ProductionBatches.GetByOrderIdAsync(activeFilter.OrderId.Value, cancellationToken)
            : await _unitOfWork.ProductionBatches.GetAllActiveBatchesAsync(null, cancellationToken);

        if (activeFilter.StageSequence.HasValue)
        {
            batches = batches.Where(b => b.CurrentStageSeq == activeFilter.StageSequence.Value).ToList();
        }

        if (!string.IsNullOrWhiteSpace(activeFilter.WoodType))
        {
            var woodKeyword = activeFilter.WoodType.Trim();
            batches = batches
                .Where(b => (b.OrderItem?.Product?.WoodSpecies ?? string.Empty)
                    .Contains(woodKeyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return batches
            .OrderBy(b => b.OrderItem?.CustomerOrder?.EtdDate ?? DateTime.MaxValue)
            .ThenBy(b => b.BatchId)
            .Select(b =>
            {
                var steps = b.BatchRoutingSteps.OrderBy(s => s.StageSequence).ToList();
                var timeline = steps.Count > 0
                    ? string.Join(" -> ", steps.Select(s => $"[{s.StageSequence}.{s.StageName}: {s.StepStatus} ({s.PassedQty}/{s.TargetQty})]"))
                    : "Chưa khởi tạo bước công đoạn";

                return new WorkshopDispatchExportRowDto
                {
                    BatchId = b.BatchId,
                    BatchCode = b.BatchCode,
                    BatchType = b.BatchType.ToString(),
                    ParentBatchCode = b.ParentBatch?.BatchCode ?? string.Empty,
                    PoNumber = b.OrderItem?.CustomerOrder?.PoNumber ?? string.Empty,
                    CustomerName = b.OrderItem?.CustomerOrder?.Customer?.CompanyName ?? string.Empty,
                    EtdDate = b.OrderItem?.CustomerOrder?.EtdDate ?? DateTime.UtcNow.Date,
                    ProductCode = b.OrderItem?.Product?.ProductSku ?? string.Empty,
                    ProductName = b.OrderItem?.Product?.ProductName ?? string.Empty,
                    InitialQty = GetInitialBatchQty(b),
                    CurrentQty = b.BatchQty,
                    CurrentStageSeq = b.CurrentStageSeq,
                    CurrentStageName = GetCurrentStageLabel(b),
                    BatchStatus = b.BatchStatus.ToString(),
                    IsOverdriver = b.IsOverdriver,
                    HoldPointStageSeq = b.HoldPointStageSeq,
                    GoodsIssuedUnlocked = b.GoodsIssuedUnlocked,
                    PassedStagesCount = steps.Count(s => s.StepStatus == StepStatusEnum.PASSED),
                    TotalStagesCount = steps.Count,
                    RoutingTimelineSummary = timeline,
                    CreatedAt = b.CreatedAt
                };
            })
            .ToList();
    }

    private async Task<QualityAnalyticsFilterDto> ValidateAndNormalizeFilterAsync(
        QualityAnalyticsFilterDto? filter,
        CancellationToken cancellationToken)
    {
        var activeFilter = filter ?? new QualityAnalyticsFilterDto();
        if (activeFilter.TopN <= 0)
        {
            activeFilter.TopN = 20;
        }

        var validator = new Validators.ProductionTracking.QualityAnalyticsFilterValidator();
        var validationResult = await validator.ValidateAsync(activeFilter, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new FluentValidation.ValidationException(validationResult.Errors);
        }

        if (activeFilter.OrderId.HasValue)
        {
            var existingOrder = await _unitOfWork.OrderItems.GetOrderByIdAsync(activeFilter.OrderId.Value, cancellationToken);
            if (existingOrder == null)
            {
                throw AppException.NotFound(
                    $"Không tìm thấy đơn hàng với mã định danh ID = {activeFilter.OrderId.Value} để thống kê chất lượng & Overdriver.");
            }
        }

        return activeFilter;
    }

    private static string ResolveWoodType(DefectLog dl)
    {
        var orderItem = dl.QcInspection?.BatchRoutingStep?.ProductionBatch?.OrderItem;
        var woodSpecies = orderItem?.Product?.WoodSpecies;
        if (!string.IsNullOrWhiteSpace(woodSpecies))
        {
            return woodSpecies.Trim();
        }

        var lumberMaterial = orderItem?.BomHeader?.BomDetails?
            .Select(bd => bd.Material)
            .FirstOrDefault(m => m != null && m.MaterialGroup == MaterialGroupEnum.LUMBER);

        if (!string.IsNullOrWhiteSpace(lumberMaterial?.MaterialName))
        {
            return lumberMaterial!.MaterialName.Trim();
        }

        return "Gỗ tự nhiên tiêu chuẩn";
    }

    private static string ResolveHoldPointStageName(ProductionBatch? batch, int holdPointSeq)
    {
        var stepName = batch?.BatchRoutingSteps?
            .FirstOrDefault(s => s.StageSequence == holdPointSeq)?.StageName;

        if (!string.IsNullOrWhiteSpace(stepName))
        {
            return stepName.Trim();
        }

        return $"Trạm Hold Point #{holdPointSeq}";
    }

    private static int GetInitialBatchQty(ProductionBatch batch)
    {
        var firstStepTarget = batch.BatchRoutingSteps
            .OrderBy(s => s.StageSequence)
            .FirstOrDefault()?.TargetQty ?? 0;

        return firstStepTarget > 0 ? firstStepTarget : batch.BatchQty;
    }

    private static string GetCurrentStageLabel(ProductionBatch batch)
    {
        var currentStep = batch.BatchRoutingSteps
            .OrderBy(s => s.StageSequence)
            .FirstOrDefault(s => s.StageSequence == batch.CurrentStageSeq);

        if (!string.IsNullOrWhiteSpace(currentStep?.StageName))
        {
            return currentStep.StageName;
        }

        return $"STAGE_{batch.CurrentStageSeq}";
    }

    private async Task<int> GetEtdWarningDaysAsync()
    {
        var parsedDays = await _unitOfWork.SystemConfigs.GetIntConfigAsync(SystemConfigKeys.EtdWarningDays, DefaultEtdWarningDays);
        return parsedDays > 0 ? parsedDays : DefaultEtdWarningDays;
    }
}
