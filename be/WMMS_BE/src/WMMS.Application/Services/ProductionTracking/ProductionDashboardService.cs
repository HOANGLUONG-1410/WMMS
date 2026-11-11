using WMMS.Application.DTOs.ProductionTracking;
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

    public ProductionDashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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
