using Microsoft.EntityFrameworkCore;
using WMMS.Domain.Constants;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Entities.KanbanQc;
using WMMS.Domain.Entities.System;
using WMMS.Domain.Enums;

namespace WMMS.Infrastructure.Persistence.Seed;

public static class DbInitializer
{
    public static async Task SeedAsync(WmmsDbContext context)
    {
        // 1. Seed Roles
        if (!await context.Roles.AnyAsync())
        {
            var roles = new List<Role>
            {
                new() { RoleCode = RoleConstants.Admin, RoleName = "System Administrator", Description = "Full system control, user & multi-role RBAC management, and system audit log inspection.", PermissionsJson = "{\"all\": true, \"users.manage\": true, \"roles.manage\": true, \"audit.view\": true}" },
                new() { RoleCode = RoleConstants.Owner, RoleName = "Factory Owner / Executive", Description = "Executive KPI dashboard, ETD risk monitoring, and high-level order/PR approvals.", PermissionsJson = "{\"view_all\": true, \"approve_orders\": true, \"approve_prs\": true, \"dashboard.view\": true}" },
                new() { RoleCode = RoleConstants.Sales, RoleName = "Sales Staff", Description = "Customer order registration, space & item management, and order progress tracking.", PermissionsJson = "{\"manage_orders\": true, \"view_customers\": true}" },
                new() { RoleCode = RoleConstants.Planner, RoleName = "Production Planner", Description = "BOM definition, routing configuration, inventory reconciliation, PR consolidation, and batch creation.", PermissionsJson = "{\"manage_boms\": true, \"manage_batches\": true, \"manage_prs\": true}" },
                new() { RoleCode = RoleConstants.Purchasing, RoleName = "Purchasing Officer", Description = "Supplier management, PO issuance from approved PRs, and procurement tracking.", PermissionsJson = "{\"manage_pos\": true, \"manage_suppliers\": true}" },
                new() { RoleCode = RoleConstants.Warehouse, RoleName = "Warehouse Keeper", Description = "Goods receipt (GRN), material stock management, and shop-floor material issuance.", PermissionsJson = "{\"manage_inventory\": true, \"manage_grns\": true}" },
                new() { RoleCode = RoleConstants.Leader, RoleName = "Production Team Leader", Description = "Kanban board execution, workstation step progression, and rework sub-card handling.", PermissionsJson = "{\"manage_routing\": true, \"view_kanban\": true, \"execute_steps\": true}" },
                new() { RoleCode = RoleConstants.Qc, RoleName = "Quality Control Inspector", Description = "100% QC gate inspection, 97%-3% defect branching, and Hold-Point verification.", PermissionsJson = "{\"manage_qc\": true, \"log_defects\": true}" }
            };

            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();
        }
        else
        {
            // Cập nhật Description cho các Role đã seed trước đó nếu đang null
            var existingRoles = await context.Roles.Where(r => r.Description == null).ToListAsync();
            if (existingRoles.Count > 0)
            {
                foreach (var role in existingRoles)
                {
                    role.Description = role.RoleCode switch
                    {
                        RoleConstants.Admin => "Full system control, user & multi-role RBAC management, and system audit log inspection.",
                        RoleConstants.Owner => "Executive KPI dashboard, ETD risk monitoring, and high-level order/PR approvals.",
                        RoleConstants.Sales => "Customer order registration, space & item management, and order progress tracking.",
                        RoleConstants.Planner => "BOM definition, routing configuration, inventory reconciliation, PR consolidation, and batch creation.",
                        RoleConstants.Purchasing => "Purchasing Officer responsible for suppliers and PO issuance.",
                        RoleConstants.Warehouse => "Warehouse Keeper responsible for inventory stock and GRN receipts.",
                        RoleConstants.Leader => "Production Team Leader responsible for shop-floor Kanban execution.",
                        RoleConstants.Qc => "Quality Control Inspector responsible for stage gate inspections and defect logs.",
                        _ => role.RoleName
                    };
                }
                await context.SaveChangesAsync();
            }
        }

        // 2. Seed Default Admin User
        if (!await context.Users.AnyAsync(u => u.Username == "admin"))
        {
            var adminRole = await context.Roles.FirstAsync(r => r.RoleCode == RoleConstants.Admin);
            var now = DateTime.UtcNow;

            var adminUser = new User
            {
                Username = "admin",
                Email = "admin@wmms.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                FullName = "System Administrator",
                Phone = "0901234567",
                Status = UserStatusEnum.ACTIVE,
                CreatedAt = now,
                UpdatedAt = now
            };

            await context.Users.AddAsync(adminUser);
            await context.SaveChangesAsync();

            await context.UserRoles.AddAsync(new UserRole
            {
                UserId = adminUser.UserId,
                RoleId = adminRole.RoleId,
                AssignedAt = now
            });
            await context.SaveChangesAsync();
        }

        // 3. Seed Defect Catalog
        if (!await context.DefectCatalogs.AnyAsync())
        {
            var defects = new List<DefectCatalog>
            {
                new()
                {
                    DefectCode = DefectCodeConstants.CutErr01,
                    DefectName = "Wood Cutting Deviation",
                    StandardStage = "Wood Preparation",
                    RootCause = "Saw blade wear or machine vibration",
                    RecommendedAction = "Recalibrate saw blade and re-cut piece"
                },
                new()
                {
                    DefectCode = DefectCodeConstants.WoodErr01,
                    DefectName = "Lumber Knot Defect",
                    StandardStage = "Wood Preparation",
                    RootCause = "Natural grain knot / structural weakness",
                    RecommendedAction = "Cut out knot or re-classify wood piece"
                },
                new()
                {
                    DefectCode = DefectCodeConstants.SurfErr02,
                    DefectName = "Rough Surface Finish",
                    StandardStage = "Surface Prep",
                    RootCause = "Uneven sanding or grit paper worn",
                    RecommendedAction = "Re-sand surface to required smoothness"
                },
                new()
                {
                    DefectCode = DefectCodeConstants.AssyErr03,
                    DefectName = "Joint Alignment Mismatch",
                    StandardStage = "Assembly",
                    RootCause = "Misaligned dowel or tenon issue",
                    RecommendedAction = "Disassemble and re-align joinery"
                },
                new()
                {
                    DefectCode = DefectCodeConstants.PaintErr04,
                    DefectName = "Paint Bubble / Drips",
                    StandardStage = "Finishing",
                    RootCause = "Excess paint spray or dust particle",
                    RecommendedAction = "Scrape, re-sand, and re-spray coating"
                }
            };

            await context.DefectCatalogs.AddRangeAsync(defects);
            await context.SaveChangesAsync();
        }

        // 4. Seed Default System Configs
        if (!await context.SystemConfigs.AnyAsync())
        {
            var now = DateTime.UtcNow;
            var configs = new List<SystemConfig>
            {
                new()
                {
                    ConfigKey = SystemConfigKeys.DefaultScrapMin,
                    ConfigValue = "3.00",
                    DataType = "DECIMAL",
                    Description = "Minimum default scrap allowance percentage (3%)",
                    UpdatedAt = now
                },
                new()
                {
                    ConfigKey = SystemConfigKeys.DefaultScrapMax,
                    ConfigValue = "5.00",
                    DataType = "DECIMAL",
                    Description = "Maximum default scrap allowance percentage (5%)",
                    UpdatedAt = now
                },
                new()
                {
                    ConfigKey = SystemConfigKeys.EtdWarningDays,
                    ConfigValue = "7",
                    DataType = "INT",
                    Description = "Advance warning threshold in days before Order ETD",
                    UpdatedAt = now
                }
            };

            await context.SystemConfigs.AddRangeAsync(configs);
            await context.SaveChangesAsync();
        }
    }
}
