using System.Text.Json;
using AutoMapper;
using WMMS.Application.DTOs.BomRouting;
using WMMS.Application.Interfaces.External;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;
using WMMS.Domain.Entities.CatalogBom;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Enums;
using WMMS.Domain.Exceptions;

namespace WMMS.Application.Services.BomRouting;

/// <summary>
/// Triển khai Dịch vụ Bóc tách Định mức Nguyên vật liệu (BOM) &amp; Quy trình Sản xuất Động (Phân hệ 5 - QT-02, SC-10, SC-11, UC-38..UC-41).
/// Tuân thủ đầy đủ quy tắc nghiệp vụ:
/// - BR-001: Mỗi dòng đơn hàng (<c>OrderItem</c>) chỉ có duy nhất một bản BOM Active/Draft.
/// - BR-002: Tỷ lệ hao hụt gỗ tự nhiên đối chiếu động theo tham số cấu hình hệ thống (<c>DEFAULT_SCRAP_MIN</c> .. <c>DEFAULT_SCRAP_MAX</c>).
/// - BR-011: Kiểm tra và xác nhận import BOM từ vùng đệm Staging Grid.
/// - GBR-05: Ghi vết kiểm toán bất biến (<c>audit_logs</c>) khi khởi tạo, cập nhật và phê duyệt/chốt BOM.
/// </summary>
public class BomRoutingService : IBomRoutingService
{
    private readonly IBomRepository _bomRepository;
    private readonly IRoutingRepository _routingRepository;
    private readonly IProductRepository _productRepository;
    private readonly IMaterialRepository _materialRepository;
    private readonly IOrderItemRepository _orderItemRepository;
    private readonly ISystemConfigRepository _systemConfigRepository;
    private readonly IExcelBomParser _excelBomParser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public BomRoutingService(
        IBomRepository bomRepository,
        IRoutingRepository routingRepository,
        IProductRepository productRepository,
        IMaterialRepository materialRepository,
        IOrderItemRepository orderItemRepository,
        ISystemConfigRepository systemConfigRepository,
        IExcelBomParser excelBomParser,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _bomRepository = bomRepository;
        _routingRepository = routingRepository;
        _productRepository = productRepository;
        _materialRepository = materialRepository;
        _orderItemRepository = orderItemRepository;
        _systemConfigRepository = systemConfigRepository;
        _excelBomParser = excelBomParser;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<BomDto?> GetBomByIdAsync(long bomId)
    {
        var bom = await _bomRepository.GetByIdAsync(bomId);
        if (bom == null) return null;

        var dto = _mapper.Map<BomDto>(bom);
        var routing = await _routingRepository.GetActiveRoutingByProductIdAsync(bom.ProductId);
        if (routing != null)
        {
            dto.RoutingStages = _mapper.Map<List<RoutingStageDto>>(routing.RoutingStages.OrderBy(s => s.StageSequence));
        }

        return dto;
    }

    public async Task<BomDto?> GetBomByOrderItemIdAsync(long orderItemId)
    {
        var bom = await _bomRepository.GetByOrderItemIdAsync(orderItemId);
        if (bom == null) return null;

        var dto = _mapper.Map<BomDto>(bom);
        var routing = await _routingRepository.GetActiveRoutingByProductIdAsync(bom.ProductId);
        if (routing != null)
        {
            dto.RoutingStages = _mapper.Map<List<RoutingStageDto>>(routing.RoutingStages.OrderBy(s => s.StageSequence));
        }

        return dto;
    }

    public async Task<InheritedBomDto> CheckExistingProductBomAsync(long productId)
    {
        var existingBom = await _bomRepository.GetLatestActiveBomByProductIdAsync(productId);
        var routing = await _routingRepository.GetActiveRoutingByProductIdAsync(productId);

        var result = new InheritedBomDto
        {
            HasExistingBom = existingBom != null,
            SourceBomId = existingBom?.BomId,
            ScrapRatePercent = existingBom?.ScrapRatePercent ?? 3.00m
        };

        if (existingBom != null)
        {
            result.Details = _mapper.Map<List<BomDetailDto>>(existingBom.BomDetails);
        }

        if (routing != null)
        {
            result.RoutingStages = _mapper.Map<List<RoutingStageDto>>(routing.RoutingStages.OrderBy(s => s.StageSequence));
        }
        else
        {
            result.RoutingStages = GetStandardWoodworkingStages();
        }

        return result;
    }

    public async Task<List<VisualCatalogProductDto>> GetVisualCatalogAsync(string? category = null)
    {
        var products = await _productRepository.GetAllAsync(category);
        var dtos = _mapper.Map<List<VisualCatalogProductDto>>(products);

        foreach (var dto in dtos)
        {
            if (!dto.DefaultRoutingStages.Any())
            {
                dto.DefaultRoutingStages = GetStandardWoodworkingStages();
            }
        }

        return dtos;
    }

    public async Task<List<RoutingStageDto>> GetDefaultRoutingAsync(long productId)
    {
        var routing = await _routingRepository.GetActiveRoutingByProductIdAsync(productId);
        if (routing != null && routing.RoutingStages.Any())
        {
            return _mapper.Map<List<RoutingStageDto>>(routing.RoutingStages.OrderBy(s => s.StageSequence));
        }

        return GetStandardWoodworkingStages();
    }

    public async Task<CalculateDemandResponseDto> CalculateGrossDemandAsync(CalculateDemandRequestDto request)
    {
        await ValidateScrapRateAsync(request.ScrapRatePercent);

        var materialIds = request.Items.Select(i => i.MaterialId).Distinct().ToList();
        var materials = new Dictionary<long, Material>();
        foreach (var id in materialIds)
        {
            var mat = await _materialRepository.GetByIdAsync(id);
            if (mat != null) materials[id] = mat;
        }

        var results = new List<CalculateDemandItemResultDto>();
        foreach (var item in request.Items)
        {
            materials.TryGetValue(item.MaterialId, out var mat);
            var grossDemand = CalculateGrossDemand(item.NetQtyPerUnit, request.OrderedQty, request.ScrapRatePercent);

            results.Add(new CalculateDemandItemResultDto
            {
                MaterialId = item.MaterialId,
                MaterialCode = mat?.MaterialCode ?? $"MAT-{item.MaterialId}",
                MaterialName = mat?.MaterialName ?? "Unknown Material",
                UnitOfMeasure = mat?.UnitOfMeasure ?? "PCS",
                NetQtyPerUnit = item.NetQtyPerUnit,
                GrossTotalDemand = grossDemand
            });
        }

        return new CalculateDemandResponseDto
        {
            OrderedQty = request.OrderedQty,
            ScrapRatePercent = request.ScrapRatePercent,
            CalculatedItems = results
        };
    }

    public async Task<BomDto> CreateBomAsync(CreateBomRequestDto request, long currentUserId)
    {
        // 1. Enforce BR-001 (Tính duy nhất của Active BOM)
        var exists = await _bomRepository.HasBomForOrderItemAsync(request.OrderItemId);
        if (exists)
        {
            throw new ActiveBomConflictException(
                $"BOM for OrderItemId {request.OrderItemId} already exists. Enforcing BR-001: Only one Active BOM is allowed per order item.");
        }

        // 2. Validate BR-002 (Tỷ lệ hao hụt tự nhiên theo SystemConfig DEFAULT_SCRAP_MIN .. DEFAULT_SCRAP_MAX)
        await ValidateScrapRateAsync(request.ScrapRatePercent);

        // 3. Verify OrderItem & Product
        var orderItem = await _orderItemRepository.GetByIdAsync(request.OrderItemId);
        if (orderItem == null)
        {
            throw new NotFoundException($"OrderItem with ID {request.OrderItemId} was not found.");
        }

        var product = await _productRepository.GetByIdAsync(request.ProductId);
        if (product == null)
        {
            throw new NotFoundException($"Product with ID {request.ProductId} was not found.");
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;
            var bomHeader = new BomHeader
            {
                OrderItemId = request.OrderItemId,
                ProductId = request.ProductId,
                CreatedBy = currentUserId,
                ScrapRatePercent = request.ScrapRatePercent,
                BomStatus = BomStatusEnum.DRAFT,
                CreatedAt = now
            };

            await _bomRepository.AddAsync(bomHeader);
            await _unitOfWork.SaveChangesAsync();

            // Create Bom Details
            var details = new List<BomDetail>();
            foreach (var d in request.Details)
            {
                var grossDemand = CalculateGrossDemand(d.NetQtyPerUnit, orderItem.OrderedQty, request.ScrapRatePercent);
                details.Add(new BomDetail
                {
                    BomId = bomHeader.BomId,
                    MaterialId = d.MaterialId,
                    NetQtyPerUnit = d.NetQtyPerUnit,
                    GrossTotalDemand = grossDemand,
                    Notes = d.Notes
                });
            }

            await _bomRepository.AddDetailsAsync(details);

            // Handle Custom Dynamic Routing (QT-02: cho phép custom thứ tự công đoạn + IsHoldPoint)
            if (request.CustomRoutingStages != null && request.CustomRoutingStages.Any())
            {
                var stages = request.CustomRoutingStages.Select(s => new RoutingStage
                {
                    StageSequence = s.StageSequence,
                    StageName = s.StageName,
                    IsHoldPoint = s.IsHoldPoint
                });
                await _routingRepository.SaveCustomRoutingAsync(request.ProductId, stages);
            }

            // Update OrderItem Status to BOM_DRAFT
            orderItem.Status = OrderItemStatusEnum.BOM_DRAFT;
            await _orderItemRepository.UpdateAsync(orderItem);

            // Ghi vết kiểm toán bất biến GBR-05 vào bảng audit_logs
            var afterStateJson = JsonSerializer.Serialize(new
            {
                bomHeader.BomId,
                bomHeader.OrderItemId,
                bomHeader.ProductId,
                bomHeader.ScrapRatePercent,
                BomStatus = bomHeader.BomStatus.ToString(),
                MaterialLinesCount = details.Count
            });

            await _bomRepository.AddAuditLogAsync(new AuditLog
            {
                UserId = currentUserId,
                ActionType = AuditActionConstants.BomCreated,
                EntityName = "BomHeader",
                EntityId = bomHeader.BomId.ToString(),
                BeforeState = null,
                AfterState = afterStateJson,
                Reason = $"Created BOM in DRAFT state for OrderItem #{request.OrderItemId} with {request.ScrapRatePercent:0.##}% scrap rate (SC-10, BR-001, BR-002).",
                CreatedAt = now
            });

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            return (await GetBomByIdAsync(bomHeader.BomId))!;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<BomDto> UpdateBomAsync(long bomId, UpdateBomRequestDto request, long currentUserId)
    {
        var bomHeader = await _bomRepository.GetByIdAsync(bomId);
        if (bomHeader == null)
        {
            throw new NotFoundException($"Bom with ID {bomId} was not found.");
        }

        // Validate BR-002
        await ValidateScrapRateAsync(request.ScrapRatePercent);

        var orderItem = await _orderItemRepository.GetByIdAsync(bomHeader.OrderItemId);
        int orderedQty = orderItem?.OrderedQty ?? 1;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;
            var beforeStateJson = JsonSerializer.Serialize(new
            {
                bomHeader.BomId,
                bomHeader.ScrapRatePercent,
                BomStatus = bomHeader.BomStatus.ToString(),
                MaterialLinesCount = bomHeader.BomDetails.Count
            });

            bomHeader.ScrapRatePercent = request.ScrapRatePercent;
            bomHeader.UpdatedAt = now;

            await _bomRepository.UpdateAsync(bomHeader);

            // Replace Details
            if (bomHeader.BomDetails.Any())
            {
                await _bomRepository.RemoveDetailsAsync(bomHeader.BomDetails.ToList());
            }

            var newDetails = new List<BomDetail>();
            foreach (var d in request.Details)
            {
                var grossDemand = CalculateGrossDemand(d.NetQtyPerUnit, orderedQty, request.ScrapRatePercent);
                newDetails.Add(new BomDetail
                {
                    BomId = bomHeader.BomId,
                    MaterialId = d.MaterialId,
                    NetQtyPerUnit = d.NetQtyPerUnit,
                    GrossTotalDemand = grossDemand,
                    Notes = d.Notes
                });
            }

            await _bomRepository.AddDetailsAsync(newDetails);

            // Handle Custom Dynamic Routing
            if (request.CustomRoutingStages != null && request.CustomRoutingStages.Any())
            {
                var stages = request.CustomRoutingStages.Select(s => new RoutingStage
                {
                    StageSequence = s.StageSequence,
                    StageName = s.StageName,
                    IsHoldPoint = s.IsHoldPoint
                });
                await _routingRepository.SaveCustomRoutingAsync(bomHeader.ProductId, stages);
            }

            var afterStateJson = JsonSerializer.Serialize(new
            {
                bomHeader.BomId,
                bomHeader.ScrapRatePercent,
                BomStatus = bomHeader.BomStatus.ToString(),
                MaterialLinesCount = newDetails.Count
            });

            // Ghi vết kiểm toán bất biến GBR-05 vào bảng audit_logs
            await _bomRepository.AddAuditLogAsync(new AuditLog
            {
                UserId = currentUserId,
                ActionType = AuditActionConstants.BomUpdated,
                EntityName = "BomHeader",
                EntityId = bomHeader.BomId.ToString(),
                BeforeState = beforeStateJson,
                AfterState = afterStateJson,
                Reason = $"Updated BOM #{bomId} material breakdown and recalculated gross demand with {request.ScrapRatePercent:0.##}% scrap rate (SC-10, BR-002).",
                CreatedAt = now
            });

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            return (await GetBomByIdAsync(bomHeader.BomId))!;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<BomDto> ApproveBomAsync(long bomId, long currentUserId)
    {
        var bomHeader = await _bomRepository.GetByIdAsync(bomId);
        if (bomHeader == null)
        {
            throw new NotFoundException($"Bom with ID {bomId} was not found.");
        }

        if (!bomHeader.BomDetails.Any())
        {
            throw new DomainException("Cannot approve an empty BOM without material lines.", "BOM_EMPTY");
        }

        await ValidateScrapRateAsync(bomHeader.ScrapRatePercent);

        var orderItem = await _orderItemRepository.GetByIdAsync(bomHeader.OrderItemId);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;
            var previousStatus = bomHeader.BomStatus.ToString();

            bomHeader.BomStatus = BomStatusEnum.READY_FOR_PRODUCTION;
            bomHeader.UpdatedAt = now;
            await _bomRepository.UpdateAsync(bomHeader);

            if (orderItem != null)
            {
                orderItem.Status = OrderItemStatusEnum.READY_PROD;
                await _orderItemRepository.UpdateAsync(orderItem);
            }

            await _bomRepository.AddAuditLogAsync(new AuditLog
            {
                UserId = currentUserId,
                ActionType = AuditActionConstants.BomApproved,
                EntityName = "BomHeader",
                EntityId = bomHeader.BomId.ToString(),
                BeforeState = JsonSerializer.Serialize(new { BomStatus = previousStatus }),
                AfterState = JsonSerializer.Serialize(new
                {
                    BomStatus = bomHeader.BomStatus.ToString(),
                    OrderItemStatus = orderItem?.Status.ToString()
                }),
                Reason = $"Locked and approved BOM #{bomId} to READY_FOR_PRODUCTION (QT-02, SC-10).",
                CreatedAt = now
            });

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            return (await GetBomByIdAsync(bomHeader.BomId))!;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<BomStagingPreviewDto> PreviewBomExcelStagingAsync(Stream fileStream, string fileName, long orderItemId, long productId)
    {
        var preview = await _excelBomParser.ParseBomExcelAsync(fileStream, fileName);

        // Resolve Material Codes against DB to validate existence
        var codes = preview.Rows
            .Where(r => !string.IsNullOrWhiteSpace(r.MaterialCode))
            .Select(r => r.MaterialCode.Trim())
            .Distinct()
            .ToList();

        var materialsMap = await _materialRepository.GetByCodesAsync(codes);

        foreach (var row in preview.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.MaterialCode))
            {
                row.IsValid = false;
                row.Errors.Add("Mã nguyên vật liệu (MaterialCode) không được để trống.");
            }
            else if (!materialsMap.TryGetValue(row.MaterialCode.Trim(), out var material))
            {
                row.IsValid = false;
                row.Errors.Add($"Mã nguyên vật liệu '{row.MaterialCode}' không tồn tại trong danh mục Master Data.");
            }
            else
            {
                row.ResolvedMaterialId = material.MaterialId;
                if (string.IsNullOrWhiteSpace(row.MaterialName))
                {
                    row.MaterialName = material.MaterialName;
                }
                if (string.IsNullOrWhiteSpace(row.UnitOfMeasure))
                {
                    row.UnitOfMeasure = material.UnitOfMeasure;
                }
                row.MaterialGroup = material.MaterialGroup.ToString();
            }

            if (row.NetQtyPerUnit <= 0)
            {
                row.IsValid = false;
                row.Errors.Add("Định mức 1 sản phẩm (NetQtyPerUnit) phải lớn hơn 0.");
            }
        }

        preview.ValidRows = preview.Rows.Count(r => r.IsValid);
        preview.ErrorRows = preview.Rows.Count(r => !r.IsValid);

        return preview;
    }

    public async Task<BomDto> ConfirmBomStagingAsync(ConfirmBomStagingRequestDto request, long currentUserId)
    {
        var invalidRows = request.Rows.Where(r => !r.IsValid || r.Errors.Any()).ToList();
        if (invalidRows.Any())
        {
            throw new StagingValidationException(
                $"Cannot confirm BOM import. There are {invalidRows.Count} invalid row(s) in staging grid. Please correct all errors before confirming.");
        }

        // Convert staging rows to CreateBomRequestDto
        var detailDtos = new List<BomDetailItemDto>();
        foreach (var row in request.Rows)
        {
            long materialId = row.ResolvedMaterialId ?? 0;
            if (materialId == 0)
            {
                var mat = await _materialRepository.GetByCodeAsync(row.MaterialCode.Trim());
                if (mat == null)
                {
                    throw new StagingValidationException($"Material code '{row.MaterialCode}' cannot be resolved.");
                }
                materialId = mat.MaterialId;
            }

            detailDtos.Add(new BomDetailItemDto
            {
                MaterialId = materialId,
                NetQtyPerUnit = row.NetQtyPerUnit,
                Notes = row.Notes
            });
        }

        var createRequest = new CreateBomRequestDto
        {
            OrderItemId = request.OrderItemId,
            ProductId = request.ProductId,
            ScrapRatePercent = request.ScrapRatePercent,
            Details = detailDtos
        };

        return await CreateBomAsync(createRequest, currentUserId);
    }

    private async Task ValidateScrapRateAsync(decimal scrapRate)
    {
        var minScrap = await _systemConfigRepository.GetDecimalConfigAsync(SystemConfigKeys.DefaultScrapMin, 3.00m);
        var maxScrap = await _systemConfigRepository.GetDecimalConfigAsync(SystemConfigKeys.DefaultScrapMax, 5.00m);

        // Đối chiếu chính xác với ngưỡng [DEFAULT_SCRAP_MIN .. DEFAULT_SCRAP_MAX] cấu hình trong system_configs (BR-002, UC-15)
        if (scrapRate < minScrap || scrapRate > maxScrap)
        {
            throw new InvalidScrapRateException(
                $"Scrap rate {scrapRate:F2}% violates BR-002: Natural wood scrap rate must be between {minScrap:F2}% and {maxScrap:F2}%.");
        }
    }

    private static decimal CalculateGrossDemand(decimal netQtyPerUnit, int orderedQty, decimal scrapRatePercent)
    {
        // Công thức BR-002: GrossTotalDemand = (NetQtyPerUnit * OrderedQty) * (1 + ScrapRatePercent / 100)
        var total = (netQtyPerUnit * orderedQty) * (1m + (scrapRatePercent / 100m));
        return Math.Round(total, 4);
    }

    private static List<RoutingStageDto> GetStandardWoodworkingStages()
    {
        return new List<RoutingStageDto>
        {
            new() { StageSequence = 1, StageName = "1. Pha phôi gỗ", IsHoldPoint = false },
            new() { StageSequence = 2, StageName = "2. Cắt/Xẻ chi tiết", IsHoldPoint = false },
            new() { StageSequence = 3, StageName = "3. Bào & Chà nhám", IsHoldPoint = false },
            new() { StageSequence = 4, StageName = "4. Khoan & Tạo mộng", IsHoldPoint = false },
            new() { StageSequence = 5, StageName = "5. Lắp ráp & Gắn phụ kiện", IsHoldPoint = true },
            new() { StageSequence = 6, StageName = "6. Sơn lót & Sơn PU hoàn thiện", IsHoldPoint = false },
            new() { StageSequence = 7, StageName = "7. Đóng gói & Dán tem nhãn", IsHoldPoint = false }
        };
    }
}
