using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WMMS.Application.Common.Models;
using WMMS.Application.DTOs.MasterData;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Entities.CatalogBom;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Entities.InventoryPr;
using WMMS.Domain.Entities.Orders;
using WMMS.Domain.Entities.WarehousePo;
using WMMS.Domain.Enums;
using WMMS.Domain.Exceptions;
using WMMS.Infrastructure.Persistence;

namespace WMMS.Infrastructure.Services.MasterData;

/// <summary>Mã hành động AuditLog của phân hệ Master Data (GBR-05).</summary>
internal static class MasterDataAudit
{
    public const string MaterialCreated = "MATERIAL_CREATED";
    public const string MaterialUpdated = "MATERIAL_UPDATED";
    public const string MaterialDeactivated = "MATERIAL_DEACTIVATED";
    public const string ProductCreated = "PRODUCT_CREATED";
    public const string ProductUpdated = "PRODUCT_UPDATED";
    public const string ProductCadUploaded = "PRODUCT_CAD_UPLOADED";
    public const string SupplierCreated = "SUPPLIER_CREATED";
    public const string SupplierUpdated = "SUPPLIER_UPDATED";
    public const string CustomerCreated = "CUSTOMER_CREATED";

    /// <summary>Thêm bản ghi AuditLog vào context (chưa SaveChanges để cùng giao dịch với nghiệp vụ).</summary>
    public static void Add(WmmsDbContext ctx, long userId, string action, string entity, string entityId, object? before, object? after, string? ip)
    {
        ctx.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            ActionType = action,
            EntityName = entity,
            EntityId = entityId,
            BeforeState = before == null ? null : JsonSerializer.Serialize(before),
            AfterState = after == null ? null : JsonSerializer.Serialize(after),
            IpAddress = ip,
            CreatedAt = DateTime.UtcNow
        });
    }

    /// <summary>Chuẩn hóa tham số phân trang (tránh PageSize quá lớn / số âm).</summary>
    public static (int page, int size) Paging(int page, int size)
        => (Math.Max(page, 1), Math.Clamp(size <= 0 ? 10 : size, 1, 200));
}

