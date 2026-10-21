using WMMS.Application.DTOs.BomRouting;

namespace WMMS.Application.Interfaces.Services;

/// <summary>
/// Giao diện Dịch vụ Bóc tách Định mức Nguyên vật liệu (BOM) &amp; Quy trình Sản xuất Động (Dynamic Routing) (Phân hệ 5 - QT-02, SC-10, SC-11, UC-38..UC-41).
/// </summary>
public interface IBomRoutingService
{
    Task<BomDto?> GetBomByIdAsync(long bomId);
    Task<BomDto?> GetBomByOrderItemIdAsync(long orderItemId);
    Task<InheritedBomDto> CheckExistingProductBomAsync(long productId);
    Task<List<VisualCatalogProductDto>> GetVisualCatalogAsync(string? category = null);
    Task<List<RoutingStageDto>> GetDefaultRoutingAsync(long productId);
    Task<CalculateDemandResponseDto> CalculateGrossDemandAsync(CalculateDemandRequestDto request);
    Task<BomDto> CreateBomAsync(CreateBomRequestDto request, long currentUserId);
    Task<BomDto> UpdateBomAsync(long bomId, UpdateBomRequestDto request, long currentUserId);
    Task<BomDto> ApproveBomAsync(long bomId, long currentUserId);
    Task<BomStagingPreviewDto> PreviewBomExcelStagingAsync(Stream fileStream, string fileName, long orderItemId, long productId);
    Task<BomDto> ConfirmBomStagingAsync(ConfirmBomStagingRequestDto request, long currentUserId);
}
