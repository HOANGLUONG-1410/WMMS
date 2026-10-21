using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMMS.Domain.Entities.WarehousePo;

namespace WMMS.Infrastructure.Persistence.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers");
        builder.HasKey(s => s.SupplierId);
        builder.Property(s => s.SupplierId).HasColumnName("supplier_id").ValueGeneratedOnAdd();
        builder.Property(s => s.SupplierCode).HasColumnName("supplier_code").HasMaxLength(50).IsRequired();
        builder.Property(s => s.SupplierName).HasColumnName("supplier_name").HasMaxLength(255).IsRequired();
        builder.Property(s => s.SupplyCategory).HasColumnName("supply_category").HasMaxLength(100).IsRequired();
        builder.Property(s => s.ContactEmail).HasColumnName("contact_email").HasMaxLength(100);
        builder.Property(s => s.Phone).HasColumnName("phone").HasMaxLength(20);
        builder.Property(s => s.LeadTimeDays).HasColumnName("lead_time_days").HasDefaultValue(7);
        builder.Property(s => s.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("ACTIVE");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(s => s.SupplierCode).IsUnique();
    }
}

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders");
        builder.HasKey(po => po.PoId);
        builder.Property(po => po.PoId).HasColumnName("po_id").ValueGeneratedOnAdd();
        builder.Property(po => po.PoCode).HasColumnName("po_code").HasMaxLength(50).IsRequired();
        builder.Property(po => po.PrId).HasColumnName("pr_id").IsRequired();
        builder.Property(po => po.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder.Property(po => po.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(po => po.IssueDate).HasColumnName("issue_date").IsRequired();
        builder.Property(po => po.ExpectedArrivalDate).HasColumnName("expected_arrival_date").IsRequired();
        builder.Property(po => po.TotalAmount).HasColumnName("total_amount").HasPrecision(18, 2);
        builder.Property(po => po.PoStatus).HasColumnName("po_status").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(po => po.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(po => po.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(po => po.PoCode).IsUnique();

        builder.HasOne(po => po.PurchaseRequest)
            .WithMany(pr => pr.PurchaseOrders)
            .HasForeignKey(po => po.PrId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(po => po.Supplier)
            .WithMany(s => s.PurchaseOrders)
            .HasForeignKey(po => po.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(po => po.Creator)
            .WithMany()
            .HasForeignKey(po => po.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseOrderDetailConfiguration : IEntityTypeConfiguration<PurchaseOrderDetail>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderDetail> builder)
    {
        builder.ToTable("purchase_order_details");
        builder.HasKey(pod => pod.PoDetailId);
        builder.Property(pod => pod.PoDetailId).HasColumnName("po_detail_id").ValueGeneratedOnAdd();
        builder.Property(pod => pod.PoId).HasColumnName("po_id").IsRequired();
        builder.Property(pod => pod.MaterialId).HasColumnName("material_id").IsRequired();
        builder.Property(pod => pod.OrderedQty).HasColumnName("ordered_qty").HasPrecision(18, 4).IsRequired();
        builder.Property(pod => pod.ReceivedQty).HasColumnName("received_qty").HasPrecision(18, 4).HasDefaultValue(0);
        builder.Property(pod => pod.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2);

        builder.HasOne(pod => pod.PurchaseOrder)
            .WithMany(po => po.Details)
            .HasForeignKey(pod => pod.PoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pod => pod.Material)
            .WithMany(m => m.PurchaseOrderDetails)
            .HasForeignKey(pod => pod.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceipt>
{
    public void Configure(EntityTypeBuilder<GoodsReceipt> builder)
    {
        builder.ToTable("goods_receipt_notes");
        builder.HasKey(gr => gr.GrnId);
        builder.Property(gr => gr.GrnId).HasColumnName("grn_id").ValueGeneratedOnAdd();
        builder.Property(gr => gr.GrnCode).HasColumnName("grn_code").HasMaxLength(50).IsRequired();
        builder.Property(gr => gr.PoId).HasColumnName("po_id").IsRequired();
        builder.Property(gr => gr.WarehouseKeeperId).HasColumnName("warehouse_keeper_id").IsRequired();
        builder.Property(gr => gr.CommittedAt).HasColumnName("committed_at").IsRequired();
        builder.Property(gr => gr.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50).IsRequired();

        builder.HasIndex(gr => gr.GrnCode).IsUnique();

        builder.HasOne(gr => gr.PurchaseOrder)
            .WithMany(po => po.GoodsReceipts)
            .HasForeignKey(gr => gr.PoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(gr => gr.WarehouseKeeper)
            .WithMany()
            .HasForeignKey(gr => gr.WarehouseKeeperId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.ToTable("inventory_transactions");
        builder.HasKey(it => it.TransactionId);
        builder.Property(it => it.TransactionId).HasColumnName("transaction_id").ValueGeneratedOnAdd();
        builder.Property(it => it.MaterialId).HasColumnName("material_id").IsRequired();
        builder.Property(it => it.TxType).HasColumnName("tx_type").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(it => it.Quantity).HasColumnName("quantity").HasPrecision(18, 4).IsRequired();
        builder.Property(it => it.ReferenceCode).HasColumnName("reference_code").HasMaxLength(100);
        builder.Property(it => it.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(it => it.Material)
            .WithMany()
            .HasForeignKey(it => it.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
