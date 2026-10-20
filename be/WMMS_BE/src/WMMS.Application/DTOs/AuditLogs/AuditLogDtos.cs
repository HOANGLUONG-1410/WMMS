namespace WMMS.Application.DTOs.AuditLogs;

/// <summary>
/// DTO Bộ lọc truy vấn nhật ký kiểm toán hệ thống (UC-13: Query Audit Logs).
/// </summary>
public class AuditLogFilterDto
{
    public long? UserId { get; set; }
    public string? ActionType { get; set; }
    public string? EntityName { get; set; }
    public string? SearchKeyword { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// DTO Chi tiết bản ghi nhật ký kiểm toán hệ thống (UC-13: Query Audit Logs, UC-14: View Audit Log Details).
/// </summary>
public class AuditLogDto
{
    public long LogId { get; set; }
    public long? UserId { get; set; }
    public string? Username { get; set; }
    public string? UserFullName { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? BeforeState { get; set; }
    public string? AfterState { get; set; }
    public string? Reason { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}
