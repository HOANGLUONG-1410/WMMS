using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.KanbanQc;
using WMMS.Application.Interfaces.External;
using WMMS.Application.Interfaces.Services;
using WMMS.Application.Validators.KanbanQc;
using WMMS.Domain.Constants;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Entities.KanbanQc;
using WMMS.Domain.Enums;
using WMMS.Domain.Exceptions;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Services.KanbanQc;

/// <summary>
/// Thực thi dịch vụ kiểm soát điểm dừng Hold Point và luồng gửi yêu cầu &amp; phê duyệt ghi đè Overdriver (SC-20, KBN02, GBR-05).
/// </summary>
public class HoldPointService : IHoldPointService
{
    private readonly WmmsDbContext _context;
    private readonly INotificationHubService? _notificationHubService;

    public HoldPointService(
        WmmsDbContext context,
        INotificationHubService? notificationHubService = null)
    {
        _context = context;
        _notificationHubService = notificationHubService;
    }

    /// <inheritdoc />
    public async Task<HoldPointStatusDto> GetBatchHoldPointStatusAsync(
        long batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _context.ProductionBatches
            .Include(pb => pb.OrderItem)
                .ThenInclude(oi => oi.CustomerOrder)
            .Include(pb => pb.OrderItem)
                .ThenInclude(oi => oi.Product)
            .Include(pb => pb.BatchRoutingSteps)
            .Include(pb => pb.OverdriveLogs)
                .ThenInclude(ol => ol.Requester)
            .Include(pb => pb.OverdriveLogs)
                .ThenInclude(ol => ol.Approver)
            .FirstOrDefaultAsync(pb => pb.BatchId == batchId, cancellationToken);

        if (batch == null)
        {
            throw new NotFoundException($"Không tìm thấy Thẻ Lô Sản Xuất ID = {batchId}.");
        }

        var sortedSteps = batch.BatchRoutingSteps
            .OrderBy(s => s.StageSequence)
            .ToList();

        var currentStep = sortedSteps.FirstOrDefault(s => s.StageSequence == batch.CurrentStageSeq);
        var nextStep = sortedSteps.FirstOrDefault(s => s.StageSequence == batch.CurrentStageSeq + 1);

        bool canAdvance = true;
        string? blockReason = null;

        if (batch.BatchStatus == BatchStatusEnum.DONE)
        {
            canAdvance = false;
            blockReason = "Lô sản xuất đã hoàn tất toàn bộ các công đoạn trong quy trình.";
        }
        else if (batch.BatchStatus == BatchStatusEnum.HOLD_POINT_BLOCKED)
        {
            canAdvance = false;
            blockReason = $"Thẻ lô đang bị khóa tại trạm Hold Point công đoạn {batch.HoldPointStageSeq ?? batch.CurrentStageSeq}. Cần cấp phát vật tư hoặc phê duyệt Overdriver từ Chủ xưởng.";
        }
        else if (currentStep != null && !currentStep.Is100PercentPassed())
        {
            canAdvance = false;
            blockReason = $"Công đoạn hiện tại '{currentStep.StageName}' chưa đạt 100% nghiệm thu QC ({currentStep.PassedQty}/{currentStep.TargetQty} SP). (KBN01)";
        }
        else if (nextStep != null)
        {
            bool hitsHoldPoint = nextStep.IsHoldPoint || (batch.HoldPointStageSeq.HasValue && nextStep.StageSequence >= batch.HoldPointStageSeq.Value);
            if (hitsHoldPoint && !batch.GoodsIssuedUnlocked)
            {
                canAdvance = false;
                blockReason = $"Công đoạn tiếp theo '{nextStep.StageName}' (Thứ tự {nextStep.StageSequence}) là điểm dừng kiểm tra Hold Point. Chưa hoàn tất xuất kho cấp phát vật tư. (KBN02)";
            }
        }

        return new HoldPointStatusDto
        {
            BatchId = batch.BatchId,
            BatchCode = batch.BatchCode,
            OrderItemId = batch.OrderItemId,
            PoNumber = batch.OrderItem.CustomerOrder.PoNumber,
            ProductSku = batch.OrderItem.Product.ProductSku,
            ProductName = batch.OrderItem.Product.ProductName,
            BatchQty = batch.BatchQty,
            CurrentStageSeq = batch.CurrentStageSeq,
            CurrentStageName = currentStep?.StageName ?? string.Empty,
            BatchStatus = batch.BatchStatus.ToString(),
            BatchType = batch.BatchType.ToString(),
            IsOverdriver = batch.IsOverdriver,
            HoldPointStageSeq = batch.HoldPointStageSeq,
            GoodsIssuedUnlocked = batch.GoodsIssuedUnlocked,
            CanAdvance = canAdvance,
            BlockReason = blockReason,
            Steps = sortedSteps.Select(s => new HoldPointStepDto
            {
                BatchStepId = s.BatchStepId,
                StageSequence = s.StageSequence,
                StageName = s.StageName,
                IsHoldPoint = s.IsHoldPoint,
                TargetQty = s.TargetQty,
                PassedQty = s.PassedQty,
                DefectQty = s.DefectQty,
                StepStatus = s.StepStatus.ToString(),
                CompletedAt = s.CompletedAt
            }).ToList(),
            OverdriveLogs = batch.OverdriveLogs
                .OrderByDescending(ol => ol.ApprovedAt)
                .Select(ol => new OverdriveLogDto
                {
                    OverdriveId = ol.OverdriveId,
                    BatchId = ol.BatchId,
                    BatchCode = batch.BatchCode,
                    RequestedBy = ol.RequestedBy,
                    RequesterName = ol.Requester?.FullName ?? ol.Requester?.Username ?? $"User #{ol.RequestedBy}",
                    ApprovedBy = ol.ApprovedBy,
                    ApproverName = ol.Approver?.FullName ?? ol.Approver?.Username ?? $"User #{ol.ApprovedBy}",
                    Justification = ol.Justification,
                    HoldPointStageSeq = ol.HoldPointStageSeq,
                    ApprovedAt = ol.ApprovedAt
                }).ToList()
        };
    }

    /// <inheritdoc />
    public async Task<HoldPointOverrideResultDto> RequestOverrideAsync(
        RequestHoldPointOverrideDto request,
        long requesterUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var validator = new RequestHoldPointOverrideValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new FluentValidation.ValidationException(validationResult.Errors);
        }

        var batch = await _context.ProductionBatches
            .Include(pb => pb.BatchRoutingSteps)
            .FirstOrDefaultAsync(pb => pb.BatchId == request.BatchId, cancellationToken);

        if (batch == null)
        {
            throw new NotFoundException($"Không tìm thấy Thẻ Lô Sản Xuất ID = {request.BatchId}.");
        }

        var requester = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == requesterUserId, cancellationToken);

        var sortedSteps = batch.BatchRoutingSteps.OrderBy(s => s.StageSequence).ToList();

        // Xác định vị trí chốt Hold Point mục tiêu
        int targetHoldPointSeq;
        if (request.HoldPointStageSeq.HasValue)
        {
            targetHoldPointSeq = request.HoldPointStageSeq.Value;
        }
        else
        {
            var detectedStep = sortedSteps.FirstOrDefault(s => s.IsHoldPoint && s.StageSequence > batch.CurrentStageSeq)
                               ?? sortedSteps.FirstOrDefault(s => s.IsHoldPoint);

            if (detectedStep == null)
            {
                throw new DomainException("Không tìm thấy công đoạn trạm Hold Point nào trong quy trình sản xuất của lô.");
            }
            targetHoldPointSeq = detectedStep.StageSequence;
        }

        var targetStep = sortedSteps.FirstOrDefault(s => s.StageSequence == targetHoldPointSeq);
        var stageName = targetStep?.StageName ?? $"Công đoạn {targetHoldPointSeq}";

        var beforeState = JsonSerializer.Serialize(new
        {
            batch.BatchId,
            batch.BatchCode,
            batch.IsOverdriver,
            batch.HoldPointStageSeq,
            batch.BatchStatus,
            batch.GoodsIssuedUnlocked
        });

        // Cập nhật trạng thái Thẻ Lô: bật Overdriver và gắn trạm Hold Point
        batch.IsOverdriver = true;
        batch.HoldPointStageSeq = targetHoldPointSeq;
        batch.UpdatedAt = DateTime.UtcNow;

        if (batch.BatchStatus == BatchStatusEnum.LOCKED)
        {
            batch.BatchStatus = BatchStatusEnum.IN_PROGRESS;
        }

        var afterState = JsonSerializer.Serialize(new
        {
            batch.BatchId,
            batch.BatchCode,
            batch.IsOverdriver,
            batch.HoldPointStageSeq,
            batch.BatchStatus,
            batch.GoodsIssuedUnlocked
        });

        // Ghi vết Audit Log bất biến (GBR-05)
        await _context.AuditLogs.AddAsync(new AuditLog
        {
            UserId = requesterUserId,
            ActionType = AuditActionConstants.HoldPointOverdriveRequested,
            EntityName = nameof(ProductionBatch),
            EntityId = batch.BatchId.ToString(),
            BeforeState = beforeState,
            AfterState = afterState,
            Reason = request.Justification,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        // Phát thông báo thời gian thực SignalR
        if (_notificationHubService != null)
        {
            try
            {
                await _notificationHubService.NotifyHoldPointOverrideRequestedAsync(
                    batch.BatchCode,
                    targetHoldPointSeq,
                    requester?.FullName ?? requester?.Username ?? $"User #{requesterUserId}",
                    request.Justification,
                    cancellationToken);
            }
            catch
            {
                // SignalR không ảnh hưởng tới kết quả cơ sở dữ liệu
            }
        }

        return new HoldPointOverrideResultDto
        {
            OverdriveId = null,
            BatchId = batch.BatchId,
            BatchCode = batch.BatchCode,
            HoldPointStageSeq = targetHoldPointSeq,
            StageName = stageName,
            IsOverdriver = batch.IsOverdriver,
            BatchStatus = batch.BatchStatus.ToString(),
            GoodsIssuedUnlocked = batch.GoodsIssuedUnlocked,
            RequestedBy = requesterUserId,
            RequesterName = requester?.FullName ?? requester?.Username ?? $"User #{requesterUserId}",
            ApprovedBy = null,
            ApproverName = null,
            Justification = request.Justification,
            ApprovedAt = null,
            ActionTaken = "REQUESTED"
        };
    }

    /// <inheritdoc />
    public async Task<HoldPointOverrideResultDto> ApproveOverrideAsync(
        long batchId,
        ApproveHoldPointOverrideDto request,
        long approverUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var validator = new ApproveHoldPointOverrideValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new FluentValidation.ValidationException(validationResult.Errors);
        }

        var batch = await _context.ProductionBatches
            .Include(pb => pb.BatchRoutingSteps)
            .FirstOrDefaultAsync(pb => pb.BatchId == batchId, cancellationToken);

        if (batch == null)
        {
            throw new NotFoundException($"Không tìm thấy Thẻ Lô Sản Xuất ID = {batchId}.");
        }

        var approver = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == approverUserId, cancellationToken);

        var sortedSteps = batch.BatchRoutingSteps.OrderBy(s => s.StageSequence).ToList();
        var holdPointSeq = batch.HoldPointStageSeq
                           ?? sortedSteps.FirstOrDefault(s => s.IsHoldPoint)?.StageSequence
                           ?? batch.CurrentStageSeq;

        var targetStep = sortedSteps.FirstOrDefault(s => s.StageSequence == holdPointSeq);
        var stageName = targetStep?.StageName ?? $"Công đoạn {holdPointSeq}";

        var lastRequestAudit = await _context.AuditLogs
            .Where(a => a.ActionType == AuditActionConstants.HoldPointOverdriveRequested && a.EntityId == batch.BatchId.ToString())
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        long requesterUserId = lastRequestAudit?.UserId ?? approverUserId;
        var requester = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == requesterUserId, cancellationToken);

        var beforeState = JsonSerializer.Serialize(new
        {
            batch.BatchId,
            batch.BatchCode,
            batch.IsOverdriver,
            batch.HoldPointStageSeq,
            batch.BatchStatus,
            batch.GoodsIssuedUnlocked
        });

        // Phê duyệt ghi đè: Bật cờ Overdriver, mở khóa cấp phát vật tư để thẻ chạy tiếp qua Hold Point
        batch.IsOverdriver = true;
        batch.GoodsIssuedUnlocked = true;
        batch.HoldPointStageSeq = holdPointSeq;
        batch.BatchStatus = BatchStatusEnum.IN_PROGRESS;
        batch.UpdatedAt = DateTime.UtcNow;

        // Nếu công đoạn hiện tại hoặc hold point đang bị BLOCKED thì chuyển sang IN_PROGRESS
        var currentStep = sortedSteps.FirstOrDefault(s => s.StageSequence == batch.CurrentStageSeq);
        if (currentStep != null && currentStep.StepStatus == StepStatusEnum.BLOCKED)
        {
            currentStep.StepStatus = StepStatusEnum.IN_PROGRESS;
        }

        var now = DateTime.UtcNow;
        var overdriveLog = new OverdriveLog
        {
            BatchId = batch.BatchId,
            RequestedBy = requesterUserId,
            ApprovedBy = approverUserId,
            Justification = request.Justification,
            HoldPointStageSeq = holdPointSeq,
            ApprovedAt = now
        };

        await _context.OverdriveLogs.AddAsync(overdriveLog, cancellationToken);

        var afterState = JsonSerializer.Serialize(new
        {
            batch.BatchId,
            batch.BatchCode,
            batch.IsOverdriver,
            batch.HoldPointStageSeq,
            batch.BatchStatus,
            batch.GoodsIssuedUnlocked,
            OverdriveLogId = overdriveLog.OverdriveId
        });

        // Ghi nhật ký kiểm toán bất biến (GBR-05)
        await _context.AuditLogs.AddAsync(new AuditLog
        {
            UserId = approverUserId,
            ActionType = AuditActionConstants.HoldPointOverdriveApproved,
            EntityName = nameof(ProductionBatch),
            EntityId = batch.BatchId.ToString(),
            BeforeState = beforeState,
            AfterState = afterState,
            Reason = request.Justification,
            IpAddress = ipAddress,
            CreatedAt = now
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        // Phát thông báo thời gian thực SignalR
        if (_notificationHubService != null)
        {
            try
            {
                await _notificationHubService.NotifyHoldPointOverrideDecidedAsync(
                    batch.BatchCode,
                    holdPointSeq,
                    true,
                    approver?.FullName ?? approver?.Username ?? $"User #{approverUserId}",
                    request.Justification,
                    cancellationToken);
            }
            catch
            {
                // SignalR không ảnh hưởng cơ sở dữ liệu
            }
        }

        return new HoldPointOverrideResultDto
        {
            OverdriveId = overdriveLog.OverdriveId,
            BatchId = batch.BatchId,
            BatchCode = batch.BatchCode,
            HoldPointStageSeq = holdPointSeq,
            StageName = stageName,
            IsOverdriver = batch.IsOverdriver,
            BatchStatus = batch.BatchStatus.ToString(),
            GoodsIssuedUnlocked = batch.GoodsIssuedUnlocked,
            RequestedBy = requesterUserId,
            RequesterName = requester?.FullName ?? requester?.Username ?? $"User #{requesterUserId}",
            ApprovedBy = approverUserId,
            ApproverName = approver?.FullName ?? approver?.Username ?? $"User #{approverUserId}",
            Justification = request.Justification,
            ApprovedAt = now,
            ActionTaken = "APPROVED"
        };
    }

    /// <inheritdoc />
    public async Task<HoldPointOverrideResultDto> RejectOverrideAsync(
        long batchId,
        RejectHoldPointOverrideDto request,
        long rejecterUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var validator = new RejectHoldPointOverrideValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new FluentValidation.ValidationException(validationResult.Errors);
        }

        var batch = await _context.ProductionBatches
            .Include(pb => pb.BatchRoutingSteps)
            .FirstOrDefaultAsync(pb => pb.BatchId == batchId, cancellationToken);

        if (batch == null)
        {
            throw new NotFoundException($"Không tìm thấy Thẻ Lô Sản Xuất ID = {batchId}.");
        }

        var rejecter = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == rejecterUserId, cancellationToken);

        var sortedSteps = batch.BatchRoutingSteps.OrderBy(s => s.StageSequence).ToList();
        var holdPointSeq = batch.HoldPointStageSeq
                           ?? sortedSteps.FirstOrDefault(s => s.IsHoldPoint)?.StageSequence
                           ?? batch.CurrentStageSeq;

        var targetStep = sortedSteps.FirstOrDefault(s => s.StageSequence == holdPointSeq);
        var stageName = targetStep?.StageName ?? $"Công đoạn {holdPointSeq}";

        var lastRequestAudit = await _context.AuditLogs
            .Where(a => a.ActionType == AuditActionConstants.HoldPointOverdriveRequested && a.EntityId == batch.BatchId.ToString())
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        long requesterUserId = lastRequestAudit?.UserId ?? rejecterUserId;
        var requester = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == requesterUserId, cancellationToken);

        var beforeState = JsonSerializer.Serialize(new
        {
            batch.BatchId,
            batch.BatchCode,
            batch.IsOverdriver,
            batch.HoldPointStageSeq,
            batch.BatchStatus,
            batch.GoodsIssuedUnlocked
        });

        // Từ chối ghi đè: Khóa cứng thẻ tại trạm Hold Point
        batch.BatchStatus = BatchStatusEnum.HOLD_POINT_BLOCKED;
        batch.GoodsIssuedUnlocked = false;
        batch.UpdatedAt = DateTime.UtcNow;

        var afterState = JsonSerializer.Serialize(new
        {
            batch.BatchId,
            batch.BatchCode,
            batch.IsOverdriver,
            batch.HoldPointStageSeq,
            batch.BatchStatus,
            batch.GoodsIssuedUnlocked
        });

        // Ghi nhật ký kiểm toán bất biến (GBR-05)
        await _context.AuditLogs.AddAsync(new AuditLog
        {
            UserId = rejecterUserId,
            ActionType = AuditActionConstants.HoldPointOverdriveRejected,
            EntityName = nameof(ProductionBatch),
            EntityId = batch.BatchId.ToString(),
            BeforeState = beforeState,
            AfterState = afterState,
            Reason = request.Reason,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        // Phát thông báo thời gian thực SignalR
        if (_notificationHubService != null)
        {
            try
            {
                await _notificationHubService.NotifyHoldPointOverrideDecidedAsync(
                    batch.BatchCode,
                    holdPointSeq,
                    false,
                    rejecter?.FullName ?? rejecter?.Username ?? $"User #{rejecterUserId}",
                    request.Reason,
                    cancellationToken);
            }
            catch
            {
                // SignalR không ảnh hưởng cơ sở dữ liệu
            }
        }

        return new HoldPointOverrideResultDto
        {
            OverdriveId = null,
            BatchId = batch.BatchId,
            BatchCode = batch.BatchCode,
            HoldPointStageSeq = holdPointSeq,
            StageName = stageName,
            IsOverdriver = batch.IsOverdriver,
            BatchStatus = batch.BatchStatus.ToString(),
            GoodsIssuedUnlocked = batch.GoodsIssuedUnlocked,
            RequestedBy = requesterUserId,
            RequesterName = requester?.FullName ?? requester?.Username ?? $"User #{requesterUserId}",
            ApprovedBy = rejecterUserId,
            ApproverName = rejecter?.FullName ?? rejecter?.Username ?? $"User #{rejecterUserId}",
            Justification = request.Reason,
            ApprovedAt = DateTime.UtcNow,
            ActionTaken = "REJECTED"
        };
    }

    /// <inheritdoc />
    public async Task<AdvanceStageResultDto> AdvanceStageAsync(
        long batchId,
        AdvanceStageRequestDto? request,
        long currentUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        if (request != null)
        {
            var validator = new AdvanceStageRequestValidator();
            var valResult = await validator.ValidateAsync(request, cancellationToken);
            if (!valResult.IsValid)
            {
                throw new FluentValidation.ValidationException(valResult.Errors);
            }
        }

        var batch = await _context.ProductionBatches
            .Include(pb => pb.BatchRoutingSteps)
            .FirstOrDefaultAsync(pb => pb.BatchId == batchId, cancellationToken);

        if (batch == null)
        {
            throw new NotFoundException($"Không tìm thấy Thẻ Lô Sản Xuất ID = {batchId}.");
        }

        if (batch.BatchStatus == BatchStatusEnum.DONE)
        {
            throw AppException.Conflict("Thẻ Lô Sản Xuất đã hoàn thành tất cả các công đoạn sản xuất.");
        }

        if (batch.BatchStatus == BatchStatusEnum.HOLD_POINT_BLOCKED)
        {
            throw new HoldPointLockedException(
                $"Thẻ Lô '{batch.BatchCode}' đang bị khóa cứng tại trạm Hold Point. Vui lòng xuất kho vật tư hoặc gửi yêu cầu phê duyệt Overdriver tới Chủ xưởng.",
                batch.HoldPointStageSeq ?? batch.CurrentStageSeq);
        }

        var sortedSteps = batch.BatchRoutingSteps
            .OrderBy(s => s.StageSequence)
            .ToList();

        var currentStep = sortedSteps.FirstOrDefault(s => s.StageSequence == batch.CurrentStageSeq);
        if (currentStep == null)
        {
            throw new DomainException($"Không tìm thấy công đoạn thứ tự {batch.CurrentStageSeq} của Thẻ Lô '{batch.BatchCode}'.");
        }

        // 1. Quy tắc 100% QC Pass (KBN01)
        if (!currentStep.Is100PercentPassed())
        {
            throw new InsufficientQcPassException(
                $"Công đoạn {currentStep.StageSequence} ({currentStep.StageName}) chưa đạt 100% nghiệm thu QC ({currentStep.PassedQty}/{currentStep.TargetQty} SP). Tuyệt đối không được kéo nhảy cóc công đoạn. (KBN01)",
                currentStep.TargetQty,
                currentStep.PassedQty);
        }

        var nextStep = sortedSteps.FirstOrDefault(s => s.StageSequence == batch.CurrentStageSeq + 1);

        // Trường hợp đã hoàn tất công đoạn cuối cùng
        if (nextStep == null)
        {
            currentStep.StepStatus = StepStatusEnum.PASSED;
            currentStep.CompletedAt = DateTime.UtcNow;

            batch.BatchStatus = BatchStatusEnum.DONE;
            batch.UpdatedAt = DateTime.UtcNow;

            await _context.AuditLogs.AddAsync(new AuditLog
            {
                UserId = currentUserId,
                ActionType = AuditActionConstants.KanbanStageAdvanced,
                EntityName = nameof(ProductionBatch),
                EntityId = batch.BatchId.ToString(),
                Reason = request?.Notes ?? $"Hoàn thành công đoạn cuối ({currentStep.StageName}). Thẻ Lô hoàn tất.",
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            return new AdvanceStageResultDto
            {
                BatchId = batch.BatchId,
                BatchCode = batch.BatchCode,
                PreviousStageSeq = currentStep.StageSequence,
                NewStageSeq = currentStep.StageSequence,
                NewStageName = currentStep.StageName,
                BatchStatus = batch.BatchStatus.ToString(),
                IsCompleted = true,
                AdvancedAt = DateTime.UtcNow
            };
        }

        // 2. Chốt chặn điểm dừng Hold Point (KBN02)
        bool hitsHoldPoint = nextStep.IsHoldPoint || (batch.HoldPointStageSeq.HasValue && nextStep.StageSequence >= batch.HoldPointStageSeq.Value);
        if (hitsHoldPoint && !batch.GoodsIssuedUnlocked)
        {
            // Tự động khóa cứng thẻ trước trạm Hold Point
            batch.BatchStatus = BatchStatusEnum.HOLD_POINT_BLOCKED;
            batch.UpdatedAt = DateTime.UtcNow;

            currentStep.StepStatus = StepStatusEnum.BLOCKED;

            await _context.AuditLogs.AddAsync(new AuditLog
            {
                UserId = currentUserId,
                ActionType = AuditActionConstants.HoldPointBlocked,
                EntityName = nameof(ProductionBatch),
                EntityId = batch.BatchId.ToString(),
                Reason = $"Khóa cứng thẻ lô '{batch.BatchCode}' trước trạm Hold Point công đoạn {nextStep.StageSequence} ({nextStep.StageName}) do chưa xuất kho cấp phát vật tư.",
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            throw new HoldPointLockedException(
                $"Thẻ Lô '{batch.BatchCode}' bị chặn tại trạm kiểm soát Hold Point công đoạn {nextStep.StageSequence} ({nextStep.StageName}) do phụ kiện/vật tư chưa được xuất kho cấp phát (GoodsIssuedUnlocked = false). Cần Chủ xưởng phê duyệt Overdriver để tiếp tục.",
                nextStep.StageSequence,
                nextStep.StageName);
        }

        // 3. Đủ điều kiện chuyển tiếp sang công đoạn tiếp theo
        currentStep.StepStatus = StepStatusEnum.PASSED;
        currentStep.CompletedAt = DateTime.UtcNow;

        nextStep.StepStatus = StepStatusEnum.IN_PROGRESS;
        batch.CurrentStageSeq = nextStep.StageSequence;
        batch.BatchStatus = BatchStatusEnum.IN_PROGRESS;
        batch.UpdatedAt = DateTime.UtcNow;

        await _context.AuditLogs.AddAsync(new AuditLog
        {
            UserId = currentUserId,
            ActionType = AuditActionConstants.KanbanStageAdvanced,
            EntityName = nameof(ProductionBatch),
            EntityId = batch.BatchId.ToString(),
            Reason = request?.Notes ?? $"Chuyển công đoạn từ {currentStep.StageSequence} ({currentStep.StageName}) sang {nextStep.StageSequence} ({nextStep.StageName}).",
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new AdvanceStageResultDto
        {
            BatchId = batch.BatchId,
            BatchCode = batch.BatchCode,
            PreviousStageSeq = currentStep.StageSequence,
            NewStageSeq = nextStep.StageSequence,
            NewStageName = nextStep.StageName,
            BatchStatus = batch.BatchStatus.ToString(),
            IsCompleted = false,
            AdvancedAt = DateTime.UtcNow
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<OverdriveLogDto>> GetOverdriveLogsAsync(
        long? batchId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _context.OverdriveLogs
            .Include(ol => ol.ProductionBatch)
            .Include(ol => ol.Requester)
            .Include(ol => ol.Approver)
            .AsNoTracking();

        if (batchId.HasValue && batchId.Value > 0)
        {
            query = query.Where(ol => ol.BatchId == batchId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(ol => ol.ApprovedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ol => new OverdriveLogDto
            {
                OverdriveId = ol.OverdriveId,
                BatchId = ol.BatchId,
                BatchCode = ol.ProductionBatch.BatchCode,
                RequestedBy = ol.RequestedBy,
                RequesterName = ol.Requester.FullName ?? ol.Requester.Username,
                ApprovedBy = ol.ApprovedBy,
                ApproverName = ol.Approver.FullName ?? ol.Approver.Username,
                Justification = ol.Justification,
                HoldPointStageSeq = ol.HoldPointStageSeq,
                ApprovedAt = ol.ApprovedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<OverdriveLogDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        };
    }
}
