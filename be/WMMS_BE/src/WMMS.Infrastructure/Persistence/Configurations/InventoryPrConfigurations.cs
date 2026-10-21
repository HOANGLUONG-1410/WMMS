using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMMS.Domain.Entities.InventoryPr;

namespace WMMS.Infrastructure.Persistence.Configurations;

public class MaterialStockConfiguration : IEntityTypeConfiguration<MaterialStock>
{
    public void Configure(EntityTypeBuilder<MaterialStock> builder)
    {
        builder.ToTable("inventory_stocks");
        builder.HasKey(ms => ms.StockId);
        builder.Property(ms => ms.StockId).HasColumnName("stock_id").ValueGeneratedOnAdd();
        builder.Property(ms => ms.MaterialId).HasColumnName("material_id").IsRequired();
        builder.Property(ms => ms.PhysicalQty).HasColumnName("physical_qty").HasPrecision(18, 4).HasDefaultValue(0);
        builder.Property(ms => ms.AllocatedQty).HasColumnName("allocated_qty").HasPrecision(18, 4).HasDefaultValue(0);
        builder.Property(ms => ms.AvailableQty).HasColumnName("available_qty").HasPrecision(18, 4).HasDefaultValue(0);
        builder.Property(ms => ms.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(ms => ms.RowVersion).HasColumnName("row_version").IsRowVersion();

        builder.HasIndex(ms => ms.MaterialId).IsUnique();
    }
}

public class MaterialAllocationConfiguration : IEntityTypeConfiguration<MaterialAllocation>
{
    public void Configure(EntityTypeBuilder<MaterialAllocation> builder)
    {
        builder.ToTable("material_allocations");
        builder.HasKey(ma => ma.AllocationId);
        builder.Property(ma => ma.AllocationId).HasColumnName("allocation_id").ValueGeneratedOnAdd();
        builder.Property(ma => ma.OrderItemId).HasColumnName("order_item_id").IsRequired();
        builder.Property(ma => ma.MaterialId).HasColumnName("material_id").IsRequired();
        builder.Property(ma => ma.ReservedQty).HasColumnName("reserved_qty").HasPrecision(18, 4).HasDefaultValue(0);
        builder.Property(ma => ma.IssuedQty).HasColumnName("issued_qty").HasPrecision(18, 4).HasDefaultValue(0);
        builder.Property(ma => ma.RequiredQty).HasColumnName("required_qty").HasPrecision(18, 4).HasDefaultValue(0);
        builder.Property(ma => ma.AllocationStatus).HasColumnName("allocation_status").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(ma => ma.AllocatedAt).HasColumnName("allocated_at").IsRequired();

        builder.Ignore(ma => ma.ShortageQty);

        builder.HasOne(ma => ma.OrderItem)
            .WithMany(oi => oi.MaterialAllocations)
            .HasForeignKey(ma => ma.OrderItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ma => ma.Material)
            .WithMany(m => m.MaterialAllocations)
            .HasForeignKey(ma => ma.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseRequestConfiguration : IEntityTypeConfiguration<PurchaseRequest>
{
    public void Configure(EntityTypeBuilder<PurchaseRequest> builder)
    {
        builder.ToTable("consolidated_prs");
        builder.HasKey(pr => pr.PrId);
        builder.Property(pr => pr.PrId).HasColumnName("pr_id").ValueGeneratedOnAdd();
        builder.Property(pr => pr.PrCode).HasColumnName("pr_code").HasMaxLength(50).IsRequired();
        builder.Property(pr => pr.AggregationDate).HasColumnName("aggregation_date").HasColumnType("date").IsRequired();
        builder.Property(pr => pr.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(pr => pr.ApprovedBy).HasColumnName("approved_by");
        builder.Property(pr => pr.PrStatus).HasColumnName("pr_status").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(pr => pr.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(500);
        builder.Property(pr => pr.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(pr => pr.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(pr => pr.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(pr => pr.PrCode).IsUnique();

        builder.HasOne(pr => pr.Creator)
            .WithMany()
            .HasForeignKey(pr => pr.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pr => pr.Approver)
            .WithMany()
            .HasForeignKey(pr => pr.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseRequestItemConfiguration : IEntityTypeConfiguration<PurchaseRequestItem>
{
    public void Configure(EntityTypeBuilder<PurchaseRequestItem> builder)
    {
        builder.ToTable("consolidated_pr_items");
        builder.HasKey(pri => pri.PrItemId);
        builder.Property(pri => pri.PrItemId).HasColumnName("pr_item_id").ValueGeneratedOnAdd();
        builder.Property(pri => pri.PrId).HasColumnName("pr_id").IsRequired();
        builder.Property(pri => pri.MaterialId).HasColumnName("material_id").IsRequired();
        builder.Property(pri => pri.TotalShortageQty).HasColumnName("total_shortage_qty").HasPrecision(18, 4).IsRequired();
        builder.Property(pri => pri.SuggestedOrderQty).HasColumnName("suggested_order_qty").HasPrecision(18, 4).IsRequired();
        builder.Property(pri => pri.SourceOrderIdsJson).HasColumnName("source_order_ids_json");

        builder.HasOne(pri => pri.PurchaseRequest)
            .WithMany(pr => pr.Items)
            .HasForeignKey(pri => pri.PrId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pri => pri.Material)
            .WithMany(m => m.PurchaseRequestItems)
            .HasForeignKey(pri => pri.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
