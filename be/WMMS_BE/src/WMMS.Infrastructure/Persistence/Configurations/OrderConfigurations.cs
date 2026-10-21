using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMMS.Domain.Entities.Orders;

namespace WMMS.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(c => c.CustomerId);
        builder.Property(c => c.CustomerId).HasColumnName("customer_id").ValueGeneratedOnAdd();
        builder.Property(c => c.CustomerCode).HasColumnName("customer_code").HasMaxLength(50).IsRequired();
        builder.Property(c => c.CompanyName).HasColumnName("company_name").HasMaxLength(255).IsRequired();
        builder.Property(c => c.ContactPerson).HasColumnName("contact_person").HasMaxLength(255);
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(255);
        builder.Property(c => c.Country).HasColumnName("country").HasMaxLength(100);
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => c.CustomerCode).IsUnique();
    }
}

public class CustomerOrderConfiguration : IEntityTypeConfiguration<CustomerOrder>
{
    public void Configure(EntityTypeBuilder<CustomerOrder> builder)
    {
        builder.ToTable("sales_orders");
        builder.HasKey(o => o.OrderId);
        builder.Property(o => o.OrderId).HasColumnName("order_id").ValueGeneratedOnAdd();
        builder.Property(o => o.PoNumber).HasColumnName("po_number").HasMaxLength(100).IsRequired();
        builder.Property(o => o.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(o => o.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(o => o.OrderDate).HasColumnName("order_date").HasColumnType("date").IsRequired();
        builder.Property(o => o.EtdDate).HasColumnName("etd_date").HasColumnType("date").IsRequired();
        builder.Property(o => o.TotalCbm).HasColumnName("total_cbm").HasPrecision(18, 4);
        builder.Property(o => o.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(o => o.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(o => o.PoNumber).IsUnique();
        builder.HasIndex(o => new { o.Status, o.EtdDate }).HasDatabaseName("IX_sales_orders_status_etd");

        builder.HasOne(o => o.Customer)
            .WithMany(c => c.SalesOrders)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Creator)
            .WithMany()
            .HasForeignKey(o => o.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.OrderSpace)
            .WithOne(s => s.CustomerOrder)
            .HasForeignKey<OrderSpace>(s => s.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderSpaceConfiguration : IEntityTypeConfiguration<OrderSpace>
{
    public void Configure(EntityTypeBuilder<OrderSpace> builder)
    {
        builder.ToTable("order_spaces");
        builder.HasKey(s => s.SpaceId);
        builder.Property(s => s.SpaceId).HasColumnName("space_id").ValueGeneratedOnAdd();
        builder.Property(s => s.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(s => s.SpaceKey).HasColumnName("space_key").HasMaxLength(50).IsRequired();
        builder.Property(s => s.SpaceName).HasColumnName("space_name").HasMaxLength(255).IsRequired();
        builder.Property(s => s.DrawingFolderUrl).HasColumnName("drawing_folder_url").HasMaxLength(500);
        builder.Property(s => s.OverallProgressPct).HasColumnName("overall_progress_pct").HasPrecision(5, 2).HasDefaultValue(0);
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(s => s.OrderId).IsUnique();
        builder.HasIndex(s => s.SpaceKey).IsUnique();
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");
        builder.HasKey(oi => oi.OrderItemId);
        builder.Property(oi => oi.OrderItemId).HasColumnName("order_item_id").ValueGeneratedOnAdd();
        builder.Property(oi => oi.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(oi => oi.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(oi => oi.OrderedQty).HasColumnName("ordered_qty").IsRequired();
        builder.Property(oi => oi.UnitPriceUsd).HasColumnName("unit_price_usd").HasPrecision(18, 2);
        builder.Property(oi => oi.FscCertifiedReq).HasColumnName("fsc_certified_req").HasDefaultValue(false);
        builder.Property(oi => oi.LengthMm).HasColumnName("length_mm").HasPrecision(18, 2);
        builder.Property(oi => oi.WidthMm).HasColumnName("width_mm").HasPrecision(18, 2);
        builder.Property(oi => oi.HeightMm).HasColumnName("height_mm").HasPrecision(18, 2);
        builder.Property(oi => oi.Status).HasColumnName("item_status").HasConversion<string>().HasMaxLength(50).IsRequired();

        builder.HasOne(oi => oi.CustomerOrder)
            .WithMany(o => o.OrderItems)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(oi => oi.Product)
            .WithMany(p => p.OrderItems)
            .HasForeignKey(oi => oi.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(oi => oi.BomHeader)
            .WithOne(b => b.OrderItem)
            .HasForeignKey<WMMS.Domain.Entities.CatalogBom.BomHeader>(b => b.OrderItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
