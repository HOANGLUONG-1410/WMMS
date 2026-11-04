using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using WMMS.Domain.Exceptions;

namespace WMMS.API.Controllers;

/// <summary>Base dùng chung cho các controller Master Data (SC-04, SC-05, SC-06): lấy UserId và IP từ request.</summary>
[ApiController]
[Produces("application/json")]
public abstract class MasterDataControllerBase : ControllerBase
{
    /// <summary>Lấy UserId từ JWT claim (NameIdentifier / sub).</summary>
    protected long GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(sub, out var userId))
        {
            throw AppException.Unauthorized("Invalid user identity claim.");
        }
        return userId;
    }

    /// <summary>Lấy IP client (ưu tiên X-Forwarded-For) để ghi AuditLog (GBR-05).</summary>
    protected string? GetClientIpAddress()
    {
        if (Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            return forwardedFor.FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();
        }
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
