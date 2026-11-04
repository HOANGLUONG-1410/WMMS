namespace WMMS.Application.DTOs.BomRouting;

public class BomStagingRowDto
{
    public int RowIndex { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialGroup { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal NetQtyPerUnit { get; set; }

    /// <summary>Tên cụm bán thành phẩm / nhóm cấu kiện bóc tách từ file Excel xưởng (ví dụ: phụ kiện sồi, phụ kiện veneer, phụ kiện lắp ráp).</summary>
    public string? SubAssemblyName { get; set; }

    /// <summary>Tên chi tiết cấu kiện trên bản vẽ (ví dụ: mặt, chân, xà chân, giằng, bệ chân).</summary>
    public string? ComponentPartName { get; set; }

    /// <summary>Quy cách kích thước phôi chi tiết (<c>Dài x Bản x Dầy cm</c>) trước khi quy đổi sang đơn vị tồn kho.</summary>
    public string? ComponentDimensionSpec { get; set; }

    /// <summary>Số lượng chi tiết (thanh/tấm/cái) trên 1 sản phẩm trước khi quy đổi UOM.</summary>
    public decimal? RawPiecesPerUnit { get; set; }

    /// <summary>Tồn kho khả dụng hiện tại của vật tư để KHVT thấy ngay trên lưới Staging.</summary>
    public decimal AvailableStock { get; set; }

    public string? Notes { get; set; }
    public long? ResolvedMaterialId { get; set; }
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
}

public class BomStagingPreviewDto
{
    public string FileName { get; set; } = string.Empty;
    public string SheetName { get; set; } = string.Empty;
    public string? DetectedProductSku { get; set; }
    public string? DetectedProductDimensions { get; set; }
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int ErrorRows { get; set; }
    public bool HasErrors => ErrorRows > 0;
    public List<BomStagingRowDto> Rows { get; set; } = new();
}

public class ConfirmBomStagingRequestDto
{
    public long OrderItemId { get; set; }
    public long ProductId { get; set; }
    public decimal ScrapRatePercent { get; set; } = 3.00m;
    public List<BomStagingRowDto> Rows { get; set; } = new();
}
