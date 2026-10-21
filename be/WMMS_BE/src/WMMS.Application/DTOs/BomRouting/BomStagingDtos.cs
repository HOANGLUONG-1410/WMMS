namespace WMMS.Application.DTOs.BomRouting;

public class BomStagingRowDto
{
    public int RowIndex { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialGroup { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal NetQtyPerUnit { get; set; }
    public string? Notes { get; set; }
    public long? ResolvedMaterialId { get; set; }
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
}

public class BomStagingPreviewDto
{
    public string FileName { get; set; } = string.Empty;
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
