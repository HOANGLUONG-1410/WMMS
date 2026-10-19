using Microsoft.EntityFrameworkCore;
using WMMS.Domain.Common;
using WMMS.Domain.Entities.CatalogBom;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Entities.InventoryPr;
using WMMS.Domain.Entities.KanbanQc;
using WMMS.Domain.Entities.Orders;
using WMMS.Domain.Entities.System;
using WMMS.Domain.Entities.WarehousePo;

namespace WMMS.Infrastructure.Persistence;

public class WmmsDbContext : DbContext
{
    public WmmsDbContext(DbContextOptions<WmmsDbContext> options) : base(options)
    {
    }

    // Identity & Access Management
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Order Management
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerOrder> CustomerOrders => Set<CustomerOrder>();
    public DbSet<OrderSpace> OrderSpaces => Set<OrderSpace>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    // Product, Routing & BOM
    public DbSet<Product> Products => Set<Product>();
    public DbSet<RoutingTemplate> RoutingTemplates => Set<RoutingTemplate>();
    public DbSet<RoutingStage> RoutingStages => Set<RoutingStage>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<BomHeader> BomHeaders => Set<BomHeader>();
    public DbSet<BomDetail> BomDetails => Set<BomDetail>();

    // Inventory & Allocation & PR
    public DbSet<MaterialStock> MaterialStocks => Set<MaterialStock>();
    public DbSet<MaterialAllocation> MaterialAllocations => Set<MaterialAllocation>();
    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();
    public DbSet<PurchaseRequestItem> PurchaseRequestItems => Set<PurchaseRequestItem>();

    // Warehouse & PO
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderDetail> PurchaseOrderDetails => Set<PurchaseOrderDetail>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    // Kanban & QC
    public DbSet<ProductionBatch> ProductionBatches => Set<ProductionBatch>();
    public DbSet<BatchRoutingStep> BatchRoutingSteps => Set<BatchRoutingStep>();
    public DbSet<QcInspection> QcInspections => Set<QcInspection>();
    public DbSet<DefectCatalog> DefectCatalogs => Set<DefectCatalog>();
    public DbSet<DefectLog> DefectLogs => Set<DefectLog>();
    public DbSet<OverdriveLog> OverdriveLogs => Set<OverdriveLog>();

    // System Configurations
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WmmsDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();
        var now = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default)
                {
                    entry.Entity.CreatedAt = now;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
