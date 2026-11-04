using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.Users;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Constants;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Enums;
using WMMS.Domain.Exceptions;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Security;

/// <summary>
/// Triển khai Dịch vụ Quản trị Tài khoản &amp; Phân quyền Đa vai trò (Phân hệ ADM - UC-06 đến UC-10, Report 4 Sequence Diagram 3.3.1).
/// Tuân thủ nghiêm ngặt các quy tắc:
/// - GBR-04: Multi-Role RBAC (một tài khoản có thể đảm nhận 1..* vai trò).
/// - GBR-05: Ghi nhật ký kiểm toán bất biến (<c>audit_logs</c>) cho mọi thao tác tạo, sửa, đổi vai trò, khóa/mở khóa.
/// - BR-ADM-01: Quản trị viên không được tự khóa/vô hiệu hóa hoặc tự gỡ quyền ADMIN của chính mình.
/// - BR-ADM-02: Bảo đảm hệ thống luôn còn ít nhất 1 tài khoản ADMIN đang ở trạng thái ACTIVE.
/// </summary>
public class UserService : IUserService
{
    private readonly WmmsDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(WmmsDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    /// <summary>
    /// UC-06: Xem danh sách tài khoản có phân trang, tìm kiếm theo từ khóa và lọc theo vai trò / trạng thái.
    /// </summary>
    public async Task<PagedResult<UserDto>> GetUsersAsync(
        UserQueryFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var query = _context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchKeyword))
        {
            var kw = filter.SearchKeyword.Trim().ToLower();
            query = query.Where(u =>
                u.Username.ToLower().Contains(kw) ||
                u.Email.ToLower().Contains(kw) ||
                u.FullName.ToLower().Contains(kw) ||
                (u.Phone != null && u.Phone.Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(filter.RoleCode))
        {
            var roleCode = filter.RoleCode.Trim().ToUpperInvariant();
            query = query.Where(u => u.UserRoles.Any(ur => ur.Role.RoleCode == roleCode));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status) &&
            Enum.TryParse<UserStatusEnum>(filter.Status.Trim(), ignoreCase: true, out var statusEnum))
        {
            query = query.Where(u => u.Status == statusEnum);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<UserDto>
        {
            Items = users.Select(MapToUserDto).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    /// <summary>
    /// UC-07: Xem chi tiết hồ sơ một tài khoản nhân sự và danh sách các vai trò đang đảm nhận.
    /// </summary>
    public async Task<UserDto> GetUserByIdAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            throw AppException.NotFound($"User with ID {userId} was not found.");
        }

        return MapToUserDto(user);
    }

    /// <summary>
    /// UC-08: Tạo mới tài khoản nhân sự và gán đa vai trò (Report 4 Sequence Diagram 3.3.1 <c>CreateStaffAccountAsync</c>).
    /// - Kiểm tra trùng lặp Username hoặc Email (trả về HTTP 409 Conflict - MSG08 nếu trùng).
    /// - Băm mật khẩu khởi tạo bằng BCrypt work factor 12 (BR-SEC-01).
    /// - Thực thi trong DB Transaction nguyên tử: lưu <c>users</c>, lưu <c>user_roles</c>, ghi <c>audit_logs</c>.
    /// </summary>
    public async Task<UserDto> CreateStaffAccountAsync(
        CreateStaffDto dto,
        long adminUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = dto.Username.Trim();
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        // Bước 1 (Sequence Diagram 3.3.1): Kiểm tra trùng lặp Username / Email
        var duplicateExists = await _context.Users.AnyAsync(
            u => u.Username.ToLower() == normalizedUsername.ToLower() || u.Email.ToLower() == normalizedEmail,
            cancellationToken);

        if (duplicateExists)
        {
            throw AppException.Conflict(
                "Username or Email already exists in the system (HTTP 409 Conflict).",
                MessageCodes.Msg08DuplicateConflict);
        }

        // Bước 2: Kiểm tra danh sách mã vai trò hợp lệ
        var requestedRoleCodes = dto.RoleCodes
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (requestedRoleCodes.Count == 0)
        {
            throw new AppException("At least one valid role code must be provided (GBR-04).", 400, MessageCodes.Msg01RequiredField);
        }

        var roles = await _context.Roles
            .Where(r => requestedRoleCodes.Contains(r.RoleCode))
            .ToListAsync(cancellationToken);

        if (roles.Count != requestedRoleCodes.Count)
        {
            var foundCodes = roles.Select(r => r.RoleCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var invalidCodes = requestedRoleCodes.Where(c => !foundCodes.Contains(c));
            throw new AppException(
                $"Invalid role code(s): {string.Join(", ", invalidCodes)}. Allowed roles: {string.Join(", ", RoleConstants.AllRoles)}.",
                400,
                MessageCodes.Msg13ValidationError);
        }

        // Bước 3: Mở Database Transaction nguyên tử theo đúng Sequence Diagram 3.3.1
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var newUser = new User
            {
                Username = normalizedUsername,
                Email = normalizedEmail,
                PasswordHash = _passwordHasher.HashPassword(dto.InitialPassword),
                FullName = dto.FullName.Trim(),
                Phone = dto.Phone?.Trim(),
                Status = UserStatusEnum.ACTIVE,
                FailedLoginCount = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _context.Users.AddAsync(newUser, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var userRoles = roles.Select(role => new UserRole
            {
                UserId = newUser.UserId,
                RoleId = role.RoleId,
                Role = role,
                AssignedAt = now
            }).ToList();

            await _context.UserRoles.AddRangeAsync(userRoles, cancellationToken);

            var auditLog = new AuditLog
            {
                UserId = adminUserId,
                ActionType = AuditActionConstants.UserCreated,
                EntityName = nameof(User),
                EntityId = newUser.UserId.ToString(),
                BeforeState = null,
                AfterState = JsonSerializer.Serialize(new
                {
                    newUser.UserId,
                    newUser.Username,
                    newUser.Email,
                    newUser.FullName,
                    newUser.Phone,
                    Status = newUser.Status.ToString(),
                    Roles = requestedRoleCodes
                }),
                Reason = "Admin created new staff account with multi-role assignment (UC-08).",
                IpAddress = ipAddress,
                CreatedAt = now
            };

            await _context.AuditLogs.AddAsync(auditLog, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            newUser.UserRoles = userRoles;
            return MapToUserDto(newUser);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// UC-09: Cập nhật thông tin tài khoản (FullName, Email, Phone) và tùy chọn đồng bộ lại danh sách vai trò.
    /// </summary>
    public async Task<UserDto> UpdateUserAsync(
        long targetUserId,
        UpdateUserDto dto,
        long adminUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == targetUserId, cancellationToken);

        if (user is null)
        {
            throw AppException.NotFound($"User with ID {targetUserId} was not found.");
        }

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        var emailConflict = await _context.Users.AnyAsync(
            u => u.UserId != targetUserId && u.Email.ToLower() == normalizedEmail,
            cancellationToken);

        if (emailConflict)
        {
            throw AppException.Conflict("Email address is already in use by another account (MSG08).", MessageCodes.Msg08DuplicateConflict);
        }

        var beforeSnapshot = JsonSerializer.Serialize(new
        {
            user.Email,
            user.FullName,
            user.Phone,
            Roles = user.UserRoles.Select(ur => ur.Role.RoleCode).ToList()
        });

        user.Email = normalizedEmail;
        user.FullName = dto.FullName.Trim();
        user.Phone = dto.Phone?.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        if (dto.RoleCodes is not null && dto.RoleCodes.Count > 0)
        {
            await SyncUserRolesInternalAsync(user, dto.RoleCodes, adminUserId, cancellationToken);
        }

        var afterSnapshot = JsonSerializer.Serialize(new
        {
            user.Email,
            user.FullName,
            user.Phone,
            Roles = user.UserRoles.Select(ur => ur.Role.RoleCode).ToList()
        });

        await _context.AuditLogs.AddAsync(new AuditLog
        {
            UserId = adminUserId,
            ActionType = AuditActionConstants.UserUpdated,
            EntityName = nameof(User),
            EntityId = user.UserId.ToString(),
            BeforeState = beforeSnapshot,
            AfterState = afterSnapshot,
            Reason = string.IsNullOrWhiteSpace(dto.Reason) ? "Updated user profile and role assignments (UC-09)." : dto.Reason.Trim(),
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return MapToUserDto(user);
    }

    /// <summary>
    /// UC-09: Cập nhật riêng danh sách đa vai trò của nhân sự (Report 4 Class Diagram <c>UpdateStaffRolesAsync</c>).
    /// Đảm bảo quy tắc BR-ADM-01 (Admin không tự gỡ quyền ADMIN của chính mình) và BR-ADM-02 (luôn còn ít nhất 1 Admin active).
    /// </summary>
    public async Task<UserDto> UpdateStaffRolesAsync(
        long targetUserId,
        UpdateUserRolesDto dto,
        long adminUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == targetUserId, cancellationToken);

        if (user is null)
        {
            throw AppException.NotFound($"User with ID {targetUserId} was not found.");
        }

        var beforeRoles = user.UserRoles.Select(ur => ur.Role.RoleCode).ToList();

        await SyncUserRolesInternalAsync(user, dto.RoleCodes, adminUserId, cancellationToken);

        var afterRoles = user.UserRoles.Select(ur => ur.Role.RoleCode).ToList();

        await _context.AuditLogs.AddAsync(new AuditLog
        {
            UserId = adminUserId,
            ActionType = AuditActionConstants.UserRolesUpdated,
            EntityName = nameof(User),
            EntityId = user.UserId.ToString(),
            BeforeState = JsonSerializer.Serialize(new { Roles = beforeRoles }),
            AfterState = JsonSerializer.Serialize(new { Roles = afterRoles }),
            Reason = string.IsNullOrWhiteSpace(dto.Reason) ? "Updated multi-role assignments (UC-09, GBR-04)." : dto.Reason.Trim(),
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return MapToUserDto(user);
    }

    /// <summary>
    /// UC-10: Khóa / Mở khóa / Vô hiệu hóa tài khoản nhân sự (<c>PATCH /api/v1/users/{id}/status</c>).
    /// - Chặn Admin tự khóa chính mình (BR-ADM-01).
    /// - Chặn khóa tài khoản ADMIN cuối cùng đang hoạt động (BR-ADM-02).
    /// - Khi chuyển sang BANNED hoặc INACTIVE: tự động thu hồi toàn bộ Refresh Token đang hoạt động của tài khoản đó.
    /// - Khi chuyển sang ACTIVE: đặt lại <c>failed_login_count = 0</c> và xóa <c>locked_until</c>.
    /// </summary>
    public async Task<UserDto> UpdateUserStatusAsync(
        long targetUserId,
        UpdateUserStatusDto dto,
        long adminUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<UserStatusEnum>(dto.Status.Trim(), ignoreCase: true, out var newStatus))
        {
            throw new AppException(
                "Invalid user status. Allowed values: ACTIVE, INACTIVE, BANNED, LOCKED.",
                400,
                MessageCodes.Msg13ValidationError);
        }

        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.UserId == targetUserId, cancellationToken);

        if (user is null)
        {
            throw AppException.NotFound($"User with ID {targetUserId} was not found.");
        }

        // BR-ADM-01: Quản trị viên không được tự khóa hoặc vô hiệu hóa tài khoản của chính mình
        if (targetUserId == adminUserId && newStatus != UserStatusEnum.ACTIVE)
        {
            throw AppException.Forbidden(
                "Administrators cannot ban, lock, or deactivate their own account (BR-ADM-01).",
                MessageCodes.Msg11ForbiddenRule);
        }

        // BR-ADM-02: Không được khóa/vô hiệu hóa tài khoản ADMIN hoạt động cuối cùng trong hệ thống
        var isTargetAdmin = user.UserRoles.Any(ur => ur.Role.RoleCode == RoleConstants.Admin);
        if (isTargetAdmin && newStatus != UserStatusEnum.ACTIVE)
        {
            var otherActiveAdminsCount = await _context.Users
                .CountAsync(u =>
                    u.UserId != targetUserId &&
                    u.Status == UserStatusEnum.ACTIVE &&
                    u.UserRoles.Any(ur => ur.Role.RoleCode == RoleConstants.Admin),
                    cancellationToken);

            if (otherActiveAdminsCount == 0)
            {
                throw AppException.Forbidden(
                    "Cannot deactivate or ban the last active System Administrator account (BR-ADM-02).",
                    MessageCodes.Msg11ForbiddenRule);
            }
        }

        var previousStatus = user.Status;
        user.Status = newStatus;
        user.UpdatedAt = DateTime.UtcNow;

        var revokedSessionsCount = 0;
        if (newStatus == UserStatusEnum.ACTIVE)
        {
            user.ResetFailedCount();
        }
        else
        {
            // Thu hồi toàn bộ Refresh Token đang hoạt động khi tài khoản bị BANNED / INACTIVE / LOCKED (UC-10)
            foreach (var activeToken in user.RefreshTokens.Where(rt => !rt.IsRevoked))
            {
                activeToken.Revoke();
                revokedSessionsCount++;
            }
        }

        await _context.AuditLogs.AddAsync(new AuditLog
        {
            UserId = adminUserId,
            ActionType = AuditActionConstants.UserStatusChanged,
            EntityName = nameof(User),
            EntityId = user.UserId.ToString(),
            BeforeState = JsonSerializer.Serialize(new { Status = previousStatus.ToString() }),
            AfterState = JsonSerializer.Serialize(new { Status = newStatus.ToString(), RevokedSessionsCount = revokedSessionsCount }),
            Reason = dto.Reason.Trim(),
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return MapToUserDto(user);
    }

    private async Task SyncUserRolesInternalAsync(
        User user,
        List<string> roleCodes,
        long adminUserId,
        CancellationToken cancellationToken)
    {
        var normalizedCodes = roleCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (normalizedCodes.Count == 0)
        {
            throw new AppException("At least one role must be assigned to the user account (GBR-04).", 400, MessageCodes.Msg01RequiredField);
        }

        // BR-ADM-01: Admin không được tự gỡ quyền ADMIN của chính mình
        if (user.UserId == adminUserId &&
            user.UserRoles.Any(ur => ur.Role.RoleCode == RoleConstants.Admin) &&
            !normalizedCodes.Contains(RoleConstants.Admin))
        {
            throw AppException.Forbidden(
                "Administrators cannot remove the ADMIN role from their own account (BR-ADM-01).",
                MessageCodes.Msg11ForbiddenRule);
        }

        var targetRoles = await _context.Roles
            .Where(r => normalizedCodes.Contains(r.RoleCode))
            .ToListAsync(cancellationToken);

        if (targetRoles.Count != normalizedCodes.Count)
        {
            var validSet = targetRoles.Select(r => r.RoleCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var invalid = normalizedCodes.Where(c => !validSet.Contains(c));
            throw new AppException(
                $"Invalid role code(s): {string.Join(", ", invalid)}.",
                400,
                MessageCodes.Msg13ValidationError);
        }

        _context.UserRoles.RemoveRange(user.UserRoles);

        var now = DateTime.UtcNow;
        user.UserRoles = targetRoles.Select(role => new UserRole
        {
            UserId = user.UserId,
            RoleId = role.RoleId,
            Role = role,
            AssignedAt = now
        }).ToList();
    }

    private static UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            Status = user.Status.ToString(),
            FailedLoginCount = user.FailedLoginCount,
            LockedUntil = user.LockedUntil,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            Roles = user.UserRoles
                .Where(ur => ur.Role is not null)
                .Select(ur => ur.Role.RoleCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList()
        };
    }
}
