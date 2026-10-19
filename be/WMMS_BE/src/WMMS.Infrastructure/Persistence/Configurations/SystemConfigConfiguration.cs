using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMMS.Domain.Entities.System;

namespace WMMS.Infrastructure.Persistence.Configurations;

public class SystemConfigConfiguration : IEntityTypeConfiguration<SystemConfig>
{
    public void Configure(EntityTypeBuilder<SystemConfig> builder)
    {
        builder.ToTable("system_configs");
        builder.HasKey(sc => sc.ConfigId);
        builder.Property(sc => sc.ConfigId).HasColumnName("config_id").ValueGeneratedOnAdd();
        builder.Property(sc => sc.ConfigKey).HasColumnName("config_key").HasMaxLength(100).IsRequired();
        builder.Property(sc => sc.ConfigValue).HasColumnName("config_value").HasMaxLength(1000).IsRequired();
        builder.Property(sc => sc.DataType).HasColumnName("data_type").HasMaxLength(50);
        builder.Property(sc => sc.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(sc => sc.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(sc => sc.ConfigKey).IsUnique();
    }
}
