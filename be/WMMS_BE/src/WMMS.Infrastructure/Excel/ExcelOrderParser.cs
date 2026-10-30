using System.Globalization;
using System.Text.RegularExpressions;
using OfficeOpenXml;
using WMMS.Application.DTOs.Orders;
using WMMS.Application.Interfaces.External;
using WMMS.Domain.Constants;
using WMMS.Domain.Exceptions;

namespace WMMS.Infrastructure.Excel;

/// <summary>
/// Bộ bóc tách file Excel PO Khách hàng độc lập (UC-30: Upload Customer PO Excel, BR-ORD-01).
/// Phân tách rõ 4 khối dữ liệu trên sheet Excel:
/// 1. Khối Header PO (Mã PO#, Tên khách hàng).
/// 2. Khối Bảng Sản phẩm Sản xuất (hỗ trợ header gộp 2 tầng, tự động nhận diện cột FSC &amp; Finish không tiêu đề).
/// 3. Khối Dữ liệu Thương mại / Vận tải nhạy cảm bị lọc bỏ theo BR-ORD-01 (Unit Price, Amount USD, HS Code, Container Q'ty).
/// 4. Khối Footer Tổng kết &amp; Ngày giao hàng mục tiêu (Total CBM, Total CTN, Total Qty, The ETD).
/// </summary>
public class ExcelOrderParser : IExcelOrderParser
{
    public Task<ExcelOrderParseResultDto> ParseOrderExcelAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        var result = new ExcelOrderParseResultDto
        {
            FileName = fileName
        };

        using var package = new ExcelPackage(fileStream);
        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
        if (worksheet is null || worksheet.Dimension is null)
        {
            throw new AppException(
                "Invalid Excel template. Worksheet is empty (UC030.E1).",
                400,
                MessageCodes.Msg13ValidationError);
        }

        result.SheetName = worksheet.Name;

        var maxRow = worksheet.Dimension.End.Row;
        var maxCol = worksheet.Dimension.End.Column;

        var strippedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Container No",
            "Shipping Port",
            "Proforma Invoice (PI)",
            "Unit Prices (CBD)"
        };

        // --------------------------------------------------------------------
        // KHỐI 1 & 3: Quét vùng Header (10 dòng đầu) tìm PO#, Khách hàng & Cột bảng
        // --------------------------------------------------------------------
        var headerRow = 1;
        var dataStartRow = 2;
        var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var r = 1; r <= Math.Min(10, maxRow); r++)
        {
            for (var c = 1; c <= maxCol; c++)
            {
                var cellText = GetCellString(worksheet, r, c);
                if (string.IsNullOrWhiteSpace(cellText))
                {
                    continue;
                }

                // Bóc tách mã PO# (VD: "PO#22771200" hoặc "PO Number: 22771200")
                if (cellText.StartsWith("PO#", StringComparison.OrdinalIgnoreCase) ||
                    cellText.StartsWith("PO Number", StringComparison.OrdinalIgnoreCase) ||
                    cellText.StartsWith("PO No", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = cellText.Split(new[] { ':', '#' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[^1]))
                    {
                        result.DetectedPoNumber = parts[^1].Trim();
                    }
                    else if (c + 1 <= maxCol)
                    {
                        var nextCell = GetCellString(worksheet, r, c + 1);
                        if (!string.IsNullOrWhiteSpace(nextCell))
                        {
                            result.DetectedPoNumber = nextCell;
                        }
                    }
                }
                else if (cellText.StartsWith("Customer", StringComparison.OrdinalIgnoreCase) ||
                         cellText.StartsWith("Buyer", StringComparison.OrdinalIgnoreCase) ||
                         cellText.StartsWith("Client", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = cellText.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[^1]))
                    {
                        result.DetectedCustomerName = parts[^1].Trim();
                    }
                    else if (c + 1 <= maxCol)
                    {
                        var nextCell = GetCellString(worksheet, r, c + 1);
                        if (!string.IsNullOrWhiteSpace(nextCell))
                        {
                            result.DetectedCustomerName = nextCell;
                        }
                    }
                }

                var lower = cellText.ToLowerInvariant();
                var upperCell = r > 1 ? GetCellString(worksheet, r - 1, c).ToLowerInvariant() : string.Empty;

                // Nhận diện các cột thương mại cần loại bỏ theo BR-ORD-01
                if (lower.Contains("unit price") || (upperCell.Contains("unit price") && lower == "usd"))
                {
                    strippedFields.Add("Unit Price (USD)");
                    continue;
                }
                if (lower.Contains("amount") || (upperCell.Contains("amount") && lower == "usd"))
                {
                    strippedFields.Add("Commercial Amount (USD)");
                    continue;
                }
                if (lower.Contains("hs code") || lower == "hs" || (upperCell == "hs" && lower == "code"))
                {
                    strippedFields.Add("HS Code");
                    continue;
                }

                // Nhận diện cột SKU sản phẩm (Không ghi đè bởi cột "HS Code")
                if (lower.Contains("item no") || lower.Contains("sku") || lower.Contains("product code") ||
                    (lower == "code" && upperCell != "hs" && !colMap.ContainsKey("sku")))
                {
                    if (!colMap.ContainsKey("sku"))
                    {
                        headerRow = r;
                        colMap["sku"] = c;
                    }
                }
                else if (lower.Contains("description") || lower.Contains("product name") || lower.Contains("item name"))
                {
                    colMap["desc"] = c;
                }
                else if (lower.Contains("dimension") || lower.Contains("size"))
                {
                    colMap["dim"] = c;
                }
                else if (lower.Contains("finish") || lower.Contains("color") || lower.Contains("material"))
                {
                    colMap["finish"] = c;
                }
                else if ((lower == "quantity" || lower == "order qty" || lower == "qty") &&
                         !lower.Contains("ctn") && !lower.Contains("carton"))
                {
                    colMap["qty"] = c;
                }
                else if (lower.Contains("etd") || lower.Contains("delivery"))
                {
                    colMap["etd"] = c;
                }
                else if (lower.Contains("fsc"))
                {
                    colMap["fsc"] = c;
                }
                else if (lower.Contains("drawing") || lower.Contains("cad"))
                {
                    colMap["cad"] = c;
                }
                else if (lower.Contains("no. of") || lower == "ctn" || lower.Contains("carton"))
                {
                    if (lower == "ctn" || lower.Contains("no. of"))
                    {
                        colMap["ctn"] = c;
                    }
                }
                else if (lower == "total" && r + 1 <= maxRow &&
                         GetCellString(worksheet, r + 1, c).Equals("cbm", StringComparison.OrdinalIgnoreCase))
                {
                    colMap["total_cbm"] = c;
                }
            }
        }

        if (!colMap.ContainsKey("sku"))
        {
            colMap["sku"] = 1;
            colMap["desc"] = 2;
            colMap["dim"] = 3;
            colMap["finish"] = 4;
            colMap["qty"] = 5;
            colMap["etd"] = 6;
            dataStartRow = headerRow + 1;
        }
        else
        {
            // Kiểm tra xem dòng ngay dưới headerRow có phải là sub-header tầng 2 (như dòng 3 của sampleOrder.xlsx) hay không
            dataStartRow = headerRow + 1;
            if (dataStartRow <= maxRow)
            {
                var skuCellInNextRow = GetCellString(worksheet, dataStartRow, colMap["sku"]);
                var dimCellInNextRow = colMap.TryGetValue("dim", out var dCol) ? GetCellString(worksheet, dataStartRow, dCol) : string.Empty;
                if (string.IsNullOrWhiteSpace(skuCellInNextRow) && string.IsNullOrWhiteSpace(dimCellInNextRow))
                {
                    // Kiểm tra xem dòng này có chứa các từ khóa sub-header như "inner", "Master", "USD", "CTN", "CBM" không
                    var rowTexts = Enumerable.Range(1, maxCol)
                        .Select(c => GetCellString(worksheet, dataStartRow, c).ToLowerInvariant())
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .ToList();

                    if (rowTexts.Any(t => t is "inner" or "master" or "usd" or "cbm" or "ctn" or "code" or "of carton"))
                    {
                        dataStartRow++;
                    }
                }
            }

            // Tự động nhận diện cột FSC và Finish/Wood nếu file PO (như sampleOrder.xlsx) để trống tiêu đề cột E, F
            if (dataStartRow <= maxRow && colMap.TryGetValue("dim", out var dimCol))
            {
                for (var c = dimCol + 1; c <= Math.Min(dimCol + 3, maxCol); c++)
                {
                    if (colMap.ContainsValue(c))
                    {
                        continue;
                    }

                    var sampleVal = GetCellString(worksheet, dataStartRow, c);
                    if (string.IsNullOrWhiteSpace(sampleVal))
                    {
                        continue;
                    }

                    if (!colMap.ContainsKey("fsc") && sampleVal.Contains("FSC", StringComparison.OrdinalIgnoreCase))
                    {
                        colMap["fsc"] = c;
                    }
                    else if (!colMap.ContainsKey("finish") && !sampleVal.Contains("FSC", StringComparison.OrdinalIgnoreCase))
                    {
                        colMap["finish"] = c;
                    }
                }
            }
        }

        // --------------------------------------------------------------------
        // KHỐI 4: Quét vùng Footer ở cuối bảng để lấy Total, Container Q'ty và The ETD
        // --------------------------------------------------------------------
        var footerStartRow = maxRow + 1;
        for (var r = dataStartRow; r <= maxRow; r++)
        {
            for (var c = 1; c <= Math.Min(6, maxCol); c++)
            {
                var text = GetCellString(worksheet, r, c);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var lower = text.ToLowerInvariant();
                if (lower.StartsWith("total") || lower.StartsWith("q'ty") || lower.StartsWith("the etd") ||
                    lower.StartsWith("etd :") || lower.StartsWith("etd:") || lower == "submit" || lower == "cancel")
                {
                    if (r < footerStartRow)
                    {
                        footerStartRow = r;
                    }

                    if (lower.StartsWith("total"))
                    {
                        if (colMap.TryGetValue("qty", out var qCol))
                        {
                            var totalQtyStr = GetCellString(worksheet, r, qCol).Replace(",", "").Trim();
                            if (int.TryParse(totalQtyStr, out var totalQty))
                            {
                                result.TotalOrderQty = totalQty;
                            }
                        }

                        if (colMap.TryGetValue("ctn", out var ctnCol))
                        {
                            var totalCtnStr = GetCellString(worksheet, r, ctnCol).Replace(",", "").Trim();
                            if (int.TryParse(totalCtnStr, out var totalCtn))
                            {
                                result.TotalCartons = totalCtn;
                            }
                        }

                        if (colMap.TryGetValue("total_cbm", out var cbmCol))
                        {
                            var totalCbmStr = GetCellString(worksheet, r, cbmCol).Replace(",", ".").Trim();
                            if (decimal.TryParse(totalCbmStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var totalCbm))
                            {
                                result.TotalCbm = Math.Round(totalCbm, 4);
                            }
                        }
                    }
                    else if (lower.StartsWith("q'ty"))
                    {
                        var containerVal = FindNextNonEmptyCellInRow(worksheet, r, c + 1, maxCol);
                        if (!string.IsNullOrWhiteSpace(containerVal))
                        {
                            result.ContainerSpec = containerVal;
                            strippedFields.Add($"Container Spec ({containerVal})");
                        }
                    }
                    else if (lower.Contains("etd"))
                    {
                        var rawEtd = FindNextNonEmptyCellInRow(worksheet, r, c + 1, maxCol);
                        if (string.IsNullOrWhiteSpace(rawEtd) && text.Contains(':'))
                        {
                            rawEtd = text.Split(':', 2)[1].Trim();
                        }

                        if (!string.IsNullOrWhiteSpace(rawEtd))
                        {
                            result.DetectedTargetEtd = NormalizeDateString(rawEtd);
                        }
                    }
                }
            }
        }

        // --------------------------------------------------------------------
        // KHỐI 2: Trích xuất danh sách dòng sản phẩm sản xuất (Production Line Items)
        // --------------------------------------------------------------------
        for (var r = dataStartRow; r < footerStartRow; r++)
        {
            var sku = colMap.TryGetValue("sku", out var skuCol) ? GetCellString(worksheet, r, skuCol) : string.Empty;
            var desc = colMap.TryGetValue("desc", out var descCol) ? GetCellString(worksheet, r, descCol) : string.Empty;
            var dimRaw = colMap.TryGetValue("dim", out var dimCol) ? GetCellString(worksheet, r, dimCol) : string.Empty;
            var finishRaw = colMap.TryGetValue("finish", out var finCol) ? GetCellString(worksheet, r, finCol) : string.Empty;
            var qtyStr = colMap.TryGetValue("qty", out var qtyCol) ? GetCellString(worksheet, r, qtyCol) : string.Empty;
            var etdRaw = colMap.TryGetValue("etd", out var etdCol) ? GetCellString(worksheet, r, etdCol) : string.Empty;
            var fscStr = colMap.TryGetValue("fsc", out var fscCol) ? GetCellString(worksheet, r, fscCol) : "FSC 100%";
            var cadStr = colMap.TryGetValue("cad", out var cadCol) ? GetCellString(worksheet, r, cadCol) : string.Empty;

            if (string.IsNullOrWhiteSpace(sku) && string.IsNullOrWhiteSpace(desc) && string.IsNullOrWhiteSpace(qtyStr))
            {
                continue;
            }

            // Chuẩn hóa Finish/Color trên 1 dòng (thay xuống dòng trong ô Excel như "OAK\nASH/black" thành "OAK / ASH/black")
            var finishNormalized = Regex.Replace(finishRaw ?? string.Empty, @"\r?\n+", " - ").Trim();

            // Chuẩn hóa kích thước từ các dạng viết tắt trong PO thực tế (VD: "kt:100*52*h180", "47,7 x 8,5 x h 18 cm", "50x24h110")
            var dimNormalized = NormalizeRawDimensionString(dimRaw);

            // Nếu file PO không có cột Product Description riêng (như sampleOrder.xlsx), tự động sinh mô tả từ Finish & SKU
            if (string.IsNullOrWhiteSpace(desc) && !string.IsNullOrWhiteSpace(sku))
            {
                desc = !string.IsNullOrWhiteSpace(finishNormalized)
                    ? $"Wooden Furniture Item #{sku} ({finishNormalized})"
                    : $"Wooden Furniture Item #{sku}";
            }

            // Nếu từng dòng không có cột ETD riêng, kế thừa ngày ETD ở khối Footer của đơn hàng (VD: Jan 15,2027 -> 2027-01-15)
            var effectiveEtd = !string.IsNullOrWhiteSpace(etdRaw)
                ? NormalizeDateString(etdRaw)
                : (result.DetectedTargetEtd ?? string.Empty);

            _ = int.TryParse(qtyStr.Replace(",", "").Trim(), out var parsedQty);

            result.Rows.Add(new StagingOrderRowDto
            {
                RowNumber = result.Rows.Count + 1,
                ProductSku = sku,
                ProductDescription = desc,
                DimensionsMm = dimNormalized,
                FinishColor = finishNormalized,
                OrderQty = parsedQty,
                TargetEtd = effectiveEtd,
                FscRequired = string.IsNullOrWhiteSpace(fscStr) ||
                              fscStr.Contains("FSC", StringComparison.OrdinalIgnoreCase) ||
                              fscStr.Contains("100", StringComparison.OrdinalIgnoreCase),
                TechnicalDrawingName = cadStr
            });
        }

        if (!result.TotalOrderQty.HasValue && result.Rows.Count > 0)
        {
            result.TotalOrderQty = result.Rows.Sum(x => x.OrderQty);
        }

        result.StrippedCommercialFields = strippedFields.ToList();
        return Task.FromResult(result);
    }

    /// <summary>
    /// Chuẩn hóa chuỗi kích thước thô trong Excel PO khách hàng (VD: "kt:100*52*h180", "80x35xh170", "47,7 x 8,5 x h 18 cm", "50x24h110")
    /// về định dạng chuẩn "L x W x H" (đổi từ cm sang mm nếu phát hiện đơn vị cm hoặc thông số nhỏ hơn 250).
    /// </summary>
    public static string NormalizeRawDimensionString(string? rawDim)
    {
        if (string.IsNullOrWhiteSpace(rawDim))
        {
            return string.Empty;
        }

        var cleaned = rawDim.Trim();

        // Bỏ tiền tố "kt:", "size:", "dim:"
        cleaned = Regex.Replace(cleaned, @"^(?:kt|size|dim)\s*[:\-]?\s*", "", RegexOptions.IgnoreCase);

        // Đổi dấu phẩy thập phân kiểu châu Âu/Việt Nam (VD: "47,7" -> "47.7")
        cleaned = Regex.Replace(cleaned, @"(\d),(\d)", "$1.$2");

        // Xử lý các dạng viết liền chữ w/d/h như "50x24h110" hoặc "80x35xh170" thành dấu nhân "x"
        cleaned = Regex.Replace(cleaned, @"[xX\*×]?\s*[lLwWdDhH]\s*", " x ");

        // Trích xuất đúng 3 con số L, W, H
        var matches = Regex.Matches(cleaned, @"\d+(?:\.\d+)?");
        if (matches.Count == 3 &&
            decimal.TryParse(matches[0].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d1) &&
            decimal.TryParse(matches[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d2) &&
            decimal.TryParse(matches[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d3))
        {
            var isCmExplicit = rawDim.Contains("cm", StringComparison.OrdinalIgnoreCase);
            // Trong ngành gỗ nội thất, nếu cả 3 chiều đều <= 250 (VD: 100*52*180 cm) thì đơn vị gốc trên PO là cm -> quy đổi sang mm (* 10)
            var factor = (isCmExplicit || (d1 <= 250 && d2 <= 250 && d3 <= 250)) ? 10m : 1m;

            var lMm = Math.Round(d1 * factor, 0);
            var wMm = Math.Round(d2 * factor, 0);
            var hMm = Math.Round(d3 * factor, 0);

            return $"{lMm.ToString(CultureInfo.InvariantCulture)} x {wMm.ToString(CultureInfo.InvariantCulture)} x {hMm.ToString(CultureInfo.InvariantCulture)}";
        }

        return rawDim.Trim();
    }

    public static string NormalizeDateString(string rawDate)
    {
        if (string.IsNullOrWhiteSpace(rawDate))
        {
            return string.Empty;
        }

        // Chuẩn hóa khoảng trắng sau dấu phẩy (VD: "Jan 15,2027" -> "Jan 15, 2027")
        var normalized = Regex.Replace(rawDate.Trim(), @",\s*", ", ");
        var formats = new[]
        {
            "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy/MM/dd", "d/M/yyyy",
            "MMM d, yyyy", "MMM dd, yyyy", "MMMM d, yyyy", "MMMM dd, yyyy",
            "dd-MMM-yyyy", "d-MMM-yyyy"
        };

        if (DateTime.TryParseExact(normalized, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ||
            DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
        {
            return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return rawDate.Trim();
    }

    private static string FindNextNonEmptyCellInRow(ExcelWorksheet sheet, int row, int startCol, int maxCol)
    {
        for (var c = startCol; c <= maxCol; c++)
        {
            var val = GetCellString(sheet, row, c);
            if (!string.IsNullOrWhiteSpace(val))
            {
                return val;
            }
        }

        return string.Empty;
    }

    private static string GetCellString(ExcelWorksheet sheet, int row, int col)
    {
        if (col <= 0 || col > sheet.Dimension.End.Column)
        {
            return string.Empty;
        }

        var val = sheet.Cells[row, col].Value;
        if (val is DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return sheet.Cells[row, col].Text?.Trim() ?? string.Empty;
    }
}
