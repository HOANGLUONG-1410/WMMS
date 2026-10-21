using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.BomRouting;
using WMMS.Application.Interfaces.Services;

namespace WMMS.API.Controllers;

[ApiController]
[Route("api/v1/bom")]
public class BomRoutingController : ControllerBase
{
    private readonly IBomRoutingService _bomRoutingService;
    private readonly IValidator<CreateBomRequestDto> _createBomValidator;
    private readonly IValidator<UpdateBomRequestDto> _updateBomValidator;
    private readonly IValidator<CalculateDemandRequestDto> _calculateDemandValidator;
    private readonly IValidator<ConfirmBomStagingRequestDto> _confirmStagingValidator;

    public BomRoutingController(
        IBomRoutingService bomRoutingService,
        IValidator<CreateBomRequestDto> createBomValidator,
        IValidator<UpdateBomRequestDto> updateBomValidator,
        IValidator<CalculateDemandRequestDto> calculateDemandValidator,
        IValidator<ConfirmBomStagingRequestDto> confirmStagingValidator)
    {
        _bomRoutingService = bomRoutingService;
        _createBomValidator = createBomValidator;
        _updateBomValidator = updateBomValidator;
        _calculateDemandValidator = calculateDemandValidator;
        _confirmStagingValidator = confirmStagingValidator;
    }

    /// <summary>
    /// Lấy thông tin chi tiết của một BOM theo bomId.
    /// </summary>
    [HttpGet("{bomId:long}")]
    public async Task<ActionResult<ApiResponse<BomDto>>> GetById(long bomId)
    {
        var bom = await _bomRoutingService.GetBomByIdAsync(bomId);
        if (bom == null)
        {
            return NotFound(ApiResponse<BomDto>.Fail($"BOM với ID {bomId} không tồn tại."));
        }
        return Ok(ApiResponse<BomDto>.Ok(bom));
    }

    /// <summary>
    /// Lấy thông tin BOM của một dòng sản phẩm đơn hàng (OrderItemId).
    /// </summary>
    [HttpGet("order-item/{orderItemId:long}")]
    public async Task<ActionResult<ApiResponse<BomDto>>> GetByOrderItemId(long orderItemId)
    {
        var bom = await _bomRoutingService.GetBomByOrderItemIdAsync(orderItemId);
        if (bom == null)
        {
            return NotFound(ApiResponse<BomDto>.Fail($"OrderItem với ID {orderItemId} chưa có BOM."));
        }
        return Ok(ApiResponse<BomDto>.Ok(bom));
    }

    /// <summary>
    /// QT-02: Kiểm tra sản phẩm đã có BOM sẵn trong hệ thống chưa để kế thừa (Inherited BOM & Dynamic Routing).
    /// </summary>
    [HttpGet("check-product/{productId:long}")]
    public async Task<ActionResult<ApiResponse<InheritedBomDto>>> CheckProductBom(long productId)
    {
        var result = await _bomRoutingService.CheckExistingProductBomAsync(productId);
        return Ok(ApiResponse<InheritedBomDto>.Ok(result, result.HasExistingBom ? "Kế thừa BOM sẵn có thành công." : "Sản phẩm chưa có BOM, nạp quy trình mẫu."));
    }

    /// <summary>
    /// QT-02: Lấy danh mục sản phẩm trực quan (Visual Catalog) kèm quy trình sản xuất mẫu theo nhóm SP (Bàn, Ghế, Tủ...).
    /// </summary>
    [HttpGet("visual-catalog")]
    public async Task<ActionResult<ApiResponse<List<VisualCatalogProductDto>>>> GetVisualCatalog([FromQuery] string? category = null)
    {
        var catalog = await _bomRoutingService.GetVisualCatalogAsync(category);
        return Ok(ApiResponse<List<VisualCatalogProductDto>>.Ok(catalog));
    }

    /// <summary>
    /// QT-02: Lấy quy trình sản xuất mẫu (Default Routing) của sản phẩm.
    /// </summary>
    [HttpGet("default-routing/{productId:long}")]
    public async Task<ActionResult<ApiResponse<List<RoutingStageDto>>>> GetDefaultRouting(long productId)
    {
        var routing = await _bomRoutingService.GetDefaultRoutingAsync(productId);
        return Ok(ApiResponse<List<RoutingStageDto>>.Ok(routing));
    }

    /// <summary>
    /// QT-02 & BR-002: Xem trước tính toán tổng nhu cầu nguyên vật liệu có hao hụt:
    /// GrossTotalDemand = (NetQtyPerUnit * OrderedQty) * (1 + ScrapRatePercent / 100)
    /// </summary>
    [HttpPost("calculate-demand")]
    public async Task<ActionResult<ApiResponse<CalculateDemandResponseDto>>> CalculateDemand([FromBody] CalculateDemandRequestDto request)
    {
        var validation = await _calculateDemandValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<CalculateDemandResponseDto>.Fail("Dữ liệu tính toán không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        var result = await _bomRoutingService.CalculateGrossDemandAsync(request);
        return Ok(ApiResponse<CalculateDemandResponseDto>.Ok(result));
    }

    /// <summary>
    /// SC-10, QT-02, BR-001 & BR-002: Bóc tách & Khởi tạo BOM mới ở trạng thái Draft kèm quy trình tùy biến.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<BomDto>>> CreateBom([FromBody] CreateBomRequestDto request)
    {
        var validation = await _createBomValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<BomDto>.Fail("Dữ liệu BOM không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        long currentUserId = GetCurrentUserId();
        var result = await _bomRoutingService.CreateBomAsync(request, currentUserId);
        return CreatedAtAction(nameof(GetById), new { bomId = result.BomId }, ApiResponse<BomDto>.Ok(result, "Tạo bản BOM ở trạng thái Draft thành công."));
    }

    /// <summary>
    /// SC-10: Cập nhật thông tin BOM (định mức, tỷ lệ hao hụt hoặc quy trình công đoạn).
    /// </summary>
    [HttpPut("{bomId:long}")]
    public async Task<ActionResult<ApiResponse<BomDto>>> UpdateBom(long bomId, [FromBody] UpdateBomRequestDto request)
    {
        var validation = await _updateBomValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<BomDto>.Fail("Dữ liệu cập nhật không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        long currentUserId = GetCurrentUserId();
        var result = await _bomRoutingService.UpdateBomAsync(bomId, request, currentUserId);
        return Ok(ApiResponse<BomDto>.Ok(result, "Cập nhật BOM thành công."));
    }

    /// <summary>
    /// SC-11 & BR-011: Tải file Excel BOM lên vùng đệm Staging Grid UI, tự động kiểm tra định dạng và báo lỗi các ô/dòng không hợp lệ.
    /// </summary>
    [HttpPost("staging-preview")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<BomStagingPreviewDto>>> PreviewStaging(
        IFormFile file,
        [FromForm] long orderItemId,
        [FromForm] long productId)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<BomStagingPreviewDto>.Fail("Vui lòng chọn file Excel BOM hợp lệ (.xlsx, .xls)."));
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) &&
            !file.FileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse<BomStagingPreviewDto>.Fail("Định dạng file không được hỗ trợ. Chỉ chấp nhận file Excel (.xlsx, .xls)."));
        }

        using var stream = file.OpenReadStream();
        var preview = await _bomRoutingService.PreviewBomExcelStagingAsync(stream, file.FileName, orderItemId, productId);
        return Ok(ApiResponse<BomStagingPreviewDto>.Ok(preview, preview.HasErrors ? "File có dòng chứa lỗi cần kiểm tra trên Staging Grid." : "Dữ liệu hợp lệ sẵn sàng Import."));
    }

    /// <summary>
    /// SC-11 & BR-011: Xác nhận Confirm dữ liệu từ Staging Grid sau khi KHVT kiểm tra/sửa lỗi để lưu vào CSDL chính.
    /// </summary>
    [HttpPost("staging-confirm")]
    public async Task<ActionResult<ApiResponse<BomDto>>> ConfirmStaging([FromBody] ConfirmBomStagingRequestDto request)
    {
        var validation = await _confirmStagingValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<BomDto>.Fail("Dữ liệu xác nhận Staging không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        long currentUserId = GetCurrentUserId();
        var result = await _bomRoutingService.ConfirmBomStagingAsync(request, currentUserId);
        return Ok(ApiResponse<BomDto>.Ok(result, "Import BOM thành công và chuyển trạng thái sang Draft."));
    }

    private long GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(claim) && long.TryParse(claim, out var userId))
        {
            return userId;
        }

        // Fallback to default user (Admin / Planner ID: 1) in development or when unauthenticated
        return 1;
    }
}
