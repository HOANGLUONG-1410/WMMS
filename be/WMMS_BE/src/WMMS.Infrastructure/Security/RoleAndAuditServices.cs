using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.AuditLogs;
using WMMS.Application.DTOs.Roles;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Entities.System;
using WMMS.Domain.Exceptions;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Security;

/// <summary>
/// Triển khai Dịch vụ Quản lý Vai trò &amp; Ma trận Phân quyền (UC-11: View Role List, UC-12: Configure Role Permissions).
/// Bảo đảm quy tắc BR-ADM-03: Không cho phép vô hiệu hóa quyền quản trị cốt lõi của vai trò ADMIN.
/// </summary>
public class RoleService : IRoleService
{
    private readonly WmmsDbContext _context;

    public RoleService(WmmsDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoleDto>> GetAllRolesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .AsNoTracking()
            .OrderBy(r => r.RoleId)
            .Select(r => new RoleDto
            {
                RoleId = r.RoleId,
                RoleCode = r.RoleCode,
                RoleName = r.RoleName,
                Description = r.Description,
                PermissionsJson = r.PermissionsJson,
                AssignedUsersCount = r.UserRoles.Count
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RoleDto> UpdateRolePermissionsAsync(
        int roleId,
        UpdateRolePermissionsDto dto,
        long adminUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles
            .Include(r => r.UserRoles)
            .FirstOrDefaultAsync(r => r.RoleId == roleId, cancellationToken);

        if (role is null)
        {
            throw AppException.NotFound($"Không tìm thấy vai trò với mã ID = {roleId}.");
        }

        // Kiểm tra chuỗi JSON phân quyền có đúng cú pháp JSON Object hay không
        JsonDocument parsedJson;
        try
        {
            parsedJson = JsonDocument.Parse(dto.PermissionsJson);
            if (parsedJson.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new AppException("Dữ liệu cấu hình phân quyền phải là một đối tượng JSON hợp lệ.", 400, MessageCodes.Msg13ValidationError);
            }
        }
        catch (JsonException)
        {
            throw new AppException("Dữ liệu cấu hình phân quyền không đúng định dạng JSON.", 400, MessageCodes.Msg13ValidationError);
        }

        // BR-ADM-03: Bảo vệ quyền cốt lõi của vai trò ADMIN (không cho phép đặt "all": false hoặc xóa quyền quản trị)
        if (role.RoleCode.Equals(RoleConstants.Admin, StringComparison.OrdinalIgnoreCase))
        {
            if (parsedJson.RootElement.TryGetProperty("all", out var allProp) && allProp.ValueKind == JsonValueKind.False)
            {
                throw AppException.Forbidden(
                    "Không được phép thu hồi các quyền quản trị cốt lõi của vai trò Quản trị viên (ADMIN).",
                    MessageCodes.Msg11ForbiddenRule);
            }
        }

        var beforeSnapshot = JsonSerializer.Serialize(new
        {
            role.RoleCode,
            role.RoleName,
            role.Description,
            role.PermissionsJson
        });

        if (!string.IsNullOrWhiteSpace(dto.RoleName))
        {
            role.RoleName = dto.RoleName.Trim();
        }

        if (dto.Description is not null)
        {
            role.Description = dto.Description.Trim();
        }

        role.PermissionsJson = dto.PermissionsJson.Trim();

        var afterSnapshot = JsonSerializer.Serialize(new
        {
            role.RoleCode,
            role.RoleName,
            role.Description,
            role.PermissionsJson
        });

        await _context.AuditLogs.AddAsync(new AuditLog
        {
            UserId = adminUserId,
            ActionType = AuditActionConstants.RolePermissionsUpdated,
            EntityName = nameof(Role),
            EntityId = role.RoleId.ToString(),
            BeforeState = beforeSnapshot,
            AfterState = afterSnapshot,
            Reason = string.IsNullOrWhiteSpace(dto.Reason) ? $"Cập nhật ma trận phân quyền cho vai trò {role.RoleCode}." : dto.Reason.Trim(),
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new RoleDto
        {
            RoleId = role.RoleId,
            RoleCode = role.RoleCode,
            RoleName = role.RoleName,
            Description = role.Description,
            PermissionsJson = role.PermissionsJson,
            AssignedUsersCount = role.UserRoles.Count
        };
    }
}

/// <summary>
/// Triển khai Dịch vụ Truy vấn Nhật ký Kiểm toán Hệ thống bất biến &amp; Cấu hình Tham số Hệ thống (UC-13, UC-14, UC-15, GBR-05, SCR-ADM-02).
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly WmmsDbContext _context;

    public AuditLogService(WmmsDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(
        AuditLogFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var query = BuildAuditLogQuery(filter);

        var totalCount = await query.CountAsync(cancellationToken);

        var rawItems = await query
            .OrderByDescending(al => al.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(al => new AuditLogDto
            {
                LogId = al.LogId,
                UserId = al.UserId,
                Username = al.User != null ? al.User.Username : null,
                UserFullName = al.User != null ? al.User.FullName : null,
                PrimaryRole = al.User != null
                    ? al.User.UserRoles.OrderBy(ur => ur.RoleId).Select(ur => ur.Role.RoleCode).FirstOrDefault()
                    : null,
                ActionType = al.ActionType,
                EntityName = al.EntityName,
                EntityId = al.EntityId,
                BeforeState = al.BeforeState,
                AfterState = al.AfterState,
                Reason = al.Reason,
                IpAddress = al.IpAddress,
                CreatedAt = al.CreatedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var item in rawItems)
        {
            item.BeforeState = FormatAuditStateForDisplay(item.BeforeState);
            item.AfterState = FormatAuditStateForDisplay(item.AfterState);
            item.Reason = FormatAuditReasonForDisplay(item.Reason);
        }

        return new PagedResult<AuditLogDto>
        {
            Items = rawItems,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    /// <inheritdoc />
    public async Task<AuditLogDto> GetAuditLogByIdAsync(long logId, CancellationToken cancellationToken = default)
    {
        var log = await _context.AuditLogs
            .AsNoTracking()
            .Include(al => al.User)
                .ThenInclude(u => u!.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(al => al.LogId == logId, cancellationToken);

        if (log is null)
        {
            throw AppException.NotFound($"Không tìm thấy bản ghi nhật ký hoạt động #{logId}.");
        }

        return new AuditLogDto
        {
            LogId = log.LogId,
            UserId = log.UserId,
            Username = log.User?.Username,
            UserFullName = log.User?.FullName,
            PrimaryRole = log.User?.UserRoles.OrderBy(ur => ur.RoleId).Select(ur => ur.Role.RoleCode).FirstOrDefault(),
            ActionType = log.ActionType,
            EntityName = log.EntityName,
            EntityId = log.EntityId,
            BeforeState = FormatAuditStateForDisplay(log.BeforeState),
            AfterState = FormatAuditStateForDisplay(log.AfterState),
            Reason = FormatAuditReasonForDisplay(log.Reason),
            IpAddress = log.IpAddress,
            CreatedAt = log.CreatedAt
        };
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportAuditLogsCsvAsync(AuditLogFilterDto filter, CancellationToken cancellationToken = default)
    {
        var query = BuildAuditLogQuery(filter);

        var logs = await query
            .OrderByDescending(al => al.CreatedAt)
            .Take(2000)
            .Select(al => new AuditLogDto
            {
                LogId = al.LogId,
                UserId = al.UserId,
                Username = al.User != null ? al.User.Username : "SYSTEM",
                UserFullName = al.User != null ? al.User.FullName : "Hệ thống tự động",
                PrimaryRole = al.User != null
                    ? al.User.UserRoles.OrderBy(ur => ur.RoleId).Select(ur => ur.Role.RoleCode).FirstOrDefault() ?? "USER"
                    : "SYSTEM",
                ActionType = al.ActionType,
                EntityName = al.EntityName,
                EntityId = al.EntityId,
                BeforeState = al.BeforeState,
                AfterState = al.AfterState,
                Reason = al.Reason,
                IpAddress = al.IpAddress,
                CreatedAt = al.CreatedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var l in logs)
        {
            l.BeforeState = FormatAuditStateForDisplay(l.BeforeState);
            l.AfterState = FormatAuditStateForDisplay(l.AfterState);
            l.Reason = FormatAuditReasonForDisplay(l.Reason);
        }

        var sb = new StringBuilder();
        sb.AppendLine("LogId,TimestampUtc,ActorFullName,Username,Role,ActionType,TargetEntity,EntityId,BeforeState,AfterState,MandatoryJustification,IpAddress");

        foreach (var l in logs)
        {
            sb.Append(l.LogId).Append(',')
              .Append(EscapeCsv(l.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))).Append(',')
              .Append(EscapeCsv(l.UserFullName)).Append(',')
              .Append(EscapeCsv(l.Username)).Append(',')
              .Append(EscapeCsv(l.PrimaryRole)).Append(',')
              .Append(EscapeCsv(l.ActionType)).Append(',')
              .Append(EscapeCsv(l.EntityName)).Append(',')
              .Append(EscapeCsv(l.EntityId)).Append(',')
              .Append(EscapeCsv(l.BeforeState)).Append(',')
              .Append(EscapeCsv(l.AfterState)).Append(',')
              .Append(EscapeCsv(l.Reason)).Append(',')
              .AppendLine(EscapeCsv(l.IpAddress));
        }

        // UTF-8 with BOM để mở tiếng Việt chuẩn trên Microsoft Excel
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SystemConfigDto>> GetSystemConfigsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SystemConfigs
            .AsNoTracking()
            .OrderBy(c => c.ConfigKey)
            .Select(c => new SystemConfigDto
            {
                ConfigKey = c.ConfigKey,
                ConfigValue = c.ConfigValue,
                DataType = c.DataType ?? "STRING",
                Description = c.Description,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SystemConfigDto> UpdateSystemConfigAsync(
        string configKey,
        UpdateSystemConfigDto dto,
        long actorUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configKey) || string.IsNullOrWhiteSpace(dto.ConfigValue))
        {
            throw new AppException("Vui lòng nhập đầy đủ mã cấu hình và giá trị cấu hình.", 400, MessageCodes.Msg01RequiredField);
        }

        var normalizedKey = configKey.Trim().ToUpperInvariant();
        var config = await _context.SystemConfigs.FirstOrDefaultAsync(c => c.ConfigKey == normalizedKey, cancellationToken);
        if (config is null)
        {
            throw AppException.NotFound($"Không tìm thấy tham số cấu hình hệ thống '{normalizedKey}'.");
        }

        // Kiểm tra ngưỡng hợp lệ cho tỷ lệ hao hụt gỗ (1% - 30%) theo UC-15.E1 & BR-002
        if (normalizedKey is SystemConfigKeys.DefaultScrapMin or SystemConfigKeys.DefaultScrapMax)
        {
            if (!decimal.TryParse(dto.ConfigValue.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var scrapVal)
                || scrapVal < 1m || scrapVal > 30m)
            {
                throw new AppException("Ngưỡng tỷ lệ hao hụt gỗ cấu hình phải nằm trong khoảng từ 1% đến 30%.", 400, MessageCodes.Msg13ValidationError);
            }
        }
        else if (normalizedKey == SystemConfigKeys.EtdWarningDays)
        {
            if (!int.TryParse(dto.ConfigValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var days)
                || days < 1 || days > 90)
            {
                throw new AppException("Số ngày cảnh báo hạn giao hàng (ETD) phải là số nguyên dương từ 1 đến 90 ngày.", 400, MessageCodes.Msg13ValidationError);
            }
        }

        var beforeSnapshot = JsonSerializer.Serialize(new { config.ConfigKey, config.ConfigValue, config.Description });

        config.ConfigValue = dto.ConfigValue.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Description))
        {
            config.Description = dto.Description.Trim();
        }
        config.UpdatedAt = DateTime.UtcNow;

        var afterSnapshot = JsonSerializer.Serialize(new { config.ConfigKey, config.ConfigValue, config.Description });

        await _context.AuditLogs.AddAsync(new AuditLog
        {
            UserId = actorUserId,
            ActionType = AuditActionConstants.SystemConfigUpdated,
            EntityName = nameof(SystemConfig),
            EntityId = config.ConfigKey,
            BeforeState = beforeSnapshot,
            AfterState = afterSnapshot,
            Reason = string.IsNullOrWhiteSpace(dto.Reason)
                ? $"Cập nhật tham số cấu hình hệ thống {config.ConfigKey} thành {config.ConfigValue}."
                : dto.Reason.Trim(),
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new SystemConfigDto
        {
            ConfigKey = config.ConfigKey,
            ConfigValue = config.ConfigValue,
            DataType = config.DataType ?? "STRING",
            Description = config.Description,
            UpdatedAt = config.UpdatedAt
        };
    }

    private IQueryable<AuditLog> BuildAuditLogQuery(AuditLogFilterDto filter)
    {
        var query = _context.AuditLogs
            .AsNoTracking()
            .Include(al => al.User)
                .ThenInclude(u => u!.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .AsQueryable();

        if (filter.UserId.HasValue)
        {
            query = query.Where(al => al.UserId == filter.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.ActionType))
        {
            var action = filter.ActionType.Trim().ToUpperInvariant();
            query = query.Where(al => al.ActionType.ToUpper().Contains(action));
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityName))
        {
            var entity = filter.EntityName.Trim().ToLower();
            query = query.Where(al => al.EntityName.ToLower() == entity);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchKeyword))
        {
            var kw = filter.SearchKeyword.Trim().ToLower();
            query = query.Where(al =>
                al.ActionType.ToLower().Contains(kw) ||
                al.EntityName.ToLower().Contains(kw) ||
                al.EntityId.ToLower().Contains(kw) ||
                (al.Reason != null && al.Reason.ToLower().Contains(kw)) ||
                (al.User != null && (al.User.Username.ToLower().Contains(kw) || al.User.FullName.ToLower().Contains(kw))));
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(al => al.CreatedAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(al => al.CreatedAt <= filter.ToDate.Value);
        }

        return query;
    }

    private static string EscapeCsv(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "\"\"";
        }
        return $"\"{input.Replace("\"", "\"\"")}\"";
    }

    /// <summary>
    /// Chuyển đổi chuỗi JSON thô của <c>BeforeState</c> / <c>AfterState</c> thành chuỗi tiếng Việt tự nhiên, dễ đọc trên bảng Nhật ký hoạt động FE.
    /// </summary>
    internal static string? FormatAuditStateForDisplay(string? rawState)
    {
        if (string.IsNullOrWhiteSpace(rawState))
        {
            return null;
        }

        var trimmed = rawState.Trim();

        // Trường hợp dạng "NEWLY_RECEIVED ({...})"
        if (trimmed.StartsWith("NEWLY_RECEIVED", StringComparison.OrdinalIgnoreCase) && trimmed.Contains('{'))
        {
            var jsonIdx = trimmed.IndexOf('{');
            var innerJson = trimmed[jsonIdx..].TrimEnd(')');
            return "Mới tiếp nhận: " + (FormatAuditStateForDisplay(innerJson) ?? innerJson);
        }

        if (trimmed.Equals("STAGING_RAW", StringComparison.OrdinalIgnoreCase))
        {
            return "Dữ liệu xem trước";
        }

        if (!trimmed.StartsWith('{') || !trimmed.EndsWith('}'))
        {
            return TranslateAuditValue(trimmed);
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return trimmed;
            }

            var parts = new List<string>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var label = TranslateAuditKey(prop.Name);
                var valStr = FormatJsonElementValue(prop.Value);
                if (!string.IsNullOrWhiteSpace(valStr))
                {
                    parts.Add($"{label}: {valStr}");
                }
            }

            return parts.Count > 0 ? string.Join(" | ", parts) : trimmed;
        }
        catch
        {
            return trimmed;
        }
    }

    /// <summary>
    /// Chuẩn hóa các lý do nhật ký hoạt động cũ bằng tiếng Anh hoặc dính mã tài liệu nội bộ sang tiếng Việt tự nhiên.
    /// </summary>
    internal static string? FormatAuditReasonForDisplay(string? rawReason)
    {
        if (string.IsNullOrWhiteSpace(rawReason))
        {
            return rawReason;
        }

        var r = rawReason.Trim();
        if (r.Equals("User logged in successfully (UC-01).", StringComparison.OrdinalIgnoreCase) ||
            r.Equals("User logged in successfully.", StringComparison.OrdinalIgnoreCase))
        {
            return "Người dùng đăng nhập hệ thống thành công.";
        }

        if (r.Equals("User logged out and revoked active session token (UC-02).", StringComparison.OrdinalIgnoreCase))
        {
            return "Người dùng đăng xuất và kết thúc phiên làm việc.";
        }

        if (r.Equals("User changed personal password and revoked active sessions (UC-03).", StringComparison.OrdinalIgnoreCase))
        {
            return "Người dùng đổi mật khẩu cá nhân và thu hồi các phiên đăng nhập cũ.";
        }

        if (r.Equals("Admin created new staff account with multi-role assignment (UC-08).", StringComparison.OrdinalIgnoreCase))
        {
            return "Quản trị viên tạo mới tài khoản nhân sự và phân quyền vai trò.";
        }

        if (r.Equals("Updated user profile and role assignments (UC-09).", StringComparison.OrdinalIgnoreCase))
        {
            return "Cập nhật thông tin hồ sơ và phân quyền vai trò của tài khoản.";
        }

        if (r.Equals("Updated multi-role assignments (UC-09, GBR-04).", StringComparison.OrdinalIgnoreCase))
        {
            return "Cập nhật phân quyền đa vai trò cho nhân sự.";
        }

        // Loại bỏ các mã nội bộ như (UC-01), (SC-14), (BR-002), (GBR-05), (KBN01) ở cuối câu
        r = System.Text.RegularExpressions.Regex.Replace(
            r,
            @"\s*\((?:UC|SC|BR|GBR|MSG|QT|KBN|JOB)[A-Za-z0-9\-,\s\.]*\)\.?",
            "").Trim();

        if (!r.EndsWith('.') && !r.EndsWith('!'))
        {
            r += ".";
        }

        return r;
    }

    private static string TranslateAuditKey(string key) =>
        key switch
        {
            "Username" => "Tài khoản",
            "FullName" => "Họ tên",
            "Email" => "Email",
            "Phone" => "SĐT",
            "Roles" => "Vai trò",
            "RememberMe" => "Ghi nhớ đăng nhập",
            "Status" or "OrderStatus" or "BatchStatus" or "PrStatus" or "PoStatus" or "BomStatus" or "OrderItemStatus" => "Trạng thái",
            "FailedLoginCount" => "Số lần nhập sai",
            "LockedUntil" => "Khóa đến",
            "RevokedSessionsCount" => "Số phiên thu hồi",
            "ConfigKey" => "Mã cấu hình",
            "ConfigValue" => "Giá trị",
            "Description" => "Mô tả",
            "PoCode" or "PoNumber" => "Mã đơn hàng",
            "PrCode" => "Mã PR",
            "SpaceKey" => "Mã không gian đơn",
            "Customer" => "Khách hàng",
            "Supplier" => "Nhà cung cấp",
            "ExpectedArrivalDate" => "Ngày hẹn giao (ETA)",
            "EtdDate" => "Hạn xuất hàng (ETD)",
            "TotalAmount" => "Tổng tiền",
            "LinesCount" or "LineItemsCount" or "ItemsCount" => "Số dòng",
            "TotalUnits" or "OrderedQty" => "Tổng SL đặt",
            "CompletedQty" or "TotalCompletedQty" => "SL hoàn thành",
            "BatchQty" or "CurrentQty" => "SL lô",
            "WarehouseLocation" => "Vị trí kho",
            "HandoverNote" => "Ghi chú bàn giao",
            "RejectionReason" => "Lý do từ chối",
            "Terms" => "Điều khoản",
            "Notes" or "Reason" => "Ghi chú",
            _ => key
        };

    private static string FormatJsonElementValue(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.String => TranslateAuditValue(el.GetString() ?? string.Empty),
            JsonValueKind.True => "Có",
            JsonValueKind.False => "Không",
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.Array => string.Join(", ", el.EnumerateArray().Select(FormatJsonElementValue)),
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => el.GetRawText()
        };

    private static string TranslateAuditValue(string val) =>
        val.Trim() switch
        {
            "ACTIVE" => "Đang hoạt động",
            "INACTIVE" => "Ngừng kích hoạt",
            "BANNED" => "Khóa vĩnh viễn",
            "LOCKED" => "Đang khóa",
            "PENDING_APPROVAL" => "Chờ phê duyệt",
            "APPROVED" => "Đã phê duyệt",
            "REJECTED" => "Đã từ chối",
            "PO_ISSUED" => "Đã phát hành PO",
            "ISSUED" => "Đã phát hành",
            "PARTIALLY_RECEIVED" => "Đã nhận một phần",
            "FULFILLED" => "Đã nhận đủ hàng",
            "CANCELLED" => "Đã hủy",
            "CONFIRMED" => "Đã xác nhận",
            "IN_PLANNING" => "Đang lập kế hoạch",
            "IN_PRODUCTION" or "IN_PROD" or "IN_PROGRESS" => "Đang sản xuất",
            "COMPLETED" or "DONE" => "Đã hoàn thành",
            "DRAFT" or "BOM_DRAFT" => "Bản nháp",
            "PENDING_BOM" => "Chờ lập BOM",
            "WAITING_MAT" or "WAITING_FOR_MATERIALS" => "Chờ vật tư",
            "READY_PROD" or "READY_FOR_PRODUCTION" => "Sẵn sàng sản xuất",
            "HOLD_POINT_BLOCKED" or "BLOCKED" => "Đang tạm dừng tại chốt kiểm soát",
            "PASSED" => "Đạt kiểm định",
            _ => val
        };
}
