using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMMS.Domain.Entities.KanbanQc;

namespace WMMS.Infrastructure.Persistence.Configurations;

public class ProductionBatchConfiguration : IEntityTypeConfiguration<ProductionBatch>
{
    public void Configure(EntityTypeBuilder<ProductionBatch> builder)
    {
        builder.ToTable("production_batches");
        builder.HasKey(pb => pb.BatchId);
        builder.Property(pb => pb.BatchId).HasColumnName("batch_id").ValueGeneratedOnAdd();
        builder.Property(pb => pb.BatchCode).HasColumnName("batch_code").HasMaxLength(50).IsRequired();
        builder.Property(pb => pb.OrderItemId).HasColumnName("order_item_id").IsRequired();
        builder.Property(pb => pb.ParentBatchId).HasColumnName("parent_batch_id");
        builder.Property(pb => pb.BatchType).HasColumnName("batch_type").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(pb => pb.BatchQty).HasColumnName("batch_qty").IsRequired();
        builder.Property(pb => pb.CurrentStageSeq).HasColumnName("current_stage_seq").HasDefaultValue(1);
        builder.Property(pb => pb.IsOverdriver).HasColumnName("is_overdriver").HasDefaultValue(false);
        builder.Property(pb => pb.HoldPointStageSeq).HasColumnName("hold_point_stage_seq");
        builder.Property(pb => pb.GoodsIssuedUnlocked).HasColumnName("goods_issued_unlocked").HasDefaultValue(false);
        builder.Property(pb => pb.BatchStatus).HasColumnName("batch_status").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(pb => pb.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(pb => pb.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(pb => pb.BatchCode).IsUnique();

        builder.HasOne(pb => pb.OrderItem)
            .WithMany(oi => oi.ProductionBatches)
            .HasForeignKey(pb => pb.OrderItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pb => pb.ParentBatch)
            .WithMany(pb => pb.SubBatches)
            .HasForeignKey(pb => pb.ParentBatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BatchRoutingStepConfiguration : IEntityTypeConfiguration<BatchRoutingStep>
{
    public void Configure(EntityTypeBuilder<BatchRoutingStep> builder)
    {
        builder.ToTable("batch_routing_steps");
        builder.HasKey(brs => brs.BatchStepId);
        builder.Property(brs => brs.BatchStepId).HasColumnName("batch_step_id").ValueGeneratedOnAdd();
        builder.Property(brs => brs.BatchId).HasColumnName("batch_id").IsRequired();
        builder.Property(brs => brs.StageSequence).HasColumnName("stage_sequence").IsRequired();
        builder.Property(brs => brs.StageName).HasColumnName("stage_name").HasMaxLength(100).IsRequired();
        builder.Property(brs => brs.IsHoldPoint).HasColumnName("is_hold_point").HasDefaultValue(false);
        builder.Property(brs => brs.TargetQty).HasColumnName("target_qty").IsRequired();
        builder.Property(brs => brs.PassedQty).HasColumnName("passed_qty").HasDefaultValue(0);
        builder.Property(brs => brs.DefectQty).HasColumnName("defect_qty").HasDefaultValue(0);
        builder.Property(brs => brs.StepStatus).HasColumnName("step_status").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(brs => brs.CompletedAt).HasColumnName("completed_at");

        builder.HasOne(brs => brs.ProductionBatch)
            .WithMany(pb => pb.BatchRoutingSteps)
            .HasForeignKey(brs => brs.BatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class QcInspectionConfiguration : IEntityTypeConfiguration<QcInspection>
{
    public void Configure(EntityTypeBuilder<QcInspection> builder)
    {
        builder.ToTable("qc_inspections");
        builder.HasKey(qi => qi.QcId);
        builder.Property(qi => qi.QcId).HasColumnName("qc_id").ValueGeneratedOnAdd();
        builder.Property(qi => qi.BatchStepId).HasColumnName("batch_step_id").IsRequired();
        builder.Property(qi => qi.InspectorId).HasColumnName("inspector_id").IsRequired();
        builder.Property(qi => qi.InspectedQty).HasColumnName("inspected_qty").IsRequired();
        builder.Property(qi => qi.PassQty).HasColumnName("pass_qty").IsRequired();
        builder.Property(qi => qi.FailQty).HasColumnName("fail_qty").HasDefaultValue(0);
        builder.Property(qi => qi.ResultStatus).HasColumnName("result_status").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(qi => qi.InspectedAt).HasColumnName("inspected_at").IsRequired();

        builder.HasOne(qi => qi.BatchRoutingStep)
            .WithMany(brs => brs.QcInspections)
            .HasForeignKey(qi => qi.BatchStepId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(qi => qi.Inspector)
            .WithMany()
            .HasForeignKey(qi => qi.InspectorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DefectCatalogConfiguration : IEntityTypeConfiguration<DefectCatalog>
{
    public void Configure(EntityTypeBuilder<DefectCatalog> builder)
    {
        builder.ToTable("defect_catalogs");
        builder.HasKey(dc => dc.DefectCode);
        builder.Property(dc => dc.DefectCode).HasColumnName("defect_code").HasMaxLength(50).ValueGeneratedNever();
        builder.Property(dc => dc.DefectName).HasColumnName("defect_name").HasMaxLength(255).IsRequired();
        builder.Property(dc => dc.StandardStage).HasColumnName("standard_stage").HasMaxLength(100);
        builder.Property(dc => dc.RootCause).HasColumnName("root_cause").HasMaxLength(500);
        builder.Property(dc => dc.RecommendedAction).HasColumnName("recommended_action").HasMaxLength(500);
    }
}

public class DefectLogConfiguration : IEntityTypeConfiguration<DefectLog>
{
    public void Configure(EntityTypeBuilder<DefectLog> builder)
    {
        builder.ToTable("defect_logs");
        builder.HasKey(dl => dl.DefectLogId);
        builder.Property(dl => dl.DefectLogId).HasColumnName("defect_log_id").ValueGeneratedOnAdd();
        builder.Property(dl => dl.QcId).HasColumnName("qc_id").IsRequired();
        builder.Property(dl => dl.DefectCode).HasColumnName("defect_code").HasMaxLength(50).IsRequired();
        builder.Property(dl => dl.DefectName).HasColumnName("defect_name").HasMaxLength(255).IsRequired();
        builder.Property(dl => dl.DefectQty).HasColumnName("defect_qty").IsRequired();
        builder.Property(dl => dl.Disposition).HasColumnName("disposition").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(dl => dl.SpawnedSubBatchId).HasColumnName("spawned_sub_batch_id");

        builder.HasOne(dl => dl.QcInspection)
            .WithMany(qi => qi.DefectLogs)
            .HasForeignKey(dl => dl.QcId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(dl => dl.DefectCatalog)
            .WithMany(dc => dc.DefectLogs)
            .HasForeignKey(dl => dl.DefectCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(dl => dl.SpawnedSubBatch)
            .WithMany()
            .HasForeignKey(dl => dl.SpawnedSubBatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OverdriveLogConfiguration : IEntityTypeConfiguration<OverdriveLog>
{
    public void Configure(EntityTypeBuilder<OverdriveLog> builder)
    {
        builder.ToTable("overdrive_logs");
        builder.HasKey(ol => ol.OverdriveId);
        builder.Property(ol => ol.OverdriveId).HasColumnName("overdrive_id").ValueGeneratedOnAdd();
        builder.Property(ol => ol.BatchId).HasColumnName("batch_id").IsRequired();
        builder.Property(ol => ol.RequestedBy).HasColumnName("requested_by").IsRequired();
        builder.Property(ol => ol.ApprovedBy).HasColumnName("approved_by").IsRequired();
        builder.Property(ol => ol.Justification).HasColumnName("justification").HasMaxLength(500).IsRequired();
        builder.Property(ol => ol.HoldPointStageSeq).HasColumnName("hold_point_stage_seq").IsRequired();
        builder.Property(ol => ol.ApprovedAt).HasColumnName("approved_at").IsRequired();

        builder.HasOne(ol => ol.ProductionBatch)
            .WithMany(pb => pb.OverdriveLogs)
            .HasForeignKey(ol => ol.BatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ol => ol.Requester)
            .WithMany()
            .HasForeignKey(ol => ol.RequestedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ol => ol.Approver)
            .WithMany()
            .HasForeignKey(ol => ol.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
