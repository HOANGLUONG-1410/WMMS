using WMMS.Domain.Common;
using WMMS.Domain.Enums;

namespace WMMS.Domain.Entities.Identity;

public class User : BaseEntity, IAuditableEntity
{
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public UserStatusEnum Status { get; set; } = UserStatusEnum.ACTIVE;
    public int FailedLoginCount { get; set; } = 0;
    public DateTime? LockedUntil { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public bool IsLockedOut() => LockedUntil.HasValue && LockedUntil.Value > DateTime.UtcNow;
    public void IncrementFailedCount() => FailedLoginCount++;
    public void ResetFailedCount()
    {
        FailedLoginCount = 0;
        LockedUntil = null;
    }
}
