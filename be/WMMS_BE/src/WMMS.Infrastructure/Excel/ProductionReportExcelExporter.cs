using System.Drawing;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using WMMS.Application.DTOs.ProductionTracking;
using WMMS.Application.Interfaces.External;

namespace WMMS.Infrastructure.Excel;

/// <summary>
/// [BE-Iter 3] Thực thi bộ xuất báo cáo Excel chuẩn xưởng gỗ (<c>.xlsx</c>) bằng thư viện EPPlus (SC-26 / Báo cáo &amp; Lưu trữ Nghiệm thu):
/// <para>
/// - Chức năng (4 chế độ xuất tệp <c>.xlsx</c>):
///   1. <see cref="ExportProductionStatisticsExcel"/>: 2 Sheets (<c>1_TongKetTienDoDonHang</c>, <c>2_DoiChieuHaoHutBOM</c>) — Thống kê KPI sản xuất, tiến độ từng PO/SKU và đối chiếu hao hụt thực tế so với định mức BOM.
///   2. <see cref="ExportQualityInspectionSheetExcel"/>: 2 Sheets (<c>1_PhieuKiemDinhCongDoan</c>, <c>2_NhatKyLoi_PhanTichTram</c>) — Phiếu kiểm định KCS từng công đoạn, phân tích lỗi theo Trạm máy, theo Loại gỗ (<c>DefectsByWoodType</c>) và nhật ký lỗi chi tiết.
///   3. <see cref="ExportWorkshopDispatchHistoryExcel"/>: 2 Sheets (<c>1_LichSuDieuDoKanban</c>, <c>2_NhatKyPhaKhoaHoldPoint</c>) — Lộ trình thẻ lô Kanban (Main/Rework/Replenish) và thống kê tần suất phá khóa Overdriver theo Người duyệt, Trạm dừng &amp; Nhật ký chi tiết.
///   4. <see cref="ExportComprehensiveWorkshopWorkbookExcel"/>: Sổ tay Nghiệm thu Tổng hợp Toàn Xưởng gồm trọn bộ 6 Sheets trên trong một tệp <c>.xlsx</c> duy nhất.
/// - Móc nối: Được gọi trực tiếp bởi <c>ProductionDashboardService</c> thông qua interface <see cref="IProductionReportExcelExporter"/> và trả mảng byte <c>.xlsx</c> về <c>ProductionDashboardController</c>.
/// </para>
/// </summary>
public class ProductionReportExcelExporter : IProductionReportExcelExporter
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <inheritdoc />
    public ExcelExportFileDto ExportProductionStatisticsExcel(
        ProductionOverviewDashboardDto overview,
        IReadOnlyList<OrderProductionProgressDto> orderProgressList,
        ScrapVarianceReportDto scrapVarianceReport)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();

        BuildOrderProgressSheet(package, "1_TongKetTienDoDonHang", overview, orderProgressList);
        BuildScrapVarianceSheet(package, "2_DoiChieuHaoHutBOM", scrapVarianceReport);

        return new ExcelExportFileDto
        {
            FileContents = package.GetAsByteArray(),
            FileName = $"WMMS_BaoCao_ThongKe_SanXuat_TienDo_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx",
            ContentType = ExcelContentType
        };
    }

    /// <inheritdoc />
    public ExcelExportFileDto ExportQualityInspectionSheetExcel(
        IReadOnlyList<QcInspectionStageExportRowDto> inspectionRows,
        IReadOnlyList<DefectLogExportRowDto> defectRows,
        DefectAnalyticsReportDto defectAnalytics)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();

        BuildQcInspectionStageSheet(package, "1_PhieuKiemDinhCongDoan", inspectionRows, defectAnalytics);
        BuildDefectLogAndStationAnalyticsSheet(package, "2_NhatKyLoi_PhanTichTram", defectRows, defectAnalytics);

        return new ExcelExportFileDto
        {
            FileContents = package.GetAsByteArray(),
            FileName = $"WMMS_PhieuKiemDinh_KCS_NghiemThu_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx",
            ContentType = ExcelContentType
        };
    }

    /// <inheritdoc />
    public ExcelExportFileDto ExportWorkshopDispatchHistoryExcel(
        IReadOnlyList<WorkshopDispatchExportRowDto> dispatchRows,
        OverdriveAnalyticsReportDto overdriveAnalytics)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();

        BuildWorkshopDispatchKanbanSheet(package, "1_LichSuDieuDoKanban", dispatchRows);
        BuildOverdriveHistoryAndAnalyticsSheet(package, "2_NhatKyPhaKhoaHoldPoint", overdriveAnalytics);

        return new ExcelExportFileDto
        {
            FileContents = package.GetAsByteArray(),
            FileName = $"WMMS_LichSu_DieuDo_Kanban_Overdriver_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx",
            ContentType = ExcelContentType
        };
    }

    /// <inheritdoc />
    public ExcelExportFileDto ExportComprehensiveWorkshopWorkbookExcel(
        ProductionOverviewDashboardDto overview,
        IReadOnlyList<OrderProductionProgressDto> orderProgressList,
        ScrapVarianceReportDto scrapVarianceReport,
        IReadOnlyList<QcInspectionStageExportRowDto> inspectionRows,
        IReadOnlyList<DefectLogExportRowDto> defectRows,
        DefectAnalyticsReportDto defectAnalytics,
        IReadOnlyList<WorkshopDispatchExportRowDto> dispatchRows,
        OverdriveAnalyticsReportDto overdriveAnalytics)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();

        BuildOrderProgressSheet(package, "1_TongKetTienDoDonHang", overview, orderProgressList);
        BuildScrapVarianceSheet(package, "2_DoiChieuHaoHutBOM", scrapVarianceReport);
        BuildQcInspectionStageSheet(package, "3_PhieuKiemDinhCongDoan", inspectionRows, defectAnalytics);
        BuildDefectLogAndStationAnalyticsSheet(package, "4_NhatKyLoi_PhanTichTram", defectRows, defectAnalytics);
        BuildWorkshopDispatchKanbanSheet(package, "5_LichSuDieuDoKanban", dispatchRows);
        BuildOverdriveHistoryAndAnalyticsSheet(package, "6_NhatKyPhaKhoaHoldPoint", overdriveAnalytics);

        return new ExcelExportFileDto
        {
            FileContents = package.GetAsByteArray(),
            FileName = $"WMMS_SoTay_NghiemThu_SanXuat_ToanXuong_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx",
            ContentType = ExcelContentType
        };
    }

    private static void BuildOrderProgressSheet(
        ExcelPackage package,
        string sheetName,
        ProductionOverviewDashboardDto overview,
        IReadOnlyList<OrderProductionProgressDto> orderProgressList)
    {
        var ws = package.Workbook.Worksheets.Add(sheetName);
        WriteBannerTitle(
            ws,
            "BÁO CÁO TỔNG KẾT TIẾN ĐỘ ĐƠN HÀNG & THỐNG KÊ ĐIỀU HÀNH SẢN XUẤT XƯỞNG GỖ (WMMS)",
            $"Ngày kết xuất: {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC | Tỷ lệ thu hồi đạt chuẩn (Yield Rate): {overview.OverallYieldRatePercent}% | Tỷ lệ lỗi: {overview.OverallDefectRatePercent}%",
            12);

        // Khối KPI Tổng quan (Dòng 4-5)
        var kpiHeaders = new[]
        {
            "Đơn đang chạy", "Đơn hoàn tất", "Đơn trễ ETD", "Đơn sắp tới ETD",
            "Tổng số lô Kanban", "Lô đang chạy", "Lô hoàn tất (DONE)", "Lô khóa Hold Point",
            "Thẻ phụ 3% Rework/Bù", "Tỷ lệ đạt chuẩn (%)", "Tỷ lệ lỗi (%)", "Số trạm có WIP"
        };
        var kpiValues = new object[]
        {
            overview.TotalActiveOrders, overview.TotalCompletedOrders, overview.OverdueOrdersCount, overview.EtdWarningOrdersCount,
            overview.TotalBatches, overview.ActiveBatchesCount, overview.DoneBatchesCount, overview.HoldPointBlockedBatchesCount,
            overview.ReworkAndReplenishBatchesCount, overview.OverallYieldRatePercent, overview.OverallDefectRatePercent, overview.WorkInProgressByStage.Count
        };

        WriteHeaderRow(ws, 4, kpiHeaders, Color.FromArgb(47, 85, 151));
        for (int c = 0; c < kpiValues.Length; c++)
        {
            ws.Cells[5, c + 1].Value = kpiValues[c];
            ws.Cells[5, c + 1].Style.Font.Bold = true;
            ws.Cells[5, c + 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }
        ApplyGridBorders(ws, 4, 5, 1, kpiHeaders.Length);

        // Khối Bảng chi tiết tiến độ đơn hàng & dòng sản phẩm (Từ dòng 7)
        string[] headers =
        {
            "STT", "Mã PO#", "Không gian (Order Space)", "Khách hàng", "Ngày ETD",
            "Còn lại (Ngày)", "Cảnh báo ETD", "Mã SKU", "Tên sản phẩm",
            "SL Đặt hàng", "SL Hoàn thành", "Tiến độ (%)"
        };
        WriteHeaderRow(ws, 7, headers, Color.FromArgb(31, 78, 120));

        int row = 8;
        int stt = 1;
        if (orderProgressList.Count == 0)
        {
            WriteEmptyStateRow(ws, row, headers.Length, "(Không có đơn hàng sản xuất nào khớp với bộ lọc hiện tại)");
            row++;
        }
        else
        {
            foreach (var order in orderProgressList)
            {
                var spaceDisplay = order.SpaceId.HasValue ? $"SPACE-{order.SpaceId.Value}" : "-";
                var etdRiskVi = FormatEtdRiskVi(order.EtdRiskLevel);
                if (order.Items.Count == 0)
                {
                    ws.Cells[row, 1].Value = stt++;
                    ws.Cells[row, 2].Value = order.PoNumber;
                    ws.Cells[row, 3].Value = spaceDisplay;
                    ws.Cells[row, 4].Value = order.CustomerName;
                    ws.Cells[row, 5].Value = order.EtdDate.ToString("yyyy-MM-dd");
                    ws.Cells[row, 6].Value = order.DaysUntilEtd;
                    ws.Cells[row, 7].Value = etdRiskVi;
                    ws.Cells[row, 8].Value = "-";
                    ws.Cells[row, 9].Value = "(Chưa có dòng sản phẩm)";
                    ws.Cells[row, 10].Value = order.TotalOrderedUnits;
                    ws.Cells[row, 11].Value = order.TotalCompletedUnits;
                    ws.Cells[row, 12].Value = order.OverallCompletionPercent;
                    row++;
                    continue;
                }

                foreach (var item in order.Items)
                {
                    ws.Cells[row, 1].Value = stt++;
                    ws.Cells[row, 2].Value = order.PoNumber;
                    ws.Cells[row, 3].Value = spaceDisplay;
                    ws.Cells[row, 4].Value = order.CustomerName;
                    ws.Cells[row, 5].Value = order.EtdDate.ToString("yyyy-MM-dd");
                    ws.Cells[row, 6].Value = order.DaysUntilEtd;
                    ws.Cells[row, 7].Value = etdRiskVi;
                    ws.Cells[row, 8].Value = item.ProductCode;
                    ws.Cells[row, 9].Value = item.ProductName;
                    ws.Cells[row, 10].Value = item.OrderedQty;
                    ws.Cells[row, 11].Value = item.CompletedQty;
                    ws.Cells[row, 12].Value = item.CompletionPercent;
                    row++;
                }
            }
        }

        if (row > 8)
        {
            ApplyGridBorders(ws, 7, row - 1, 1, headers.Length);
        }
        AutoFitColumnsSafe(ws, headers.Length);
    }

    private static void BuildScrapVarianceSheet(
        ExcelPackage package,
        string sheetName,
        ScrapVarianceReportDto scrapVarianceReport)
    {
        var ws = package.Workbook.Worksheets.Add(sheetName);
        WriteBannerTitle(
            ws,
            "BÁO CÁO ĐỐI CHIẾU TỶ LỆ HAO HỤT THỰC TẾ SO VỚI ĐỊNH MỨC VẬT TƯ",
            $"Tổng dòng sản phẩm phân tích: {scrapVarianceReport.TotalAnalyzedOrderItems} | Hao hụt BOM TB: {scrapVarianceReport.AveragePlannedBomScrapRatePercent}% | Hao hụt thực tế TB: {scrapVarianceReport.AverageActualDefectScrapRatePercent}% | Số mục vượt ngưỡng: {scrapVarianceReport.ExceededThresholdItemsCount}",
            12);

        string[] headers =
        {
            "STT", "Mã PO#", "Mã SKU", "Tên sản phẩm", "SL Đặt hàng",
            "SL Phôi ban đầu", "SL Thành phẩm đạt", "SL Lỗi KCS", "SL Sửa chữa Rework",
            "SL Hủy bù phôi", "Định mức BOM (%)", "Hao hụt thực tế (%) & Đánh giá"
        };
        WriteHeaderRow(ws, 4, headers, Color.FromArgb(55, 86, 35));

        int row = 5;
        int stt = 1;
        if (scrapVarianceReport.Items.Count == 0)
        {
            WriteEmptyStateRow(ws, row, headers.Length, "(Không có dữ liệu đối chiếu hao hụt BOM nào khớp với bộ lọc hiện tại)");
            row++;
        }
        else
        {
            foreach (var item in scrapVarianceReport.Items)
            {
                ws.Cells[row, 1].Value = stt++;
                ws.Cells[row, 2].Value = item.PoNumber;
                ws.Cells[row, 3].Value = item.ProductCode;
                ws.Cells[row, 4].Value = item.ProductName;
                ws.Cells[row, 5].Value = item.OrderedQty;
                ws.Cells[row, 6].Value = item.TotalInitialBatchQty;
                ws.Cells[row, 7].Value = item.TotalGoodCompletedQty;
                ws.Cells[row, 8].Value = item.TotalDefectQty;
                ws.Cells[row, 9].Value = item.TotalReworkQty;
                ws.Cells[row, 10].Value = item.TotalScrappedQty;
                ws.Cells[row, 11].Value = item.PlannedBomScrapRatePercent;
                ws.Cells[row, 12].Value = $"{item.ActualDefectRatePercent}% (Chênh lệch: {item.ScrapVariancePercent}%) - {item.VarianceEvaluation}";
                row++;
            }
        }

        if (row > 5)
        {
            ApplyGridBorders(ws, 4, row - 1, 1, headers.Length);
        }
        AutoFitColumnsSafe(ws, headers.Length);
    }

    private static void BuildQcInspectionStageSheet(
        ExcelPackage package,
        string sheetName,
        IReadOnlyList<QcInspectionStageExportRowDto> inspectionRows,
        DefectAnalyticsReportDto defectAnalytics)
    {
        var ws = package.Workbook.Worksheets.Add(sheetName);
        WriteBannerTitle(
            ws,
            "PHIẾU KIỂM ĐỊNH CHẤT LƯỢNG KCS 100% TỪNG CÔNG ĐOẠN SẢN XUẤT",
            $"Tổng biên bản KCS: {inspectionRows.Count} | Tổng SP lỗi ghi nhận: {defectAnalytics.TotalDefectUnits} SP (Rework: {defectAnalytics.TotalReworkUnits} SP, Hủy bù phôi: {defectAnalytics.TotalScrapReplenishUnits} SP)",
            14);

        string[] headers =
        {
            "Mã KCS", "Mã Lô Kanban", "Loại Thẻ", "Mã PO#", "Mã SKU",
            "Tên Sản phẩm", "Loại Gỗ", "Thứ tự Trạm", "Tên Công đoạn / Trạm máy",
            "Hold Point", "SL Kiểm định (100%)", "SL Đạt (Pass)", "SL Lỗi (Fail)",
            "Kết quả & Người nghiệm thu"
        };
        WriteHeaderRow(ws, 4, headers, Color.FromArgb(31, 78, 120));

        int row = 5;
        if (inspectionRows.Count == 0)
        {
            WriteEmptyStateRow(ws, row, headers.Length, "(Không có phiếu kiểm định KCS nào khớp với bộ lọc hiện tại)");
            row++;
        }
        else
        {
            foreach (var item in inspectionRows)
            {
                ws.Cells[row, 1].Value = item.QcId;
                ws.Cells[row, 2].Value = item.BatchCode;
                ws.Cells[row, 3].Value = FormatBatchTypeVi(item.BatchType);
                ws.Cells[row, 4].Value = item.PoNumber;
                ws.Cells[row, 5].Value = item.ProductCode;
                ws.Cells[row, 6].Value = item.ProductName;
                ws.Cells[row, 7].Value = item.WoodType;
                ws.Cells[row, 8].Value = item.StageSequence;
                ws.Cells[row, 9].Value = item.StageName;
                ws.Cells[row, 10].Value = item.IsHoldPoint ? "CÓ (Hold Point)" : "Không";
                ws.Cells[row, 11].Value = item.InspectedQty;
                ws.Cells[row, 12].Value = item.PassQty;
                ws.Cells[row, 13].Value = item.FailQty;
                ws.Cells[row, 14].Value = $"{FormatQcResultVi(item.ResultStatus)} | KCS: {item.InspectorName} ({item.InspectedAt:yyyy-MM-dd HH:mm}) {(string.IsNullOrWhiteSpace(item.DefectSummary) ? "" : "- " + item.DefectSummary)}";
                row++;
            }
        }

        if (row > 5)
        {
            ApplyGridBorders(ws, 4, row - 1, 1, headers.Length);
        }

        // Khung chữ ký nghiệm thu lưu trữ phân xưởng gỗ
        int sigRow = row + 2;
        ws.Cells[sigRow, 2].Value = "NGƯỜI LẬP BIỂU / KCS";
        ws.Cells[sigRow, 2].Style.Font.Bold = true;
        ws.Cells[sigRow, 7].Value = "TỔ TRƯỞNG SẢN XUẤT";
        ws.Cells[sigRow, 7].Style.Font.Bold = true;
        ws.Cells[sigRow, 12].Value = "CHỦ XƯỞNG / QUẢN LÝ NGHIỆM THU";
        ws.Cells[sigRow, 12].Style.Font.Bold = true;

        AutoFitColumnsSafe(ws, headers.Length);
    }

    private static void BuildDefectLogAndStationAnalyticsSheet(
        ExcelPackage package,
        string sheetName,
        IReadOnlyList<DefectLogExportRowDto> defectRows,
        DefectAnalyticsReportDto defectAnalytics)
    {
        var ws = package.Workbook.Worksheets.Add(sheetName);
        WriteBannerTitle(
            ws,
            "BẢNG PHÂN TÍCH LỖI CHẤT LƯỢNG THEO TRẠM MÁY / LOẠI GỖ & NHẬT KÝ TÁCH NHÁNH 97%-3%",
            defectAnalytics.SummaryAssessment,
            11);

        // Khối 1: Thống kê theo Trạm máy
        WriteSectionTitle(ws, 4, 11, "1. PHÂN TÍCH LỖI CHẤT LƯỢNG THEO TRẠM MÁY / CÔNG ĐOẠN", Color.FromArgb(252, 228, 214));
        string[] stationHeaders =
        {
            "Thứ tự Trạm", "Tên Trạm máy / Công đoạn", "Số lượt lỗi", "Tổng SP lỗi",
            "SL Rework (3%)", "SL Hủy bù phôi", "Tỷ trọng lỗi (%)", "Mã lỗi phổ biến nhất",
            "Tên lỗi chính", "Nguyên nhân gốc rễ (Root Cause)", "Khuyến nghị khắc phục"
        };
        WriteHeaderRow(ws, 5, stationHeaders, Color.FromArgb(131, 60, 12));

        int row = 6;
        if (defectAnalytics.DefectsByStation.Count == 0)
        {
            WriteEmptyStateRow(ws, row, stationHeaders.Length, "(Không ghi nhận lỗi theo trạm máy nào trong phạm vi lọc)");
            row++;
        }
        else
        {
            foreach (var st in defectAnalytics.DefectsByStation)
            {
                ws.Cells[row, 1].Value = st.StageSequence;
                ws.Cells[row, 2].Value = st.StationName;
                ws.Cells[row, 3].Value = st.TotalDefectEventsCount;
                ws.Cells[row, 4].Value = st.TotalDefectQty;
                ws.Cells[row, 5].Value = st.ReworkQty;
                ws.Cells[row, 6].Value = st.ScrapReplenishQty;
                ws.Cells[row, 7].Value = st.ShareOfTotalDefectsPercent;
                ws.Cells[row, 8].Value = st.TopDefectCode;
                ws.Cells[row, 9].Value = st.TopDefectName;
                ws.Cells[row, 10].Value = st.RootCauseSummary;
                ws.Cells[row, 11].Value = st.RecommendedAction;
                row++;
            }
        }
        ApplyGridBorders(ws, 5, row - 1, 1, stationHeaders.Length);

        // Khối 2: Thống kê lỗi theo Loại gỗ nguyên liệu
        int woodSectionRow = row + 2;
        WriteSectionTitle(ws, woodSectionRow, 11, "2. PHÂN TÍCH LỖI CHẤT LƯỢNG THEO LOẠI GỖ NGUYÊN LIỆU", Color.FromArgb(226, 239, 218));
        int woodHeaderRow = woodSectionRow + 1;
        string[] woodHeaders =
        {
            "STT", "Loại gỗ nguyên liệu", "Số lượt lỗi", "Tổng SP lỗi",
            "SL Rework (3%)", "SL Hủy bù phôi", "Tỷ trọng lỗi (%)", "Số SP ảnh hưởng",
            "Mã lỗi phổ biến", "Tên lỗi chính", "Trạm phát sinh nhiều nhất"
        };
        WriteHeaderRow(ws, woodHeaderRow, woodHeaders, Color.FromArgb(55, 86, 35));

        int wRow = woodHeaderRow + 1;
        int wStt = 1;
        if (defectAnalytics.DefectsByWoodType.Count == 0)
        {
            WriteEmptyStateRow(ws, wRow, woodHeaders.Length, "(Không ghi nhận lỗi theo loại gỗ nào trong phạm vi lọc)");
            wRow++;
        }
        else
        {
            foreach (var wt in defectAnalytics.DefectsByWoodType)
            {
                ws.Cells[wRow, 1].Value = wStt++;
                ws.Cells[wRow, 2].Value = wt.WoodType;
                ws.Cells[wRow, 3].Value = wt.TotalDefectEventsCount;
                ws.Cells[wRow, 4].Value = wt.TotalDefectQty;
                ws.Cells[wRow, 5].Value = wt.ReworkQty;
                ws.Cells[wRow, 6].Value = wt.ScrapReplenishQty;
                ws.Cells[wRow, 7].Value = wt.ShareOfTotalDefectsPercent;
                ws.Cells[wRow, 8].Value = wt.AffectedProductsCount;
                ws.Cells[wRow, 9].Value = wt.MostCommonDefectCode;
                ws.Cells[wRow, 10].Value = wt.MostCommonDefectName;
                ws.Cells[wRow, 11].Value = wt.MostProblematicStationName;
                wRow++;
            }
        }
        ApplyGridBorders(ws, woodHeaderRow, wRow - 1, 1, woodHeaders.Length);

        // Khối 3: Nhật ký chi tiết từng lỗi phát sinh (defect_logs)
        int detailSectionRow = wRow + 2;
        WriteSectionTitle(ws, detailSectionRow, 11, "3. NHẬT KÝ CHI TIẾT LỖI KCS & TÁCH NHÁNH 97%-3%", Color.FromArgb(217, 225, 242));
        int detailHeaderRow = detailSectionRow + 1;
        string[] detailHeaders =
        {
            "Mã Log", "Mã Lô Chính", "Mã PO#", "Sản phẩm", "Loại Gỗ",
            "Trạm Công đoạn", "Mã Lỗi", "Tên Lỗi Kỹ thuật", "SL Lỗi",
            "Phương án Xử lý", "Thẻ Phụ Sinh ra & Thời gian"
        };
        WriteHeaderRow(ws, detailHeaderRow, detailHeaders, Color.FromArgb(47, 85, 151));

        int dRow = detailHeaderRow + 1;
        if (defectRows.Count == 0)
        {
            WriteEmptyStateRow(ws, dRow, detailHeaders.Length, "(Không có nhật ký lỗi chi tiết nào khớp với bộ lọc hiện tại)");
            dRow++;
        }
        else
        {
            foreach (var d in defectRows)
            {
                ws.Cells[dRow, 1].Value = d.DefectLogId;
                ws.Cells[dRow, 2].Value = d.BatchCode;
                ws.Cells[dRow, 3].Value = d.PoNumber;
                ws.Cells[dRow, 4].Value = $"{d.ProductCode} - {d.ProductName}";
                ws.Cells[dRow, 5].Value = d.WoodType;
                ws.Cells[dRow, 6].Value = d.StageName;
                ws.Cells[dRow, 7].Value = d.DefectCode;
                ws.Cells[dRow, 8].Value = d.DefectName;
                ws.Cells[dRow, 9].Value = d.DefectQty;
                ws.Cells[dRow, 10].Value = FormatDispositionVi(d.Disposition);
                var timeLabel = d.InspectedAt == DateTime.MinValue ? "Không rõ thời gian" : $"{d.InspectedAt:yyyy-MM-dd HH:mm}";
                ws.Cells[dRow, 11].Value = $"{(string.IsNullOrWhiteSpace(d.SpawnedSubBatchCode) ? "Không sinh thẻ phụ" : d.SpawnedSubBatchCode)} ({timeLabel})";
                dRow++;
            }
        }
        ApplyGridBorders(ws, detailHeaderRow, dRow - 1, 1, detailHeaders.Length);
        AutoFitColumnsSafe(ws, detailHeaders.Length);
    }

    private static void BuildWorkshopDispatchKanbanSheet(
        ExcelPackage package,
        string sheetName,
        IReadOnlyList<WorkshopDispatchExportRowDto> dispatchRows)
    {
        var ws = package.Workbook.Worksheets.Add(sheetName);
        WriteBannerTitle(
            ws,
            "LỊCH SỬ ĐIỀU ĐỘ PHÂN XƯỞNG & LỘ TRÌNH THẺ LÔ SẢN XUẤT KANBAN",
            $"Tổng số thẻ lô Kanban (Thẻ chính + Thẻ nhánh 3%): {dispatchRows.Count} thẻ | Kết xuất lúc: {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC",
            14);

        string[] headers =
        {
            "Mã Batch ID", "Mã Thẻ Lô Kanban", "Loại Thẻ", "Thẻ Chính Cha", "Mã PO#",
            "Khách hàng", "Ngày ETD", "Mã SKU & Sản phẩm", "SL Ban đầu",
            "SL Hiện tại", "Công đoạn Hiện tại", "Trạng thái Thẻ", "Chế độ Overdriver / Hold Point",
            "Tiến độ Lộ trình Công đoạn"
        };
        WriteHeaderRow(ws, 4, headers, Color.FromArgb(31, 78, 120));

        int row = 5;
        if (dispatchRows.Count == 0)
        {
            WriteEmptyStateRow(ws, row, headers.Length, "(Không có thẻ lô Kanban nào khớp với bộ lọc hiện tại)");
            row++;
        }
        else
        {
            foreach (var b in dispatchRows)
            {
                ws.Cells[row, 1].Value = b.BatchId;
                ws.Cells[row, 2].Value = b.BatchCode;
                ws.Cells[row, 3].Value = FormatBatchTypeVi(b.BatchType);
                ws.Cells[row, 4].Value = string.IsNullOrWhiteSpace(b.ParentBatchCode) ? "-" : b.ParentBatchCode;
                ws.Cells[row, 5].Value = b.PoNumber;
                ws.Cells[row, 6].Value = b.CustomerName;
                ws.Cells[row, 7].Value = b.EtdDate.ToString("yyyy-MM-dd");
                ws.Cells[row, 8].Value = $"{b.ProductCode} - {b.ProductName}";
                ws.Cells[row, 9].Value = b.InitialQty;
                ws.Cells[row, 10].Value = b.CurrentQty;
                ws.Cells[row, 11].Value = $"#{b.CurrentStageSeq} - {b.CurrentStageName}";
                ws.Cells[row, 12].Value = FormatBatchStatusVi(b.BatchStatus);
                ws.Cells[row, 13].Value = b.IsOverdriver
                    ? $"OVERDRIVER (Hold Point #{b.HoldPointStageSeq ?? 5} | Xuất kho: {(b.GoodsIssuedUnlocked ? "Đã mở" : "Chưa mở")})"
                    : "Chuẩn (Đủ vật tư)";
                ws.Cells[row, 14].Value = $"{b.PassedStagesCount}/{b.TotalStagesCount} trạm đạt | {b.RoutingTimelineSummary}";
                row++;
            }
        }

        if (row > 5)
        {
            ApplyGridBorders(ws, 4, row - 1, 1, headers.Length);
        }
        AutoFitColumnsSafe(ws, headers.Length);
    }

    private static void BuildOverdriveHistoryAndAnalyticsSheet(
        ExcelPackage package,
        string sheetName,
        OverdriveAnalyticsReportDto overdriveAnalytics)
    {
        var ws = package.Workbook.Worksheets.Add(sheetName);
        WriteBannerTitle(
            ws,
            "NHẬT KÝ PHÊ DUYỆT PHÁ KHÓA NGOẠI LỆ TRẠM DỪNG KỸ THUẬT (HOLD POINT)",
            overdriveAnalytics.GovernanceEvaluation,
            10);

        // Khối 1: Thống kê tần suất phá khóa theo Người phê duyệt (Chủ xưởng / Quản lý)
        WriteSectionTitle(ws, 4, 10, "1. THỐNG KÊ TẦN SUẤT PHÁ KHÓA THEO NGƯỜI PHÊ DUYỆT & TRẠM HOLD POINT", Color.FromArgb(252, 228, 214));
        string[] approverHeaders =
        {
            "STT", "Mã Người duyệt", "Tài khoản", "Họ tên Quản lý / Chủ xưởng",
            "Số lượt duyệt", "Số lô ảnh hưởng", "Tỷ trọng (%)", "Lần duyệt gần nhất",
            "Trạm Hold Point nóng nhất", "Đánh giá quản trị"
        };
        WriteHeaderRow(ws, 5, approverHeaders, Color.FromArgb(131, 60, 12));

        int aRow = 6;
        int aStt = 1;
        if (overdriveAnalytics.ByApprover.Count == 0)
        {
            WriteEmptyStateRow(ws, aRow, approverHeaders.Length, "(Không có lượt phê duyệt phá khóa Overdriver nào trong phạm vi lọc)");
            aRow++;
        }
        else
        {
            var topStageSummary = !string.IsNullOrWhiteSpace(overdriveAnalytics.MostOverdrivenHoldPointName)
                ? $"#{overdriveAnalytics.MostOverdrivenHoldPointSeq} - {overdriveAnalytics.MostOverdrivenHoldPointName} ({overdriveAnalytics.MostOverdrivenStageCount} lần)"
                : "-";
            foreach (var app in overdriveAnalytics.ByApprover)
            {
                ws.Cells[aRow, 1].Value = aStt++;
                ws.Cells[aRow, 2].Value = app.ApproverUserId;
                ws.Cells[aRow, 3].Value = app.ApproverUsername;
                ws.Cells[aRow, 4].Value = app.ApproverFullName;
                ws.Cells[aRow, 5].Value = app.TotalApprovalsCount;
                ws.Cells[aRow, 6].Value = app.DistinctBatchesCount;
                ws.Cells[aRow, 7].Value = app.SharePercent;
                ws.Cells[aRow, 8].Value = app.LastApprovedAt.HasValue ? app.LastApprovedAt.Value.ToString("yyyy-MM-dd HH:mm") : "-";
                ws.Cells[aRow, 9].Value = topStageSummary;
                ws.Cells[aRow, 10].Value = app.TotalApprovalsCount >= 3 ? "Tần suất cao - cần rà soát cung ứng vật tư" : "Trong ngưỡng kiểm soát";
                aRow++;
            }
        }
        ApplyGridBorders(ws, 5, aRow - 1, 1, approverHeaders.Length);

        // Khối 2: Thống kê tần suất phá khóa theo từng Trạm dừng kỹ thuật Hold Point
        int stageSectionRow = aRow + 2;
        WriteSectionTitle(ws, stageSectionRow, 10, "2. THỐNG KÊ TẦN SUẤT PHÁ KHÓA THEO TRẠM DỪNG KỸ THUẬT HOLD POINT", Color.FromArgb(226, 239, 218));
        int stageHeaderRow = stageSectionRow + 1;
        WriteHeaderRow(ws, stageHeaderRow, new[]
        {
            "STT", "Thứ tự Trạm", "Tên Trạm Hold Point", "Số lần phá khóa",
            "Số lô ảnh hưởng", "Tỷ trọng (%)", "Lý do giải trình tiêu biểu", "", "", ""
        }, Color.FromArgb(55, 86, 35));
        ws.Cells[stageHeaderRow, 7, stageHeaderRow, 10].Merge = true;

        int sRow = stageHeaderRow + 1;
        int sStt = 1;
        if (overdriveAnalytics.ByHoldPointStage.Count == 0)
        {
            WriteEmptyStateRow(ws, sRow, 10, "(Không có dữ liệu thống kê theo trạm Hold Point)");
            sRow++;
        }
        else
        {
            foreach (var stg in overdriveAnalytics.ByHoldPointStage)
            {
                ws.Cells[sRow, 1].Value = sStt++;
                ws.Cells[sRow, 2].Value = stg.HoldPointStageSeq;
                ws.Cells[sRow, 3].Value = stg.StageName;
                ws.Cells[sRow, 4].Value = stg.OverdriveCount;
                ws.Cells[sRow, 5].Value = stg.DistinctBatchesCount;
                ws.Cells[sRow, 6].Value = stg.SharePercent;
                ws.Cells[sRow, 7, sRow, 10].Merge = true;
                ws.Cells[sRow, 7].Value = stg.SampleJustifications.Count > 0
                    ? string.Join(" | ", stg.SampleJustifications)
                    : "-";
                sRow++;
            }
        }
        ApplyGridBorders(ws, stageHeaderRow, sRow - 1, 1, 10);

        // Khối 3: Bảng Nhật ký chi tiết từng lần phê duyệt phá khóa
        int detailSectionRow = sRow + 2;
        WriteSectionTitle(ws, detailSectionRow, 10, "3. NHẬT KÝ CHI TIẾT CÁC LẦN PHÁ KHÓA NGOẠI LỆ HOLD POINT", Color.FromArgb(217, 225, 242));
        int detailHeaderRow = detailSectionRow + 1;
        string[] headers =
        {
            "Mã Overdrive", "Mã Thẻ Lô", "Mã PO#", "Mã SKU", "Tên Sản phẩm",
            "Trạm Hold Point", "Tên Trạm Dừng", "Tổ trưởng Yêu cầu", "Chủ xưởng / Quản lý Phê duyệt",
            "Lý do Giải trình & Thời điểm Duyệt"
        };
        WriteHeaderRow(ws, detailHeaderRow, headers, Color.FromArgb(47, 85, 151));

        int row = detailHeaderRow + 1;
        if (overdriveAnalytics.RecentOverdriveLogs.Count == 0)
        {
            WriteEmptyStateRow(ws, row, headers.Length, "(Không có nhật ký phá khóa Hold Point nào khớp với bộ lọc hiện tại)");
            row++;
        }
        else
        {
            foreach (var log in overdriveAnalytics.RecentOverdriveLogs)
            {
                ws.Cells[row, 1].Value = log.OverdriveId;
                ws.Cells[row, 2].Value = log.BatchCode;
                ws.Cells[row, 3].Value = log.PoNumber;
                ws.Cells[row, 4].Value = log.ProductCode;
                ws.Cells[row, 5].Value = log.ProductName;
                ws.Cells[row, 6].Value = log.HoldPointStageSeq;
                ws.Cells[row, 7].Value = log.StageName;
                ws.Cells[row, 8].Value = log.RequesterName;
                ws.Cells[row, 9].Value = log.ApproverName;
                ws.Cells[row, 10].Value = $"{log.Justification} ({log.ApprovedAt:yyyy-MM-dd HH:mm} UTC)";
                row++;
            }
        }

        ApplyGridBorders(ws, detailHeaderRow, row - 1, 1, headers.Length);
        AutoFitColumnsSafe(ws, headers.Length);
    }

    private static void WriteSectionTitle(ExcelWorksheet ws, int row, int totalCols, string title, Color bgColor)
    {
        ws.Cells[row, 1, row, totalCols].Merge = true;
        ws.Cells[row, 1].Value = title;
        ws.Cells[row, 1].Style.Font.Size = 11;
        ws.Cells[row, 1].Style.Font.Bold = true;
        ws.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
        ws.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(bgColor);
        ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
        ws.Cells[row, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        ws.Row(row).Height = 22;
    }

    private static void WriteEmptyStateRow(ExcelWorksheet ws, int row, int totalCols, string message)
    {
        ws.Cells[row, 1, row, totalCols].Merge = true;
        ws.Cells[row, 1].Value = message;
        ws.Cells[row, 1].Style.Font.Italic = true;
        ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    }

    private static string FormatEtdRiskVi(string riskLevel) => riskLevel switch
    {
        "ON_TRACK" => "Đúng tiến độ",
        "WARNING" => "Cảnh báo cận ETD",
        "OVERDUE" => "Trễ hạn ETD",
        "COMPLETED" => "Đã hoàn tất",
        _ => riskLevel
    };

    private static string FormatBatchTypeVi(string batchType) => batchType switch
    {
        "MAIN_CARD" => "Thẻ chính",
        "REWORK_SUB_CARD" => "Thẻ sửa chữa (3%)",
        "REPLENISH_LOT" => "Lô bù phôi hủy",
        _ => batchType
    };

    private static string FormatQcResultVi(string resultStatus) => resultStatus switch
    {
        "FULL_PASS_100" => "Đạt chuẩn 100%",
        "BRANCHED_PARTIAL" => "Tách nhánh lỗi (97-3)",
        "FULL_REJECT" => "Không đạt toàn bộ",
        _ => resultStatus
    };

    private static string FormatDispositionVi(string disposition) => disposition switch
    {
        "REWORK_SUB_CARD" => "Sửa chữa (Rework)",
        "SCRAP_REPLENISH" => "Hủy & Bù phôi mới",
        _ => disposition
    };

    private static string FormatBatchStatusVi(string batchStatus) => batchStatus switch
    {
        "LOCKED" => "Khóa chờ vật tư",
        "IN_PROGRESS" => "Đang gia công",
        "QC_PENDING" => "Chờ kiểm định KCS",
        "HOLD_POINT_BLOCKED" => "Khóa cứng Hold Point",
        "DONE" => "Hoàn tất (DONE)",
        _ => batchStatus
    };

    private static void WriteBannerTitle(ExcelWorksheet ws, string title, string subtitle, int totalCols)
    {
        ws.Cells[1, 1, 1, totalCols].Merge = true;
        ws.Cells[1, 1].Value = title;
        ws.Cells[1, 1].Style.Font.Size = 13;
        ws.Cells[1, 1].Style.Font.Bold = true;
        ws.Cells[1, 1].Style.Font.Color.SetColor(Color.White);
        ws.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
        ws.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(24, 52, 92));
        ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        ws.Cells[1, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        ws.Row(1).Height = 26;

        ws.Cells[2, 1, 2, totalCols].Merge = true;
        ws.Cells[2, 1].Value = subtitle;
        ws.Cells[2, 1].Style.Font.Size = 10;
        ws.Cells[2, 1].Style.Font.Italic = true;
        ws.Cells[2, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
        ws.Cells[2, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(237, 242, 248));
        ws.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
        ws.Cells[2, 1].Style.WrapText = true;
        ws.Row(2).Height = 24;
    }

    private static void WriteHeaderRow(ExcelWorksheet ws, int row, string[] headers, Color bgColor)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cells[row, i + 1];
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.Color.SetColor(Color.White);
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(bgColor);
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        }
        ws.Row(row).Height = 22;
    }

    private static void ApplyGridBorders(ExcelWorksheet ws, int startRow, int endRow, int startCol, int endCol)
    {
        if (endRow < startRow) return;
        var range = ws.Cells[startRow, startCol, endRow, endCol];
        range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
        range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
        range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
        range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
        range.Style.WrapText = true;
        range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
    }

    private static void AutoFitColumnsSafe(ExcelWorksheet ws, int totalCols)
    {
        for (int c = 1; c <= totalCols; c++)
        {
            ws.Column(c).Width = c == totalCols ? 42 : 20;
        }
    }
}
