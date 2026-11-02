using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;
using OfficeOpenXml;
using WMMS.Application.Common.Helpers;
using WMMS.Application.DTOs.BomRouting;
using WMMS.Application.Interfaces.External;

namespace WMMS.Infrastructure.Excel;

/// <summary>
/// Bộ bóc tách file Excel BOM thực tế của xưởng mộc (SC-11, BR-011).
/// Hỗ trợ đồng thời cả 2 định dạng:
/// 1. Định dạng chuẩn hiện đại (<c>.xlsx</c>) với các cột Mã VT, Tên VT, Nhóm, ĐVT, Định mức.
/// 2. Định dạng sổ kỹ thuật thực tế của xưởng (<c>.xls</c> BIFF8 như <c>sampleBOM.xls</c>) chia theo từng cụm bán thành phẩm
///    (phụ kiện veneer, phụ kiện sồi, phụ kiện lắp ráp) kèm kích thước chi tiết phôi (<c>Dài x Bản x Ván dầy</c>, <c>Số lượng/1 SP</c>),
///    tự động ánh xạ sang mã vật tư chuẩn trong Master Data và quy đổi UOM (<c>cm -&gt; m³ / tấm / bộ</c>).
/// </summary>
public class ExcelBomParser : IExcelBomParser
{
    static ExcelBomParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public Task<BomStagingPreviewDto> ParseBomExcelAsync(Stream fileStream, string fileName)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        var result = new BomStagingPreviewDto
        {
            FileName = fileName
        };

        var grid = ReadFirstNonEmptySheetToGrid(fileStream, fileName, out var sheetName);
        result.SheetName = sheetName;

        if (grid.Count == 0)
        {
            return Task.FromResult(result);
        }

        // Kiểm tra xem sheet có phải định dạng sổ bóc tách phôi thực tế của xưởng (như sampleBOM.xls: có cột "tên phụ kiện", "dài", "bản", "ván dầy", "số lượng /một sp") không
        if (IsWorkshopComponentBreakdownFormat(grid))
        {
            ParseWorkshopComponentBreakdown(grid, result);
        }
        else
        {
            ParseStandardTabularBom(grid, result);
        }

        result.TotalRows = result.Rows.Count;
        result.ValidRows = result.Rows.Count(r => r.IsValid);
        result.ErrorRows = result.Rows.Count(r => !r.IsValid);

        return Task.FromResult(result);
    }

    private static List<List<string>> ReadFirstNonEmptySheetToGrid(Stream fileStream, string fileName, out string sheetName)
    {
        sheetName = "Sheet1";
        using var ms = new MemoryStream();
        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }
        fileStream.CopyTo(ms);
        ms.Position = 0;

        // Kiểm tra magic bytes của OLE2 Compound Document (.xls BIFF8: D0 CF 11 E0) hoặc đuôi .xls
        var headerBytes = new byte[8];
        var bytesRead = ms.Read(headerBytes, 0, headerBytes.Length);
        ms.Position = 0;

        bool isLegacyXls = (bytesRead >= 4 &&
                            headerBytes[0] == 0xD0 &&
                            headerBytes[1] == 0xCF &&
                            headerBytes[2] == 0x11 &&
                            headerBytes[3] == 0xE0)
                           || fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase);

        if (!isLegacyXls)
        {
            try
            {
                using var package = new ExcelPackage(ms);
                var ws = package.Workbook.Worksheets.FirstOrDefault(w => w.Dimension != null);
                if (ws != null && ws.Dimension != null)
                {
                    sheetName = ws.Name;
                    var rows = new List<List<string>>();
                    for (int r = 1; r <= ws.Dimension.Rows; r++)
                    {
                        var rowCells = new List<string>();
                        for (int c = 1; c <= ws.Dimension.Columns; c++)
                        {
                            rowCells.Add(ws.Cells[r, c].Text?.Trim() ?? string.Empty);
                        }
                        rows.Add(rowCells);
                    }
                    return rows;
                }
            }
            catch
            {
                // Fallback sang ExcelDataReader bên dưới nếu EPPlus không đọc được
                ms.Position = 0;
            }
        }

        ms.Position = 0;
        using var reader = ExcelReaderFactory.CreateReader(ms);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = false
            }
        });

        foreach (DataTable table in dataSet.Tables)
        {
            if (table.Rows.Count == 0 || table.Columns.Count == 0)
            {
                continue;
            }

            var grid = new List<List<string>>();
            bool hasAnyContent = false;

            for (int r = 0; r < table.Rows.Count; r++)
            {
                var rowList = new List<string>();
                for (int c = 0; c < table.Columns.Count; c++)
                {
                    var val = Convert.ToString(table.Rows[r][c], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        hasAnyContent = true;
                    }
                    rowList.Add(val);
                }
                grid.Add(rowList);
            }

            if (hasAnyContent)
            {
                sheetName = table.TableName;
                return grid;
            }
        }

        return new List<List<string>>();
    }

    private static bool IsWorkshopComponentBreakdownFormat(List<List<string>> grid)
    {
        for (int r = 0; r < Math.Min(10, grid.Count); r++)
        {
            var rowJoined = string.Join(" | ", grid[r]).ToLowerInvariant();
            if (rowJoined.Contains("tên phụ kiện") && (rowJoined.Contains("dài") || rowJoined.Contains("một sp") || rowJoined.Contains("ván dầy")))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Phân tích cấu trúc sổ kỹ thuật bóc tách phôi thực tế của xưởng (như <c>sampleBOM.xls</c>):
    /// - Nhận diện Mã SP (<c>MÃ :880501</c>) và Kích thước tổng (<c>L85xW40xH43</c>).
    /// - Nhận diện các cụm bán thành phẩm (<c>phụ kiện veneer</c>, <c>phụ kiện sồi</c>, <c>phụ kiện lắp ráp</c>).
    /// - Trích xuất chi tiết phôi (<c>tên phụ kiện</c>, <c>dài</c>, <c>bản</c>, <c>ván dầy</c>, <c>số lượng/một sp</c>, <c>ghi chú</c>).
    /// - Ánh xạ sang mã vật tư chuẩn trong Master Data và quy đổi UOM bằng <see cref="UomConversionHelper"/>.
    /// </summary>
    private static void ParseWorkshopComponentBreakdown(List<List<string>> grid, BomStagingPreviewDto result)
    {
        string currentSubAssembly = "Cụm chi tiết mộc";
        int colStt = 1;
        int colPartName = 2;
        int colLength = 3;
        int colWidth = 4;
        int colThickness = 5;
        int colQtyPerUnit = 6;
        int colNotes = 10;

        int rowIndexCounter = 1;

        for (int r = 0; r < grid.Count; r++)
        {
            var row = grid[r];
            var nonEmptyCells = row
                .Select((val, idx) => (Val: Regex.Replace(val ?? string.Empty, @"\s+", " ").Trim(), Idx: idx))
                .Where(x => !string.IsNullOrWhiteSpace(x.Val))
                .ToList();

            if (nonEmptyCells.Count == 0)
            {
                continue;
            }

            // Kiểm tra thông tin Mã SP hoặc Kích thước ở cột đầu tiên
            foreach (var cell in nonEmptyCells)
            {
                if (cell.Val.StartsWith("MÃ", StringComparison.OrdinalIgnoreCase) && cell.Val.Contains(':'))
                {
                    result.DetectedProductSku = cell.Val.Split(':', 2)[1].Trim();
                }
                else if (Regex.IsMatch(cell.Val, @"^L\d+.*W\d+.*H\d+", RegexOptions.IgnoreCase))
                {
                    result.DetectedProductDimensions = cell.Val;
                }
            }

            // Kiểm tra dòng Header của từng cụm ("stt | tên phụ kiện | dài | bản | ván dầy | số lượng /một sp ...")
            var rowJoinedLower = string.Join(" | ", nonEmptyCells.Select(c => c.Val.ToLowerInvariant()));
            if (rowJoinedLower.Contains("tên phụ kiện") && (rowJoinedLower.Contains("dài") || rowJoinedLower.Contains("một sp")))
            {
                foreach (var cell in nonEmptyCells)
                {
                    var lower = cell.Val.ToLowerInvariant();
                    if (lower == "stt") colStt = cell.Idx;
                    else if (lower.Contains("tên phụ kiện")) colPartName = cell.Idx;
                    else if (lower == "dài" || lower.Contains("dài")) colLength = cell.Idx;
                    else if (lower == "bản" || lower.Contains("rộng")) colWidth = cell.Idx;
                    else if (lower.Contains("dầy") || lower.Contains("dày")) colThickness = cell.Idx;
                    else if (lower.Contains("một sp") || lower.Contains("1 sp")) colQtyPerUnit = cell.Idx;
                    else if (lower.Contains("ghi chú")) colNotes = cell.Idx;
                }
                continue;
            }

            // Kiểm tra dòng tiêu đề cụm bán thành phẩm (ví dụ: "phụ kiện veneer", "phụ kiện sồi", "phụ kiện lắp ráp")
            if (nonEmptyCells.Count <= 2)
            {
                var sectionCandidate = nonEmptyCells.FirstOrDefault(c =>
                    c.Val.Contains("phụ kiện", StringComparison.OrdinalIgnoreCase) ||
                    c.Val.Contains("cụm", StringComparison.OrdinalIgnoreCase) ||
                    c.Val.Contains("phần", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(sectionCandidate.Val))
                {
                    currentSubAssembly = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(sectionCandidate.Val.ToLowerInvariant());
                    continue;
                }
            }

            // Lấy dữ liệu dòng chi tiết
            var sttText = GetGridCell(row, colStt);
            var partName = GetGridCell(row, colPartName);
            var lengthText = GetGridCell(row, colLength);
            var widthText = GetGridCell(row, colWidth);
            var thicknessText = GetGridCell(row, colThickness);
            var qtyPerUnitText = GetGridCell(row, colQtyPerUnit);
            var notesText = GetGridCell(row, colNotes);

            if (string.IsNullOrWhiteSpace(partName) || !decimal.TryParse(sttText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                continue;
            }

            _ = decimal.TryParse(lengthText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var lengthCm);
            _ = decimal.TryParse(thicknessText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var thicknessCm);
            var hasPieces = decimal.TryParse(qtyPerUnitText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var piecesPerUnit);

            // Tự động ánh xạ sang Mã vật tư chuẩn trong Master Data dựa trên cụm bán thành phẩm + tên chi tiết
            var (mappedCode, mappedName, mappedGroup, mappedUom) = InferCatalogMaterialFromWorkshopPart(
                currentSubAssembly,
                partName,
                lengthText,
                widthText,
                thicknessText);

            var dimParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(lengthText)) dimParts.Add($"Dài {lengthText}");
            if (!string.IsNullOrWhiteSpace(widthText)) dimParts.Add($"Bản {widthText}");
            if (!string.IsNullOrWhiteSpace(thicknessText)) dimParts.Add($"Dầy {thicknessText}");
            var componentDimSpec = dimParts.Count > 0 ? $"{string.Join(" x ", dimParts)} cm ({piecesPerUnit:0.##} chi tiết/SP)" : $"{piecesPerUnit:0.##} chi tiết/SP";

            var convertedNetQty = hasPieces && piecesPerUnit > 0
                ? UomConversionHelper.ConvertComponentSpecToMaterialUom(
                    mappedUom,
                    lengthCm > 0 ? lengthCm : null,
                    widthText,
                    thicknessCm > 0 ? thicknessCm : null,
                    piecesPerUnit)
                : 0m;

            var combinedNotes = string.IsNullOrWhiteSpace(notesText)
                ? $"[{currentSubAssembly}] Chi tiết: {partName} ({componentDimSpec})"
                : $"[{currentSubAssembly}] Chi tiết: {partName} ({componentDimSpec}) - {notesText}";

            var stagingRow = new BomStagingRowDto
            {
                RowIndex = rowIndexCounter++,
                MaterialCode = mappedCode,
                MaterialName = mappedName,
                MaterialGroup = mappedGroup,
                UnitOfMeasure = mappedUom,
                SubAssemblyName = currentSubAssembly,
                ComponentPartName = partName,
                ComponentDimensionSpec = componentDimSpec,
                RawPiecesPerUnit = hasPieces ? piecesPerUnit : null,
                NetQtyPerUnit = convertedNetQty,
                Notes = combinedNotes,
                IsValid = hasPieces && convertedNetQty > 0
            };

            if (!stagingRow.IsValid)
            {
                stagingRow.Errors.Add($"Số lượng/định mức chi tiết '{partName}' không hợp lệ.");
            }

            result.Rows.Add(stagingRow);
        }
    }

    private static (string Code, string Name, string Group, string Uom) InferCatalogMaterialFromWorkshopPart(
        string subAssembly,
        string partName,
        string lengthText,
        string widthText,
        string thicknessText)
    {
        var sectionLower = (subAssembly ?? string.Empty).ToLowerInvariant();
        var partLower = (partName ?? string.Empty).ToLowerInvariant();

        // 1. Nhóm Ván Veneer / MDF
        if (sectionLower.Contains("veneer") || partLower.Contains("veneer") || partLower.Contains("mdf"))
        {
            return ("WOOD_MDF_18", "Ván MDF chống ẩm phủ Veneer Sồi 18mm", "WOOD", "Tấm");
        }

        // 2. Nhóm Phụ kiện lắp ráp (Đệm, Ốc khế, Vít, Bản lề...)
        if (sectionLower.Contains("lắp ráp") || partLower.Contains("ốc") || partLower.Contains("vít") || partLower.Contains("bu lông") || partLower.Contains("đệm"))
        {
            if (partLower.Contains("đệm") || partLower.Contains("nệm") || partLower.Contains("mút"))
            {
                return ("PACK_FOAM_10", "Mút xốp PE Foam bọc lót đệm 10mm", "PACKAGING", "Cuộn");
            }

            return ("HW_BOLT_M6", "Bộ ốc vít lục giác & tán cấy gỗ M6x50mm", "HARDWARE", "Bộ");
        }

        // 3. Nhóm Gỗ Sồi (Oak) phân loại theo chiều dày ván hoặc phôi tiện
        if (sectionLower.Contains("sồi") || sectionLower.Contains("oak") || partLower.Contains("chân") || partLower.Contains("xà") || partLower.Contains("giằng"))
        {
            if (widthText.Contains("phi", StringComparison.OrdinalIgnoreCase) ||
                partLower.Contains("chân") ||
                (decimal.TryParse(thicknessText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var t) && t >= 3.0m))
            {
                return ("WOOD_OAK_40", "Gỗ Sồi Trắng (White Oak) sấy 40mm - FSC 100%", "WOOD", "m3");
            }

            return ("WOOD_OAK_25", "Gỗ Sồi Trắng (White Oak) sấy 25mm - FSC 100%", "WOOD", "m3");
        }

        return ("WOOD_OAK_25", "Gỗ Sồi Trắng (White Oak) sấy 25mm - FSC 100%", "WOOD", "m3");
    }

    private static void ParseStandardTabularBom(List<List<string>> grid, BomStagingPreviewDto result)
    {
        int rowCount = grid.Count;
        int colCount = grid.Max(r => r.Count);

        int colMaterialCode = -1;
        int colMaterialName = -1;
        int colMaterialGroup = -1;
        int colUnit = -1;
        int colNetQty = -1;
        int colNotes = -1;

        int headerRow = 0;
        for (int row = 0; row < Math.Min(rowCount, 6); row++)
        {
            for (int col = 0; col < grid[row].Count; col++)
            {
                var val = grid[row][col]?.Trim().ToLowerInvariant() ?? string.Empty;
                if (val.Contains("mã") || val.Contains("code") || val.Contains("item"))
                    colMaterialCode = col;
                else if (val.Contains("tên") || val.Contains("name") || val.Contains("desc"))
                    colMaterialName = col;
                else if (val.Contains("nhóm") || val.Contains("group") || val.Contains("loại"))
                    colMaterialGroup = col;
                else if (val.Contains("đvt") || val.Contains("đơn vị") || val.Contains("unit") || val.Contains("uom"))
                    colUnit = col;
                else if (val.Contains("định mức") || val.Contains("qty") || val.Contains("số lượng") || val.Contains("net"))
                    colNetQty = col;
                else if (val.Contains("chú") || val.Contains("note") || val.Contains("remark"))
                    colNotes = col;
            }

            if (colMaterialCode != -1 && colNetQty != -1)
            {
                headerRow = row;
                break;
            }
        }

        if (colMaterialCode == -1) colMaterialCode = 0;
        if (colMaterialName == -1) colMaterialName = 1;
        if (colMaterialGroup == -1) colMaterialGroup = 2;
        if (colUnit == -1) colUnit = 3;
        if (colNetQty == -1) colNetQty = 4;
        if (colNotes == -1) colNotes = 5;

        int rowIndexCounter = 1;
        for (int row = headerRow + 1; row < rowCount; row++)
        {
            var rCells = grid[row];
            var codeText = GetGridCell(rCells, colMaterialCode);
            var nameText = GetGridCell(rCells, colMaterialName);
            var groupText = GetGridCell(rCells, colMaterialGroup);
            var unitText = GetGridCell(rCells, colUnit);
            var qtyText = GetGridCell(rCells, colNetQty);
            var notesText = GetGridCell(rCells, colNotes);

            if (string.IsNullOrWhiteSpace(codeText) && string.IsNullOrWhiteSpace(qtyText) && string.IsNullOrWhiteSpace(nameText))
            {
                continue;
            }

            // Bỏ qua dòng tổng cộng cuối bảng nếu có
            var lowerCode = codeText.ToLowerInvariant();
            if (lowerCode.StartsWith("tổng") || lowerCode.StartsWith("total") || lowerCode.StartsWith("sum"))
            {
                continue;
            }

            var stagingRow = new BomStagingRowDto
            {
                RowIndex = rowIndexCounter++,
                MaterialCode = codeText,
                MaterialName = nameText,
                MaterialGroup = groupText,
                UnitOfMeasure = unitText,
                Notes = string.IsNullOrWhiteSpace(notesText) ? null : notesText,
                IsValid = true
            };

            if (string.IsNullOrWhiteSpace(qtyText))
            {
                stagingRow.IsValid = false;
                stagingRow.Errors.Add("Định mức 1 sản phẩm (NetQtyPerUnit) không được để trống.");
            }
            else if (!decimal.TryParse(qtyText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedQty))
            {
                stagingRow.IsValid = false;
                stagingRow.Errors.Add($"Định mức '{qtyText}' không đúng định dạng số hợp lệ.");
            }
            else if (parsedQty <= 0)
            {
                stagingRow.IsValid = false;
                stagingRow.Errors.Add("Định mức 1 sản phẩm phải lớn hơn 0.");
            }
            else
            {
                stagingRow.NetQtyPerUnit = parsedQty;
            }

            result.Rows.Add(stagingRow);
        }
    }

    private static string GetGridCell(List<string> row, int colIdx)
    {
        if (colIdx < 0 || colIdx >= row.Count)
        {
            return string.Empty;
        }

        return Regex.Replace(row[colIdx] ?? string.Empty, @"\s+", " ").Trim();
    }
}
