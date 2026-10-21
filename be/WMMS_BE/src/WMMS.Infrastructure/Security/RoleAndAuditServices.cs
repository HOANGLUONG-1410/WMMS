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
            throw AppException.NotFound($"Role with ID {roleId} was not found.");
        }

        // Kiểm tra chuỗi JSON phân quyền có đúng cú pháp JSON Object hay không
        JsonDocument parsedJson;
        try
        {
            parsedJson = JsonDocument.Parse(dto.PermissionsJson);
            if (parsedJson.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new AppException("PermissionsJson must be a valid JSON object.", 400, MessageCodes.Msg13ValidationError);
            }
        }
        catch (JsonException)
        {
            throw new AppException("PermissionsJson is not a valid JSON format.", 400, MessageCodes.Msg13ValidationError);
        }

        // BR-ADM-03: Bảo vệ quyền cốt lõi của vai trò ADMIN (không cho phép đặt "all": false hoặc xóa quyền quản trị)
        if (role.RoleCode.Equals(RoleConstants.Admin, StringComparison.OrdinalIgnoreCase))
        {
            if (parsedJson.RootElement.TryGetProperty("all", out var allProp) && allProp.ValueKind == JsonValueKind.False)
            {
                throw AppException.Forbidden(
                    "Core administrative privileges of the ADMIN role cannot be revoked (BR-ADM-03).",
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
            Reason = string.IsNullOrWhiteSpace(dto.Reason) ? $"Updated permissions matrix for role {role.RoleCode} (UC-12)." : dto.Reason.Trim(),
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

        var items = await query
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

        return new PagedResult<AuditLogDto>
        {
            Items = items,
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
            throw AppException.NotFound($"Audit log entry #{logId} was not found.");
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
            BeforeState = log.BeforeState,
            AfterState = log.AfterState,
            Reason = log.Reason,
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
                UserFullName = al.User != null ? al.User.FullName : "System Process",
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
            throw new AppException("ConfigKey and ConfigValue are required (UC-15).", 400, MessageCodes.Msg01RequiredField);
        }

        var normalizedKey = configKey.Trim().ToUpperInvariant();
        var config = await _context.SystemConfigs.FirstOrDefaultAsync(c => c.ConfigKey == normalizedKey, cancellationToken);
        if (config is null)
        {
            throw AppException.NotFound($"System configuration key '{normalizedKey}' was not found.");
        }

        // Kiểm tra ngưỡng hợp lệ cho tỷ lệ hao hụt gỗ (1% - 30%) theo UC-15.E1 & BR-002
        if (normalizedKey is SystemConfigKeys.DefaultScrapMin or SystemConfigKeys.DefaultScrapMax)
        {
            if (!decimal.TryParse(dto.ConfigValue.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var scrapVal)
                || scrapVal < 1m || scrapVal > 30m)
            {
                throw new AppException("Scrap rate bounds must be between 1% and 30% (UC-15.E1).", 400, MessageCodes.Msg13ValidationError);
            }
        }
        else if (normalizedKey == SystemConfigKeys.EtdWarningDays)
        {
            if (!int.TryParse(dto.ConfigValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var days)
                || days < 1 || days > 90)
            {
                throw new AppException("ETD warning days must be a positive integer between 1 and 90 (UC-15).", 400, MessageCodes.Msg13ValidationError);
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
                ? $"Updated system configuration {config.ConfigKey} to {config.ConfigValue} (UC-15)."
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
}
