using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.KanbanQc;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;

namespace WMMS.API.Controllers;

/// <summary>
/// API Controller quản lý Điều độ, Phát hành Lệnh sản xuất, Tách lô thông minh &amp; Cưỡng chế tuần tự Kanban (Phân hệ 8 - SC-18, SC-19, SC-20, UC-62..UC-64).
/// </summary>
[ApiController]
[Route("api/v1/production-orders")]
[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Leader},{RoleConstants.Qc},{RoleConstants.Planner},{RoleConstants.Warehouse}")]
public class ProductionOrderController : ControllerBase
{
    private readonly IProductionOrderService _productionOrderService;
    private readonly IValidator<GenerateBatchesRequestDto> _generateBatchesValidator;
    private readonly IValidator<AdvanceStepRequestDto> _advanceStepValidator;
    private readonly IValidator<RecordQcInspectionDto> _qcInspectValidator;
    private readonly IValidator<RequestOverdriveDto> _requestOverdriveValidator;
    private readonly IValidator<ApproveOverdriveDto> _approveOverdriveValidator;

    public ProductionOrderController(
        IProductionOrderService productionOrderService,
        IValidator<GenerateBatchesRequestDto> generateBatchesValidator,
        IValidator<AdvanceStepRequestDto> advanceStepValidator,
        IValidator<RecordQcInspectionDto> qcInspectValidator,
        IValidator<RequestOverdriveDto> requestOverdriveValidator,
        IValidator<ApproveOverdriveDto> approveOverdriveValidator)
    {
        _productionOrderService = productionOrderService;
        _generateBatchesValidator = generateBatchesValidator;
        _advanceStepValidator = advanceStepValidator;
        _qcInspectValidator = qcInspectValidator;
        _requestOverdriveValidator = requestOverdriveValidator;
        _approveOverdriveValidator = approveOverdriveValidator;
    }

    /// <summary>
    /// SC-19 &amp; UC-62: Thuật toán tách lô sản xuất thông minh &amp; Phát hành Lệnh sản xuất (Production Order Release).
    /// Tách đơn hàng thành các thẻ Kanban Lots theo công suất trạm hoặc phân bổ tùy chỉnh, kiểm tra chốt chặn vật tư (BR-008) hoặc kích hoạt Overdrive (QT-06, KBN02).
    /// </summary>
    [HttpPost("generate-batches")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Leader}")]
    public async Task<ActionResult<ApiResponse<ProductionOrderSummaryDto>>> GenerateBatches([FromBody] GenerateBatchesRequestDto request)
    {
        var validation = await _generateBatchesValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<ProductionOrderSummaryDto>.Fail(
                "Dữ liệu yêu cầu phát hành lệnh sản xuất không hợp lệ.",
                validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        long currentUserId = GetCurrentUserId();
        var result = await _productionOrderService.GenerateProductionOrderAndSplitLotsAsync(request, currentUserId);
        return Ok(ApiResponse<ProductionOrderSummaryDto>.Ok(result, result.Message));
    }

    /// <summary>
    /// SC-19 &amp; QT-05: Cưỡng chế tuần tự di chuyển thẻ công đoạn trên Kanban (S -> S + 1).
    /// Bắt buộc điều kiện PassedQty == BatchQty tại công đoạn hiện tại (100% QC Pass - BR-008) mới cho phép chuyển bước.
    /// Tự động kích hoạt khóa chốt chặn kỹ thuật (Hold Point Lock Engine) nếu là lô Overdrive chưa xuất phụ kiện.
    /// </summary>
    [HttpPost("batches/{batchId:long}/advance-step")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Leader}")]
    public async Task<ActionResult<ApiResponse<ProductionBatchDto>>> AdvanceStep(
        long batchId,
        [FromBody] AdvanceStepRequestDto request)
    {
        var validation = await _advanceStepValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<ProductionBatchDto>.Fail(
                "Dữ liệu chuyển bước không hợp lệ.",
                validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        long currentUserId = GetCurrentUserId();
        var result = await _productionOrderService.AdvanceStepAsync(batchId, request, currentUserId);
        return Ok(ApiResponse<ProductionBatchDto>.Ok(result, $"Chuyển bước thành công! Thẻ lô {result.BatchCode} hiện đang ở công đoạn {result.CurrentStageSeq} ({result.CurrentStageName})."));
    }

    /// <summary>
    /// SC-19, QT-06 &amp; KBN01: Ghi nhận kiểm định chất lượng tại cổng KCS (100% Rule)
    /// và thực thi thuật toán tách nhánh 97%-3% (sinh thẻ nhánh phụ REWORK_SUB_CARD nếu phát hiện lỗi).
    /// </summary>
    [HttpPost("batches/{batchId:long}/qc-inspect")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Qc}")]
    public async Task<ActionResult<ApiResponse<QcInspectionResultDto>>> RecordQcInspection(
        long batchId,
        [FromBody] RecordQcInspectionDto request)
    {
        var validation = await _qcInspectValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<QcInspectionResultDto>.Fail(
                "Dữ liệu kiểm định QC không hợp lệ.",
                validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        long currentUserId = GetCurrentUserId();
        var result = await _productionOrderService.RecordQcInspectionAsync(batchId, request, currentUserId);
        return Ok(ApiResponse<QcInspectionResultDto>.Ok(result, result.Message));
    }

    /// <summary>
    /// SC-20, UC-63 &amp; QT-06: Tổ trưởng sản xuất (Line Leader) gửi yêu cầu kích hoạt chế độ chạy trước Overdrive
    /// khi cận hạn ETD nhưng thiếu phụ kiện ốc vít/sơn, thiết lập trạm dừng Hold Point.
    /// </summary>
    [HttpPost("batches/{batchId:long}/overdrive/request")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Leader}")]
    public async Task<ActionResult<ApiResponse<OverdriveOperationResultDto>>> RequestOverdrive(
        long batchId,
        [FromBody] RequestOverdriveDto request)
    {
        var validation = await _requestOverdriveValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<OverdriveOperationResultDto>.Fail(
                "Dữ liệu yêu cầu Overdrive không hợp lệ.",
                validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        long currentUserId = GetCurrentUserId();
        var result = await _productionOrderService.RequestOverdriveAsync(batchId, request, currentUserId);
        return Ok(ApiResponse<OverdriveOperationResultDto>.Ok(result, result.Message));
    }

    /// <summary>
    /// SC-20, KBN02 &amp; QT-06: Chủ xưởng (Factory Owner) phê duyệt hoặc từ chối yêu cầu Overdrive,
    /// cho phép sơ chế phôi gỗ thô trước trạm dừng Hold Point và lưu vết Audit Log.
    /// </summary>
    [HttpPost("batches/{batchId:long}/overdrive/approve")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner}")]
    public async Task<ActionResult<ApiResponse<OverdriveOperationResultDto>>> ApproveOverdrive(
        long batchId,
        [FromBody] ApproveOverdriveDto request)
    {
        var validation = await _approveOverdriveValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<OverdriveOperationResultDto>.Fail(
                "Dữ liệu quyết định Overdrive không hợp lệ.",
                validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        long currentUserId = GetCurrentUserId();
        var result = await _productionOrderService.ApproveOverdriveAsync(batchId, request, currentUserId);
        return Ok(ApiResponse<OverdriveOperationResultDto>.Ok(result, result.Message));
    }

    /// <summary>
    /// SC-20: Mở khóa chốt chặn Hold Point khi Thủ kho bấm xác nhận xuất kho phụ kiện bổ sung (Goods Issue).
    /// </summary>
    [HttpPost("batches/{batchId:long}/hold-point/unlock")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Warehouse}")]
    public async Task<ActionResult<ApiResponse<OverdriveOperationResultDto>>> UnlockHoldPoint(long batchId)
    {
        long currentUserId = GetCurrentUserId();
        var result = await _productionOrderService.UnlockHoldPointAsync(batchId, currentUserId);
        return Ok(ApiResponse<OverdriveOperationResultDto>.Ok(result, result.Message));
    }

    /// <summary>
    /// Lấy danh sách thẻ lô Kanban theo dòng sản phẩm đơn hàng (OrderItemId).
    /// </summary>
    [HttpGet("order-item/{orderItemId:long}")]
    public async Task<ActionResult<ApiResponse<List<ProductionBatchDto>>>> GetBatchesByOrderItem(long orderItemId)
    {
        var batches = await _productionOrderService.GetBatchesByOrderItemIdAsync(orderItemId);
        return Ok(ApiResponse<List<ProductionBatchDto>>.Ok(batches, $"Lấy danh sách {batches.Count} thẻ lô của sản phẩm đơn hàng #{orderItemId} thành công."));
    }

    /// <summary>
    /// Lấy danh sách thẻ lô Kanban của toàn bộ đơn hàng (OrderId).
    /// </summary>
    [HttpGet("order/{orderId:long}")]
    public async Task<ActionResult<ApiResponse<List<ProductionBatchDto>>>> GetBatchesByOrder(long orderId)
    {
        var batches = await _productionOrderService.GetBatchesByOrderIdAsync(orderId);
        return Ok(ApiResponse<List<ProductionBatchDto>>.Ok(batches, $"Lấy danh sách {batches.Count} thẻ lô của đơn hàng #{orderId} thành công."));
    }

    /// <summary>
    /// Lấy danh sách thẻ lô Kanban theo Không gian đơn hàng (Order Space Key - ví dụ: SPACE-PO-110205).
    /// </summary>
    [HttpGet("space/{spaceKey}")]
    public async Task<ActionResult<ApiResponse<List<ProductionBatchDto>>>> GetBatchesBySpace(string spaceKey)
    {
        var batches = await _productionOrderService.GetBatchesBySpaceKeyAsync(spaceKey);
        return Ok(ApiResponse<List<ProductionBatchDto>>.Ok(batches, $"Lấy danh sách {batches.Count} thẻ lô thuộc không gian {spaceKey} thành công."));
    }

    /// <summary>
    /// Lấy thông tin chi tiết một thẻ lô Kanban theo BatchId kèm đầy đủ tiến độ các công đoạn (Steps) và cổng QC.
    /// </summary>
    [HttpGet("batches/{batchId:long}")]
    public async Task<ActionResult<ApiResponse<ProductionBatchDto>>> GetBatchById(long batchId)
    {
        var batch = await _productionOrderService.GetBatchByIdAsync(batchId);
        if (batch == null)
        {
            return NotFound(ApiResponse<ProductionBatchDto>.Fail($"Thẻ lô sản xuất với ID {batchId} không tồn tại."));
        }
        return Ok(ApiResponse<ProductionBatchDto>.Ok(batch, "Lấy thông tin chi tiết thẻ lô thành công."));
    }

    /// <summary>
    /// SC-18: Lấy danh mục Trạm gia công tiêu chuẩn của xưởng gỗ (Workstation Catalog).
    /// </summary>
    [HttpGet("workstations")]
    public ActionResult<ApiResponse<List<StandardWorkstationDto>>> GetWorkstations()
    {
        var workstations = _productionOrderService.GetStandardWorkstations();
        return Ok(ApiResponse<List<StandardWorkstationDto>>.Ok(workstations, "Lấy danh mục trạm gia công thành công (SC-18)."));
    }

    /// <summary>
    /// SC-18: Lấy danh mục Cổng kiểm tra chất lượng &amp; Trạm dừng kỹ thuật bắt buộc (Quality Gates &amp; Hold Points Catalog).
    /// </summary>
    [HttpGet("quality-gates")]
    public ActionResult<ApiResponse<List<StandardQualityGateDto>>> GetQualityGates()
    {
        var gates = _productionOrderService.GetStandardQualityGates();
        return Ok(ApiResponse<List<StandardQualityGateDto>>.Ok(gates, "Lấy danh mục cổng kiểm soát chất lượng thành công (SC-18)."));
    }

    private long GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(claim) && long.TryParse(claim, out var userId))
        {
            return userId;
        }

        // Fallback người dùng mặc định (Admin / Quản đốc ID = 1) khi kiểm thử hoặc chưa đăng nhập
        return 1;
    }
}

