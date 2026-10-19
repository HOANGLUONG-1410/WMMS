namespace WMMS.Domain.Entities.Identity;

public class AuditLog
{
    public long LogId { get; set; }
    public long? UserId { get; set; }
    public User? User { get; set; }

    public string ActionType { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? BeforeState { get; set; }
    public string? AfterState { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
