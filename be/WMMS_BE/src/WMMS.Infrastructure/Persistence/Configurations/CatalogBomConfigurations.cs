using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMMS.Domain.Entities.CatalogBom;

namespace WMMS.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.ProductId);
        builder.Property(p => p.ProductId).HasColumnName("product_id").ValueGeneratedOnAdd();
        builder.Property(p => p.ProductSku).HasColumnName("product_sku").HasMaxLength(100).IsRequired();
        builder.Property(p => p.ProductName).HasColumnName("product_name").HasMaxLength(255).IsRequired();
        builder.Property(p => p.CategoryName).HasColumnName("category_name").HasMaxLength(100).IsRequired();
        builder.Property(p => p.DimensionsCm).HasColumnName("dimensions_cm").HasMaxLength(100).IsRequired();
        builder.Property(p => p.WoodSpecies).HasColumnName("wood_species").HasMaxLength(100);
        builder.Property(p => p.FinishColor).HasColumnName("finish_color").HasMaxLength(100);
        builder.Property(p => p.PackingSpec).HasColumnName("packing_spec").HasMaxLength(150);
        builder.Property(p => p.CbmPerUnit).HasColumnName("cbm_per_unit").HasPrecision(18, 4);
        builder.Property(p => p.ImageUrl).HasColumnName("image_url").HasMaxLength(500);
        builder.Property(p => p.CadDrawingUrl).HasColumnName("cad_drawing_url").HasMaxLength(500);
        builder.Property(p => p.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(p => p.ProductSku).IsUnique();
    }
}

public class RoutingTemplateConfiguration : IEntityTypeConfiguration<RoutingTemplate>
{
    public void Configure(EntityTypeBuilder<RoutingTemplate> builder)
    {
        builder.ToTable("routing_templates");
        builder.HasKey(rt => rt.RoutingId);
        builder.Property(rt => rt.RoutingId).HasColumnName("routing_id").ValueGeneratedOnAdd();
        builder.Property(rt => rt.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(rt => rt.VersionCode).HasColumnName("version_code").HasMaxLength(50).IsRequired();
        builder.Property(rt => rt.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(rt => rt.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(rt => rt.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(rt => rt.Product)
            .WithMany(p => p.RoutingTemplates)
            .HasForeignKey(rt => rt.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RoutingStageConfiguration : IEntityTypeConfiguration<RoutingStage>
{
    public void Configure(EntityTypeBuilder<RoutingStage> builder)
    {
        builder.ToTable("routing_stages");
        builder.HasKey(rs => rs.StageId);
        builder.Property(rs => rs.StageId).HasColumnName("stage_id").ValueGeneratedOnAdd();
        builder.Property(rs => rs.RoutingId).HasColumnName("routing_id").IsRequired();
        builder.Property(rs => rs.StageSequence).HasColumnName("stage_sequence").IsRequired();
        builder.Property(rs => rs.StageName).HasColumnName("stage_name").HasMaxLength(100).IsRequired();
        builder.Property(rs => rs.IsHoldPoint).HasColumnName("is_hold_point").HasDefaultValue(false);

        builder.HasOne(rs => rs.RoutingTemplate)
            .WithMany(rt => rt.RoutingStages)
            .HasForeignKey(rs => rs.RoutingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.ToTable("materials");
        builder.HasKey(m => m.MaterialId);
        builder.Property(m => m.MaterialId).HasColumnName("material_id").ValueGeneratedOnAdd();
        builder.Property(m => m.MaterialCode).HasColumnName("material_code").HasMaxLength(100).IsRequired();
        builder.Property(m => m.MaterialName).HasColumnName("material_name").HasMaxLength(255).IsRequired();
        builder.Property(m => m.MaterialGroup).HasColumnName("material_group").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(m => m.UnitOfMeasure).HasColumnName("unit_of_measure").HasMaxLength(20).IsRequired();
        builder.Property(m => m.DimensionSpec).HasColumnName("dimension_spec").HasMaxLength(150);
        builder.Property(m => m.FscCertified).HasColumnName("fsc_certified").HasDefaultValue(false);
        builder.Property(m => m.SafetyStockQty).HasColumnName("safety_stock_qty").HasPrecision(18, 4).HasDefaultValue(0);
        builder.Property(m => m.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(m => m.MaterialCode).IsUnique();

        builder.HasOne(m => m.MaterialStock)
            .WithOne(ms => ms.Material)
            .HasForeignKey<WMMS.Domain.Entities.InventoryPr.MaterialStock>(ms => ms.MaterialId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class BomHeaderConfiguration : IEntityTypeConfiguration<BomHeader>
{
    public void Configure(EntityTypeBuilder<BomHeader> builder)
    {
        builder.ToTable("boms");
        builder.HasKey(b => b.BomId);
        builder.Property(b => b.BomId).HasColumnName("bom_id").ValueGeneratedOnAdd();
        builder.Property(b => b.OrderItemId).HasColumnName("order_item_id").IsRequired();
        builder.Property(b => b.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(b => b.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(b => b.ScrapRatePercent).HasColumnName("scrap_rate_percent").HasPrecision(5, 2).HasDefaultValue(3.00m);
        builder.Property(b => b.BomStatus).HasColumnName("bom_status").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(b => b.OrderItemId).IsUnique();

        builder.HasOne(b => b.OrderItem)
            .WithOne(oi => oi.BomHeader)
            .HasForeignKey<BomHeader>(b => b.OrderItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Product)
            .WithMany(p => p.BomHeaders)
            .HasForeignKey(b => b.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Creator)
            .WithMany()
            .HasForeignKey(b => b.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BomDetailConfiguration : IEntityTypeConfiguration<BomDetail>
{
    public void Configure(EntityTypeBuilder<BomDetail> builder)
    {
        builder.ToTable("bom_details");
        builder.HasKey(bd => bd.BomDetailId);
        builder.Property(bd => bd.BomDetailId).HasColumnName("bom_detail_id").ValueGeneratedOnAdd();
        builder.Property(bd => bd.BomId).HasColumnName("bom_id").IsRequired();
        builder.Property(bd => bd.MaterialId).HasColumnName("material_id").IsRequired();
        builder.Property(bd => bd.NetQtyPerUnit).HasColumnName("net_qty_per_unit").HasPrecision(18, 4).IsRequired();
        builder.Property(bd => bd.GrossTotalDemand).HasColumnName("gross_total_demand").HasPrecision(18, 4).IsRequired();
        builder.Property(bd => bd.Notes).HasColumnName("notes").HasMaxLength(500);

        builder.HasOne(bd => bd.BomHeader)
            .WithMany(b => b.BomDetails)
            .HasForeignKey(bd => bd.BomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(bd => bd.Material)
            .WithMany(m => m.BomDetails)
            .HasForeignKey(bd => bd.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
