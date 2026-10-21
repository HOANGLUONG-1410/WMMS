using System.Globalization;
using OfficeOpenXml;
using WMMS.Application.DTOs.BomRouting;
using WMMS.Application.Interfaces.External;

namespace WMMS.Infrastructure.Excel;

public class ExcelBomParser : IExcelBomParser
{
    public Task<BomStagingPreviewDto> ParseBomExcelAsync(Stream fileStream, string fileName)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        var result = new BomStagingPreviewDto
        {
            FileName = fileName
        };

        using var package = new ExcelPackage(fileStream);
        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
        if (worksheet == null || worksheet.Dimension == null)
        {
            return Task.FromResult(result);
        }

        int rowCount = worksheet.Dimension.Rows;
        int colCount = worksheet.Dimension.Columns;

        // Detect column indices based on header names (case-insensitive)
        int colMaterialCode = -1;
        int colMaterialName = -1;
        int colMaterialGroup = -1;
        int colUnit = -1;
        int colNetQty = -1;
        int colNotes = -1;

        int headerRow = 1;
        for (int row = 1; row <= Math.Min(rowCount, 5); row++)
        {
            for (int col = 1; col <= colCount; col++)
            {
                var val = worksheet.Cells[row, col].Text?.Trim().ToLowerInvariant() ?? "";
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

        // Fallbacks if headers were not explicitly identified
        if (colMaterialCode == -1) colMaterialCode = 1;
        if (colMaterialName == -1) colMaterialName = 2;
        if (colMaterialGroup == -1) colMaterialGroup = 3;
        if (colUnit == -1) colUnit = 4;
        if (colNetQty == -1) colNetQty = 5;
        if (colNotes == -1) colNotes = 6;

        int rowIndexCounter = 1;
        for (int row = headerRow + 1; row <= rowCount; row++)
        {
            var codeText = worksheet.Cells[row, colMaterialCode].Text?.Trim() ?? "";
            var nameText = colMaterialName <= colCount ? (worksheet.Cells[row, colMaterialName].Text?.Trim() ?? "") : "";
            var groupText = colMaterialGroup <= colCount ? (worksheet.Cells[row, colMaterialGroup].Text?.Trim() ?? "") : "";
            var unitText = colUnit <= colCount ? (worksheet.Cells[row, colUnit].Text?.Trim() ?? "") : "";
            var qtyText = colNetQty <= colCount ? (worksheet.Cells[row, colNetQty].Text?.Trim() ?? "") : "";
            var notesText = colNotes <= colCount ? (worksheet.Cells[row, colNotes].Text?.Trim() ?? "") : "";

            // Skip completely empty rows
            if (string.IsNullOrWhiteSpace(codeText) && string.IsNullOrWhiteSpace(qtyText) && string.IsNullOrWhiteSpace(nameText))
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

            // Parse NetQtyPerUnit
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

        result.TotalRows = result.Rows.Count;
        result.ValidRows = result.Rows.Count(r => r.IsValid);
        result.ErrorRows = result.Rows.Count(r => !r.IsValid);

        return Task.FromResult(result);
    }
}
