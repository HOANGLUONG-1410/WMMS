using WMMS.Domain.Enums;

namespace WMMS.Application.DTOs.BomRouting;

public class BomDto
{
    public long BomId { get; set; }
    public long OrderItemId { get; set; }
    public long ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string DimensionsCm { get; set; } = string.Empty;
    public int OrderedQty { get; set; }
    public decimal ScrapRatePercent { get; set; }
    public BomStatusEnum BomStatus { get; set; }
    public string BomStatusText => BomStatus.ToString();
    public long CreatedBy { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<BomDetailDto> BomDetails { get; set; } = new();
    public List<RoutingStageDto> RoutingStages { get; set; } = new();
}

public class BomDetailDto
{
    public long BomDetailId { get; set; }
    public long BomId { get; set; }
    public long MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialGroup { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string? DimensionSpec { get; set; }
    public bool FscCertified { get; set; }
    public decimal NetQtyPerUnit { get; set; }
    public decimal GrossTotalDemand { get; set; }
    public string? Notes { get; set; }
}

public class BomDetailItemDto
{
    public long MaterialId { get; set; }
    public decimal NetQtyPerUnit { get; set; }
    public string? Notes { get; set; }
}

public class CreateBomRequestDto
{
    public long OrderItemId { get; set; }
    public long ProductId { get; set; }
    public decimal ScrapRatePercent { get; set; } = 3.00m;
    public List<BomDetailItemDto> Details { get; set; } = new();
    public List<RoutingStageDto>? CustomRoutingStages { get; set; }
}

public class UpdateBomRequestDto
{
    public decimal ScrapRatePercent { get; set; } = 3.00m;
    public List<BomDetailItemDto> Details { get; set; } = new();
    public List<RoutingStageDto>? CustomRoutingStages { get; set; }
}

public class CalculateDemandItemDto
{
    public long MaterialId { get; set; }
    public decimal NetQtyPerUnit { get; set; }
}

public class CalculateDemandRequestDto
{
    public int OrderedQty { get; set; }
    public decimal ScrapRatePercent { get; set; } = 3.00m;
    public List<CalculateDemandItemDto> Items { get; set; } = new();
}

public class CalculateDemandItemResultDto
{
    public long MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal NetQtyPerUnit { get; set; }
    public decimal GrossTotalDemand { get; set; }
}

public class CalculateDemandResponseDto
{
    public int OrderedQty { get; set; }
    public decimal ScrapRatePercent { get; set; }
    public List<CalculateDemandItemResultDto> CalculatedItems { get; set; } = new();
}

public class InheritedBomDto
{
    public bool HasExistingBom { get; set; }
    public long? SourceBomId { get; set; }
    public decimal ScrapRatePercent { get; set; }
    public List<BomDetailDto> Details { get; set; } = new();
    public List<RoutingStageDto> RoutingStages { get; set; } = new();
}

public class RoutingStageDto
{
    public long StageId { get; set; }
    public int StageSequence { get; set; }
    public string StageName { get; set; } = string.Empty;
    public bool IsHoldPoint { get; set; }
}

public class VisualCatalogProductDto
{
    public long ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string DimensionsCm { get; set; } = string.Empty;
    public string? WoodSpecies { get; set; }
    public string? FinishColor { get; set; }
    public string? ImageUrl { get; set; }
    public string? CadDrawingUrl { get; set; }
    public bool HasActiveBom { get; set; }
    public List<RoutingStageDto> DefaultRoutingStages { get; set; } = new();
}
