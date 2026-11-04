using System.Globalization;
using System.Text.RegularExpressions;

namespace WMMS.Application.Common.Helpers;

/// <summary>
/// Bộ công cụ chuẩn hóa &amp; quy đổi Đơn vị tính (UOM Conversion Helper) chuyên dụng cho ngành sản xuất nội thất gỗ (MES).
/// Hỗ trợ:
/// 1. Tính thể tích gỗ khối (<c>m³</c> / CBM) từ kích thước phôi hộp chữ nhật (<c>Dài x Rộng x Dày</c>) theo đơn vị <c>cm</c> hoặc <c>mm</c>.
/// 2. Tính thể tích phôi gỗ tiện tròn (<c>m³</c>) từ chiều dài và đường kính (<c>Phi / Ø</c>).
/// 3. Tính diện tích ván công nghiệp / veneer (<c>m²</c>) từ chiều dài và bản rộng.
/// 4. Quy đổi số lượng mua sắm theo quy cách đóng gói chuẩn của xưởng (kiện gỗ, thùng sơn, hộp ốc vít).
/// </summary>
public static class UomConversionHelper
{
    private static readonly Regex PhiRegex = new(
        @"(?:phi|ø|d)\s*(?<D>\d+(?:[.,]\d+)?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Tính thể tích 1 phôi gỗ hộp chữ nhật (<c>m³</c>) từ kích thước Dài x Rộng x Dày (đơn vị: <c>cm</c>).
    /// Công thức: <c>V (m³) = (L_cm * W_cm * T_cm) / 1,000,000</c>.
    /// </summary>
    public static decimal CalculateWoodVolumeM3FromCm(decimal lengthCm, decimal widthCm, decimal thicknessCm, int piecesPerUnit = 1)
    {
        if (lengthCm <= 0 || widthCm <= 0 || thicknessCm <= 0 || piecesPerUnit <= 0)
        {
            return 0m;
        }

        var volumeSingleM3 = (lengthCm * widthCm * thicknessCm) / 1_000_000m;
        return Math.Round(volumeSingleM3 * piecesPerUnit, 6);
    }

    /// <summary>
    /// Tính thể tích 1 phôi gỗ hộp chữ nhật (<c>m³</c>) từ kích thước Dài x Rộng x Dày (đơn vị: <c>mm</c>).
    /// Công thức: <c>V (m³) = (L_mm * W_mm * T_mm) / 1,000,000,000</c>.
    /// </summary>
    public static decimal CalculateWoodVolumeM3FromMm(decimal lengthMm, decimal widthMm, decimal thicknessMm, int piecesPerUnit = 1)
    {
        if (lengthMm <= 0 || widthMm <= 0 || thicknessMm <= 0 || piecesPerUnit <= 0)
        {
            return 0m;
        }

        var volumeSingleM3 = (lengthMm * widthMm * thicknessMm) / 1_000_000_000m;
        return Math.Round(volumeSingleM3 * piecesPerUnit, 6);
    }

    /// <summary>
    /// Tính thể tích phôi gỗ tiện tròn (<c>m³</c>, ví dụ chân ghế tiện tròn <c>dài 37cm, phi 3.5cm</c>).
    /// Trong thực tế xưởng mộc, phôi gỗ trước khi tiện tròn được xẻ từ phôi vuông ngoại tiếp (<c>D x D x L</c>),
    /// do đó thể tích tiêu hao gỗ thực tế tính theo phôi vuông <c>(D_cm * D_cm * L_cm) / 1,000,000</c>.
    /// </summary>
    public static decimal CalculateRoundTurnedWoodVolumeM3FromCm(decimal lengthCm, decimal diameterCm, int piecesPerUnit = 1)
    {
        if (lengthCm <= 0 || diameterCm <= 0 || piecesPerUnit <= 0)
        {
            return 0m;
        }

        var blankSquareVolumeM3 = (lengthCm * diameterCm * diameterCm) / 1_000_000m;
        return Math.Round(blankSquareVolumeM3 * piecesPerUnit, 6);
    }

    /// <summary>
    /// Tính diện tích bề mặt ván / veneer / đệm (<c>m²</c>) từ kích thước Dài x Rộng (đơn vị: <c>cm</c>).
    /// Công thức: <c>S (m²) = (L_cm * W_cm) / 10,000</c>.
    /// </summary>
    public static decimal CalculateSheetAreaM2FromCm(decimal lengthCm, decimal widthCm, int piecesPerUnit = 1)
    {
        if (lengthCm <= 0 || widthCm <= 0 || piecesPerUnit <= 0)
        {
            return 0m;
        }

        var areaSingleM2 = (lengthCm * widthCm) / 10_000m;
        return Math.Round(areaSingleM2 * piecesPerUnit, 4);
    }

    /// <summary>
    /// Phân tích giá trị cột "Bản rộng" trong bảng BOM thực tế của xưởng (có thể là số <c>40.0</c> hoặc đường kính tiện tròn <c>phi 3.5</c>).
    /// </summary>
    public static bool TryParseWidthOrDiameterCm(string? rawWidthText, out decimal valueCm, out bool isRoundPhi)
    {
        valueCm = 0m;
        isRoundPhi = false;

        if (string.IsNullOrWhiteSpace(rawWidthText))
        {
            return false;
        }

        var text = rawWidthText.Trim();
        var phiMatch = PhiRegex.Match(text);
        if (phiMatch.Success)
        {
            isRoundPhi = true;
            var numStr = phiMatch.Groups["D"].Value.Replace(',', '.');
            return decimal.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out valueCm) && valueCm > 0;
        }

        var normalized = text.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out valueCm) && valueCm > 0;
    }

    /// <summary>
    /// Quy đổi định mức từ thông số chi tiết phôi (<c>Dài, Bản, Dầy, Số thanh/1 SP</c> trong <c>sampleBOM.xls</c>)
    /// sang đơn vị tính chuẩn của danh mục vật tư (<c>m3</c>, <c>m2</c>, <c>tấm</c>, <c>bộ</c>, <c>cái</c>).
    /// </summary>
    public static decimal ConvertComponentSpecToMaterialUom(
        string targetUom,
        decimal? lengthCm,
        string? widthOrPhiText,
        decimal? thicknessCm,
        decimal piecesPerUnit)
    {
        if (piecesPerUnit <= 0)
        {
            return 0m;
        }

        var uom = (targetUom ?? string.Empty).Trim().ToLowerInvariant();
        var hasLength = lengthCm is > 0;
        var hasWidth = TryParseWidthOrDiameterCm(widthOrPhiText, out var widthCm, out var isRoundPhi);
        var hasThickness = thicknessCm is > 0;

        // Nếu vật tư quản lý theo khối lượng gỗ m3
        if (uom is "m3" or "m³" or "cbm")
        {
            if (hasLength && hasWidth && isRoundPhi)
            {
                var volRound = CalculateRoundTurnedWoodVolumeM3FromCm(lengthCm!.Value, widthCm, 1) * piecesPerUnit;
                return Math.Max(0.0001m, Math.Round(volRound, 4));
            }

            if (hasLength && hasWidth && hasThickness)
            {
                var volBox = CalculateWoodVolumeM3FromCm(lengthCm!.Value, widthCm, thicknessCm!.Value, 1) * piecesPerUnit;
                return Math.Max(0.0001m, Math.Round(volBox, 4));
            }
        }

        // Nếu vật tư quản lý theo diện tích m2 hoặc tấm ván chuẩn (122cm x 244cm = 2.9768 m2)
        if (uom is "m2" or "m²")
        {
            if (hasLength && hasWidth)
            {
                var area = CalculateSheetAreaM2FromCm(lengthCm!.Value, widthCm, 1) * piecesPerUnit;
                return Math.Max(0.0001m, Math.Round(area, 4));
            }
        }
        else if (uom is "tấm" or "tam" or "sheet")
        {
            if (hasLength && hasWidth)
            {
                const decimal standardBoardAreaM2 = 2.9768m; // Khổ ván tiêu chuẩn 1220 x 2440 mm
                var area = CalculateSheetAreaM2FromCm(lengthCm!.Value, widthCm, 1) * piecesPerUnit;
                return Math.Max(0.01m, Math.Round(area / standardBoardAreaM2, 4));
            }
        }

        return Math.Round(piecesPerUnit, 4);
    }
}
