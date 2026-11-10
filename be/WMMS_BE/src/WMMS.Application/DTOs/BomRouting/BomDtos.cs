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

    /// <summary>Cờ báo hiệu BOM có ít nhất 1 nguyên vật liệu đang thiếu tồn kho khả dụng (QT-03).</summary>
    public bool HasMaterialShortage => BomDetails.Any(d => d.IsShortage);

    /// <summary>Số lượng dòng vật tư bị thiếu trong kho cần đưa vào đề xuất mua sắm PR.</summary>
    public int ShortageMaterialsCount => BomDetails.Count(d => d.IsShortage);

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

    /// <summary>Tồn kho thực tế tại xưởng (Physical Stock - SC-12).</summary>
    public decimal PhysicalStock { get; set; }

    /// <summary>Tồn kho đã khóa giữ chỗ cho các đơn hàng khác (Allocated Stock - QT-03).</summary>
    public decimal AllocatedStock { get; set; }

    /// <summary>Tồn kho khả dụng chưa giữ chỗ (<c>Available = Physical - Allocated</c> - QT-03).</summary>
    public decimal AvailableStock { get; set; }

    /// <summary>Số lượng thực thiếu cần mua thêm (<c>NetDemand = Max(0, GrossTotalDemand - AvailableStock)</c> - QT-03).</summary>
    public decimal NetDemand => Math.Max(0m, Math.Round(GrossTotalDemand - AvailableStock, 4));

    /// <summary>True nếu tồn khả dụng không đủ đáp ứng nhu cầu thô của BOM (<c>NetDemand &gt; 0</c>).</summary>
    public bool IsShortage => NetDemand > 0m;

    /// <summary>Tên cụm bán thành phẩm / chi tiết cấu thành (Multi-level Sub-Assembly, ví dụ: Cụm chân sồi, Mặt bàn veneer).</summary>
    public string? SubAssemblyName { get; set; }

    /// <summary>Quy cách phôi chi tiết bóc tách từ bản vẽ/Excel (ví dụ: <c>85 x 40 x 2.1 cm (1 thanh/SP)</c>).</summary>
    public string? ComponentDimensionSpec { get; set; }

    public string? Notes { get; set; }
}

public class BomDetailItemDto
{
    public long MaterialId { get; set; }
    public decimal NetQtyPerUnit { get; set; }
    public string? SubAssemblyName { get; set; }
    public string? ComponentDimensionSpec { get; set; }
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
    public string MaterialGroup { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal NetQtyPerUnit { get; set; }
    public decimal GrossTotalDemand { get; set; }

    /// <summary>Tồn thực tế trong kho (Physical Stock).</summary>
    public decimal PhysicalStock { get; set; }

    /// <summary>Tồn đã giữ chỗ cho đơn khác (Allocated Stock - bị loại trừ ngay từ đầu theo QT-03).</summary>
    public decimal AllocatedStock { get; set; }

    /// <summary>Tồn khả dụng để đối chiếu (<c>AvailableStock = PhysicalStock - AllocatedStock</c> - QT-03).</summary>
    public decimal AvailableStock { get; set; }

    /// <summary>Lượng nguyên vật liệu thực thiếu cần đề xuất mua hàng (<c>NetDemand = Max(0, GrossTotalDemand - AvailableStock)</c>).</summary>
    public decimal NetDemand { get; set; }

    /// <summary>Cờ cảnh báo thiếu hụt nguyên vật liệu (<c>NetDemand &gt; 0</c>).</summary>
    public bool IsShortage { get; set; }

    /// <summary>Trạng thái đối chiếu tồn kho của dòng vật tư (<c>SUFFICIENT</c> hoặc <c>SHORTAGE</c>).</summary>
    public string StockCoverageStatus => IsShortage ? "SHORTAGE" : "SUFFICIENT";
}

public class CalculateDemandResponseDto
{
    public int OrderedQty { get; set; }
    public decimal ScrapRatePercent { get; set; }
    public bool HasAnyShortage => CalculatedItems.Any(i => i.IsShortage);
    public int ShortageItemsCount => CalculatedItems.Count(i => i.IsShortage);
    public string RecommendedBomState => HasAnyShortage ? "WAITING_FOR_MATERIALS" : "READY_FOR_PRODUCTION";
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
    public string WorkstationCode { get; set; } = string.Empty;
    public string QualityGateName { get; set; } = string.Empty;
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

/// <summary>
/// DTO cấu hình quy trình sản xuất động và trạm dừng kỹ thuật (UC-37, BR-007, Report 4 BOM01).
/// </summary>
public class RoutingConfigDto
{
    public string? VersionCode { get; set; } = "v1.0";
    public List<RoutingStageConfigDto> Stages { get; set; } = new();
}

public class RoutingStageConfigDto
{
    public int StageSequence { get; set; }
    public string StageName { get; set; } = string.Empty;
    public bool IsHoldPoint { get; set; }
    public string? WorkstationCode { get; set; }
    public string? QualityGateName { get; set; }
}

public class RoutingDto
{
    public long RoutingId { get; set; }
    public long ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string VersionCode { get; set; } = "v1.0";
    public bool IsActive { get; set; } = true;
    public List<RoutingStageDto> Stages { get; set; } = new();
}
