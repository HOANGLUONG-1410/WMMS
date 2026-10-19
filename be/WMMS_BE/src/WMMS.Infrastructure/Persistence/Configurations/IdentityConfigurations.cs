using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMMS.Domain.Entities.Identity;

namespace WMMS.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.UserId);
        builder.Property(u => u.UserId).HasColumnName("user_id").ValueGeneratedOnAdd();
        builder.Property(u => u.Username).HasColumnName("username").HasMaxLength(100).IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(500).IsRequired();
        builder.Property(u => u.FullName).HasColumnName("full_name").HasMaxLength(255).IsRequired();
        builder.Property(u => u.Phone).HasColumnName("phone").HasMaxLength(20);
        builder.Property(u => u.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(u => u.FailedLoginCount).HasColumnName("failed_login_count").HasDefaultValue(0);
        builder.Property(u => u.LockedUntil).HasColumnName("locked_until");
        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(u => u.Username).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(r => r.RoleId);
        builder.Property(r => r.RoleId).HasColumnName("role_id").ValueGeneratedOnAdd();
        builder.Property(r => r.RoleCode).HasColumnName("role_code").HasMaxLength(50).IsRequired();
        builder.Property(r => r.RoleName).HasColumnName("role_name").HasMaxLength(100).IsRequired();
        builder.Property(r => r.PermissionsJson).HasColumnName("permissions_json").IsRequired();

        builder.HasIndex(r => r.RoleCode).IsUnique();
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");
        builder.HasKey(ur => new { ur.UserId, ur.RoleId });
        builder.Property(ur => ur.UserId).HasColumnName("user_id");
        builder.Property(ur => ur.RoleId).HasColumnName("role_id");
        builder.Property(ur => ur.AssignedAt).HasColumnName("assigned_at").IsRequired();

        builder.HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(rt => rt.TokenId);
        builder.Property(rt => rt.TokenId).HasColumnName("token_id").ValueGeneratedOnAdd();
        builder.Property(rt => rt.UserId).HasColumnName("user_id");
        builder.Property(rt => rt.TokenHash).HasColumnName("token_hash").HasMaxLength(255).IsRequired();
        builder.Property(rt => rt.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(rt => rt.IsRevoked).HasColumnName("is_revoked").HasDefaultValue(false);
        builder.Property(rt => rt.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(al => al.LogId);
        builder.Property(al => al.LogId).HasColumnName("log_id").ValueGeneratedOnAdd();
        builder.Property(al => al.UserId).HasColumnName("user_id");
        builder.Property(al => al.ActionType).HasColumnName("action_type").HasMaxLength(100).IsRequired();
        builder.Property(al => al.EntityName).HasColumnName("entity_name").HasMaxLength(100).IsRequired();
        builder.Property(al => al.EntityId).HasColumnName("entity_id").HasMaxLength(100).IsRequired();
        builder.Property(al => al.BeforeState).HasColumnName("before_state");
        builder.Property(al => al.AfterState).HasColumnName("after_state");
        builder.Property(al => al.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(al => al.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(al => al.User)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(al => al.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
