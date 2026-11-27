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
    private readonly IValidator<ReinspectSubBatchDto> _reinspectSubBatchValidator;
    private readonly IValidator<MergeSubBatchDto> _mergeSubBatchValidator;
    private readonly IValidator<ContinueSubBatchIndependentlyDto> _continueSubBatchValidator;
    private readonly IValidator<IssueReplenishLotRequestDto> _issueReplenishLotValidator;

    public ProductionOrderController(
        IProductionOrderService productionOrderService,
        IValidator<GenerateBatchesRequestDto> generateBatchesValidator,
        IValidator<AdvanceStepRequestDto> advanceStepValidator,
        IValidator<RecordQcInspectionDto> qcInspectValidator,
        IValidator<RequestOverdriveDto> requestOverdriveValidator,
        IValidator<ApproveOverdriveDto> approveOverdriveValidator,
        IValidator<ReinspectSubBatchDto> reinspectSubBatchValidator,
        IValidator<MergeSubBatchDto> mergeSubBatchValidator,
        IValidator<ContinueSubBatchIndependentlyDto> continueSubBatchValidator,
        IValidator<IssueReplenishLotRequestDto> issueReplenishLotValidator)
    {
        _productionOrderService = productionOrderService;
        _generateBatchesValidator = generateBatchesValidator;
        _advanceStepValidator = advanceStepValidator;
        _qcInspectValidator = qcInspectValidator;
        _requestOverdriveValidator = requestOverdriveValidator;
        _approveOverdriveValidator = approveOverdriveValidator;
        _reinspectSubBatchValidator = reinspectSubBatchValidator;
        _mergeSubBatchValidator = mergeSubBatchValidator;
        _continueSubBatchValidator = continueSubBatchValidator;
        _issueReplenishLotValidator = issueReplenishLotValidator;
    }

    /// <summary>
    /// SC-19 &amp; UC-62: Thuật toán tách lô sản xuất thông minh &amp; Phát hành Lệnh sản xuất (Production Order Release).
    /// </summary>
    /// <remarks>
    /// API cho phép Quản đốc hoặc Tổ trưởng phát hành Lệnh sản xuất từ dòng sản phẩm đơn hàng (<c>OrderItemId</c>):
    /// <list type="bullet">
    ///   <item><b>Tách lô tự động:</b> Chia đều theo công suất định mức của trạm máy (<c>MaxBatchSize</c>, mặc định 100 SP/lô).</item>
    ///   <item><b>Tách lô tùy chỉnh:</b> Phân bổ theo mảng số lượng tùy biến (<c>CustomBatchSplits</c>, ví dụ: [100, 100, 50]).</item>
    ///   <item><b>Chốt chặn vật tư (BR-008):</b> Nếu kho đã bấm xuất đủ 100% NVL (<c>ISSUED_TO_SHOP</c>), các thẻ sinh ra ở trạng thái <c>IN_PROGRESS</c>. Nếu chưa xuất đủ NVL, thẻ sinh ra ở trạng thái khóa cứng <c>LOCKED</c>.</item>
    ///   <item><b>Cơ chế Overdrive (QT-06, KBN02):</b> Cho phép chạy trước các công đoạn sơ chế gỗ thô khi thiếu phụ kiện kèm lý do giải trình (<c>Justification</c>).</item>
    /// </list>
    /// </remarks>
    /// <param name="request">DTO chứa OrderItemId, MaxBatchSize, CustomBatchSplits, AllowOverdrive, Justification.</param>
    /// <response code="200">Phát hành lệnh sản xuất thành công, trả về danh sách các thẻ lô Kanban.</response>
    /// <response code="400">Dữ liệu yêu cầu không hợp lệ hoặc vi phạm quy tắc chia lô / BOM chưa duyệt.</response>
    /// <response code="404">Không tìm thấy dòng sản phẩm đơn hàng.</response>
    [HttpPost("generate-batches")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Leader}")]
    public async Task<ActionResult<ApiResponse<ProductionOrderSummaryDto>>> GenerateBatches([FromBody] GenerateBatchesRequestDto request)
    {
        // 1. Kiểm tra tính hợp lệ dữ liệu đầu vào thông qua FluentValidation
        var validation = await _generateBatchesValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<ProductionOrderSummaryDto>.Fail(
                "Dữ liệu yêu cầu phát hành lệnh sản xuất không hợp lệ.",
                validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        // 2. Lấy thông tin người dùng hiện tại từ Claims Principal
        long currentUserId = GetCurrentUserId();

        // 3. Thực thi nghiệp vụ tách lô và phát hành lệnh sản xuất
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
        return Ok(ApiResponse<ProductionBatchDto>.Ok(result, $"Chuyển bước thành công! Lô sản xuất {result.BatchCode} hiện đang ở công đoạn {result.CurrentStageSeq} ({result.CurrentStageName})."));
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
    /// [BE-Iter 3] SC-20b: Nghiệm thu lại (Re-inspection) cho thẻ nhánh phụ 3% Rework / Scrap Replenish sau khi thợ mộc xử lý xong lỗi phôi hoặc bù phôi.
    /// <para>
    /// - Móc nối: Gọi <see cref="IProductionOrderService.ReinspectSubBatchAsync"/> -&gt; Tạo bản ghi <c>QcInspection</c> mới cho bước công đoạn của thẻ phụ (<c>REWORK_SUB_CARD</c> / <c>REPLENISH_LOT</c>), cập nhật <c>PassedQty</c>/<c>StepStatus</c> và ghi <c>audit_logs</c> để mở điều kiện Gộp lô (<c>merge</c>) hoặc Chuyển tiếp độc lập (<c>continue-independent</c>).
    /// </para>
    /// </summary>
    [HttpPost("batches/{subBatchId:long}/reinspect")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Qc}")]
    public async Task<ActionResult<ApiResponse<ReinspectSubBatchResultDto>>> ReinspectSubBatch(
        long subBatchId,
        [FromBody] ReinspectSubBatchDto request)
    {
        var validation = await _reinspectSubBatchValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<ReinspectSubBatchResultDto>.Fail(
                "Dữ liệu nghiệm thu lại thẻ nhánh phụ không hợp lệ.",
                validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        long currentUserId = GetCurrentUserId();
        var result = await _productionOrderService.ReinspectSubBatchAsync(subBatchId, request, currentUserId);
        return Ok(ApiResponse<ReinspectSubBatchResultDto>.Ok(result, result.Message));
    }

    /// <summary>
    /// [BE-Iter 3] SC-20b: Tái hòa nhập / Gộp lô (Merge Lot) thẻ nhánh phụ đã sửa chữa đạt chuẩn vào thẻ chính (<c>ParentBatchId</c>) khi cùng công đoạn.
    /// <para>
    /// - Móc nối: Gọi <see cref="IProductionOrderService.MergeSubBatchAsync"/> -&gt; Cộng sản lượng đạt chuẩn của thẻ phụ vào <c>BatchQty</c> và <c>TargetQty</c>/<c>PassedQty</c> của thẻ chính (<c>MAIN_CARD</c>), đóng thẻ phụ sang <c>DONE</c> và ghi vết <c>SUB_BATCH_MERGED</c> vào <c>audit_logs</c>.
    /// </para>
    /// </summary>
    [HttpPost("batches/{subBatchId:long}/merge")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Leader}")]
    public async Task<ActionResult<ApiResponse<MergeSubBatchResultDto>>> MergeSubBatch(
        long subBatchId,
        [FromBody] MergeSubBatchDto request)
    {
        var validation = await _mergeSubBatchValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<MergeSubBatchResultDto>.Fail(
                "Dữ liệu yêu cầu gộp lô không hợp lệ.",
                validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        long currentUserId = GetCurrentUserId();
        var result = await _productionOrderService.MergeSubBatchAsync(subBatchId, request, currentUserId);
        return Ok(ApiResponse<MergeSubBatchResultDto>.Ok(result, result.Message));
    }

    /// <summary>
    /// [BE-Iter 3] SC-20b: Cho phép thẻ nhánh phụ chuyển tiếp độc lập sang công đoạn tiếp theo mà không gộp vào lô chính (khi lô chính đã chạy vượt lên công đoạn sau).
    /// <para>
    /// - Móc nối: Gọi <see cref="IProductionOrderService.ContinueSubBatchIndependentlyAsync"/> -&gt; Tăng <c>CurrentStageSeq</c> của thẻ phụ sang bước kế tiếp (hoặc chuyển <c>DONE</c> nếu ở bước cuối) và ghi vết <c>SUB_BATCH_CONTINUED</c> vào <c>audit_logs</c>.
    /// </para>
    /// </summary>
    [HttpPost("batches/{subBatchId:long}/continue-independent")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Leader}")]
    public async Task<ActionResult<ApiResponse<ContinueSubBatchResultDto>>> ContinueSubBatchIndependently(
        long subBatchId,
        [FromBody] ContinueSubBatchIndependentlyDto request)
    {
        var validation = await _continueSubBatchValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<ContinueSubBatchResultDto>.Fail(
                "Dữ liệu yêu cầu chuyển tiếp độc lập không hợp lệ.",
                validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        long currentUserId = GetCurrentUserId();
        var result = await _productionOrderService.ContinueSubBatchIndependentlyAsync(subBatchId, request, currentUserId);
        return Ok(ApiResponse<ContinueSubBatchResultDto>.Ok(result, result.Message));
    }

    /// <summary>
    /// Lấy danh sách thẻ lô Kanban theo dòng sản phẩm đơn hàng (OrderItemId).
    /// </summary>
    [HttpGet("order-item/{orderItemId:long}")]
    public async Task<ActionResult<ApiResponse<List<ProductionBatchDto>>>> GetBatchesByOrderItem(long orderItemId)
    {
        var batches = await _productionOrderService.GetBatchesByOrderItemIdAsync(orderItemId);
        return Ok(ApiResponse<List<ProductionBatchDto>>.Ok(batches, $"Đã tải danh sách {batches.Count} lô sản xuất của sản phẩm thành công."));
    }

    /// <summary>
    /// Lấy danh sách thẻ lô Kanban của toàn bộ đơn hàng (OrderId).
    /// </summary>
    [HttpGet("order/{orderId:long}")]
    public async Task<ActionResult<ApiResponse<List<ProductionBatchDto>>>> GetBatchesByOrder(long orderId)
    {
        var batches = await _productionOrderService.GetBatchesByOrderIdAsync(orderId);
        return Ok(ApiResponse<List<ProductionBatchDto>>.Ok(batches, $"Đã tải danh sách {batches.Count} lô sản xuất của đơn hàng thành công."));
    }

    /// <summary>
    /// Lấy danh sách thẻ lô Kanban theo Không gian đơn hàng (Order Space Key - ví dụ: SPACE-PO-110205).
    /// </summary>
    [HttpGet("space/{spaceKey}")]
    public async Task<ActionResult<ApiResponse<List<ProductionBatchDto>>>> GetBatchesBySpace(string spaceKey)
    {
        var batches = await _productionOrderService.GetBatchesBySpaceKeyAsync(spaceKey);
        return Ok(ApiResponse<List<ProductionBatchDto>>.Ok(batches, $"Đã tải danh sách {batches.Count} lô sản xuất của không gian đơn hàng thành công."));
    }

    /// <summary>
    /// Lấy thông tin chi tiết một lô sản xuất theo BatchId kèm đầy đủ tiến độ các công đoạn (Steps) và cổng QC.
    /// </summary>
    [HttpGet("batches/{batchId:long}")]
    public async Task<ActionResult<ApiResponse<ProductionBatchDto>>> GetBatchById(long batchId)
    {
        var batch = await _productionOrderService.GetBatchByIdAsync(batchId);
        if (batch == null)
        {
            return NotFound(ApiResponse<ProductionBatchDto>.Fail("Không tìm thấy lô sản xuất được yêu cầu."));
        }
        return Ok(ApiResponse<ProductionBatchDto>.Ok(batch, "Đã tải thông tin chi tiết lô sản xuất thành công."));
    }

    /// <summary>
    /// SC-18: Lấy danh mục Trạm gia công tiêu chuẩn của xưởng gỗ (Workstation Catalog).
    /// </summary>
    [HttpGet("workstations")]
    public ActionResult<ApiResponse<List<StandardWorkstationDto>>> GetWorkstations()
    {
        var workstations = _productionOrderService.GetStandardWorkstations();
        return Ok(ApiResponse<List<StandardWorkstationDto>>.Ok(workstations, "Lấy danh mục trạm gia công thành công."));
    }

    /// <summary>
    /// SC-18: Lấy danh mục Cổng kiểm tra chất lượng &amp; Trạm dừng kỹ thuật bắt buộc (Quality Gates &amp; Hold Points Catalog).
    /// </summary>
    [HttpGet("quality-gates")]
    public ActionResult<ApiResponse<List<StandardQualityGateDto>>> GetQualityGates()
    {
        var gates = _productionOrderService.GetStandardQualityGates();
        return Ok(ApiResponse<List<StandardQualityGateDto>>.Ok(gates, "Lấy danh mục cổng kiểm soát chất lượng thành công."));
    }

    /// <summary>
    /// [P0-04, SC-18 &amp; UC-62]: Phê duyệt &amp; Phát hành Lô sản xuất bù (REPLENISH_LOT) cho số lượng sản phẩm phế phẩm đã hủy.
    /// Lô sản xuất bù sẽ chạy độc lập đầy đủ từ Trạm 1 (Pha phôi thô) đến Trạm 7 (Đóng gói) để bù đủ 100% sản lượng cho đơn hàng.
    /// </summary>
    /// <param name="request">DTO chứa OrderItemId, ReplenishQty, Reason.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>Thông tin Lô sản xuất bù kèm tiến trình 7 công đoạn trên Bảng Kanban.</returns>
    [HttpPost("batches/issue-replenish-lot")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Owner},{RoleConstants.Leader},{RoleConstants.Planner}")]
    [ProducesResponseType(typeof(ApiResponse<ProductionBatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> IssueReplenishLotAsync(
        [FromBody] IssueReplenishLotRequestDto request,
        CancellationToken cancellationToken)
    {
        var validation = await _issueReplenishLotValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<ProductionBatchDto>.Fail(
                "Dữ liệu yêu cầu phát hành Lô sản xuất bù không hợp lệ.",
                validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        var userId = GetCurrentUserId();
        var result = await _productionOrderService.IssueReplenishLotAsync(request, userId, cancellationToken);
        return Ok(ApiResponse<ProductionBatchDto>.Ok(result, $"Phát hành Lô sản xuất bù {result.BatchCode} ({result.BatchQty} sản phẩm) thành công."));
    }

    private long GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value;
        if (!string.IsNullOrWhiteSpace(claim) && long.TryParse(claim, out var userId))
        {
            return userId;
        }

        // Fallback người dùng mặc định (Admin / Quản đốc ID = 1) khi kiểm thử hoặc chưa đăng nhập
        return 1;
    }
}

