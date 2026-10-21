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
                new() { RoleCode = RoleConstants.Admin, RoleName = "System Administrator", PermissionsJson = "{\"all\": true}" },
                new() { RoleCode = RoleConstants.Owner, RoleName = "Factory Owner / Executive", PermissionsJson = "{\"view_all\": true, \"approve_orders\": true}" },
                new() { RoleCode = RoleConstants.Sales, RoleName = "Sales Staff", PermissionsJson = "{\"manage_orders\": true, \"view_customers\": true}" },
                new() { RoleCode = RoleConstants.Planner, RoleName = "Production Planner", PermissionsJson = "{\"manage_boms\": true, \"manage_batches\": true, \"manage_prs\": true}" },
                new() { RoleCode = RoleConstants.Purchasing, RoleName = "Purchasing Officer", PermissionsJson = "{\"manage_pos\": true, \"manage_suppliers\": true}" },
                new() { RoleCode = RoleConstants.Warehouse, RoleName = "Warehouse Keeper", PermissionsJson = "{\"manage_inventory\": true, \"manage_grns\": true}" },
                new() { RoleCode = RoleConstants.Leader, RoleName = "Production Team Leader", PermissionsJson = "{\"manage_routing\": true, \"view_kanban\": true}" },
                new() { RoleCode = RoleConstants.Qc, RoleName = "Quality Control Inspector", PermissionsJson = "{\"manage_qc\": true, \"log_defects\": true}" }
            };

            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();
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

        // 5. Seed Default Materials
        if (!await context.Materials.AnyAsync())
        {
            var now = DateTime.UtcNow;
            var materials = new List<WMMS.Domain.Entities.CatalogBom.Material>
            {
                new()
                {
                    MaterialCode = "WOOD_OAK_01",
                    MaterialName = "Gỗ sồi Bắc Mỹ xẻ sấy FAS (Oak)",
                    MaterialGroup = MaterialGroupEnum.LUMBER,
                    UnitOfMeasure = "M3",
                    DimensionSpec = "26mm x 150mm x 2400mm",
                    FscCertified = true,
                    SafetyStockQty = 10.0m,
                    Status = "ACTIVE",
                    CreatedAt = now
                },
                new()
                {
                    MaterialCode = "WOOD_RUBBER_02",
                    MaterialName = "Gỗ cao su ghép thanh tiêu chuẩn AB",
                    MaterialGroup = MaterialGroupEnum.LUMBER,
                    UnitOfMeasure = "M3",
                    DimensionSpec = "20mm x 1200mm x 2400mm",
                    FscCertified = true,
                    SafetyStockQty = 15.0m,
                    Status = "ACTIVE",
                    CreatedAt = now
                },
                new()
                {
                    MaterialCode = "HW_SCREW_M4",
                    MaterialName = "Vít bắt gỗ tự khoan mạ kẽm M4x30",
                    MaterialGroup = MaterialGroupEnum.HARDWARE,
                    UnitOfMeasure = "PCS",
                    DimensionSpec = "M4 x 30mm",
                    FscCertified = false,
                    SafetyStockQty = 2000.0m,
                    Status = "ACTIVE",
                    CreatedAt = now
                },
                new()
                {
                    MaterialCode = "HW_HINGE_01",
                    MaterialName = "Bản lề giảm chấn Hafele SUS304",
                    MaterialGroup = MaterialGroupEnum.HARDWARE,
                    UnitOfMeasure = "PCS",
                    DimensionSpec = "Góc mở 110 độ",
                    FscCertified = false,
                    SafetyStockQty = 500.0m,
                    Status = "ACTIVE",
                    CreatedAt = now
                },
                new()
                {
                    MaterialCode = "HW_HANDLE_01",
                    MaterialName = "Tay nắm hợp kim nhôm xước mờ",
                    MaterialGroup = MaterialGroupEnum.HARDWARE,
                    UnitOfMeasure = "PCS",
                    DimensionSpec = "Tâm lỗ 96mm",
                    FscCertified = false,
                    SafetyStockQty = 300.0m,
                    Status = "ACTIVE",
                    CreatedAt = now
                },
                new()
                {
                    MaterialCode = "PAINT_SEALER_01",
                    MaterialName = "Sơn lót PU 2K kháng ẩm gốc nước",
                    MaterialGroup = MaterialGroupEnum.FINISH_PAINT,
                    UnitOfMeasure = "LITER",
                    DimensionSpec = "Thùng 20L",
                    FscCertified = false,
                    SafetyStockQty = 100.0m,
                    Status = "ACTIVE",
                    CreatedAt = now
                },
                new()
                {
                    MaterialCode = "PAINT_TOP_MATT",
                    MaterialName = "Sơn phủ bóng mờ PU 50%",
                    MaterialGroup = MaterialGroupEnum.FINISH_PAINT,
                    UnitOfMeasure = "LITER",
                    DimensionSpec = "Thùng 20L",
                    FscCertified = false,
                    SafetyStockQty = 80.0m,
                    Status = "ACTIVE",
                    CreatedAt = now
                },
                new()
                {
                    MaterialCode = "BOX_CARTON_01",
                    MaterialName = "Thùng carton 5 lớp xuất khẩu Master",
                    MaterialGroup = MaterialGroupEnum.PACKAGING,
                    UnitOfMeasure = "PCS",
                    DimensionSpec = "600mm x 550mm x 950mm",
                    FscCertified = true,
                    SafetyStockQty = 400.0m,
                    Status = "ACTIVE",
                    CreatedAt = now
                }
            };

            await context.Materials.AddRangeAsync(materials);
            await context.SaveChangesAsync();
        }

        // 6. Seed Visual Catalog Products & Default Routing
        if (!await context.Products.AnyAsync())
        {
            var now = DateTime.UtcNow;
            var chair = new WMMS.Domain.Entities.CatalogBom.Product
            {
                ProductSku = "CHAIR-DINING-OAK-01",
                ProductName = "Ghế ăn tựa nan gỗ sồi Bắc Mỹ",
                CategoryName = "CHAIR",
                DimensionsCm = "45 x 52 x 88 cm",
                WoodSpecies = "White Oak",
                FinishColor = "Natural Walnut",
                PackingSpec = "2 pcs / Master Carton",
                CbmPerUnit = 0.12m,
                Status = "ACTIVE",
                CreatedAt = now
            };

            var table = new WMMS.Domain.Entities.CatalogBom.Product
            {
                ProductSku = "TABLE-DINING-EXT-02",
                ProductName = "Bàn ăn thông minh kéo dài gỗ sồi",
                CategoryName = "TABLE",
                DimensionsCm = "160/210 x 90 x 75 cm",
                WoodSpecies = "White Oak",
                FinishColor = "Smoked Oak",
                PackingSpec = "1 pc / 2 Cartons",
                CbmPerUnit = 0.35m,
                Status = "ACTIVE",
                CreatedAt = now
            };

            var cabinet = new WMMS.Domain.Entities.CatalogBom.Product
            {
                ProductSku = "CABINET-SIDEBOARD-03",
                ProductName = "Tủ sideboard 3 cánh 2 hộc kéo",
                CategoryName = "CABINET",
                DimensionsCm = "150 x 42 x 82 cm",
                WoodSpecies = "Rubber Wood + Oak Veneer",
                FinishColor = "Warm Chestnut",
                PackingSpec = "1 pc / Carton kèm xốp định hình",
                CbmPerUnit = 0.58m,
                Status = "ACTIVE",
                CreatedAt = now
            };

            await context.Products.AddRangeAsync(chair, table, cabinet);
            await context.SaveChangesAsync();

            // Default Routings for Products
            var products = new[] { chair, table, cabinet };
            foreach (var p in products)
            {
                var routing = new WMMS.Domain.Entities.CatalogBom.RoutingTemplate
                {
                    ProductId = p.ProductId,
                    VersionCode = "v1.0",
                    IsActive = true,
                    CreatedAt = now
                };

                await context.RoutingTemplates.AddAsync(routing);
                await context.SaveChangesAsync();

                var stages = new List<WMMS.Domain.Entities.CatalogBom.RoutingStage>
                {
                    new() { RoutingId = routing.RoutingId, StageSequence = 1, StageName = "1. Pha phôi gỗ thô", IsHoldPoint = false },
                    new() { RoutingId = routing.RoutingId, StageSequence = 2, StageName = "2. Cắt/Xẻ định hình chi tiết", IsHoldPoint = false },
                    new() { RoutingId = routing.RoutingId, StageSequence = 3, StageName = "3. Bào 4 mặt & Chà nhám phẳng", IsHoldPoint = false },
                    new() { RoutingId = routing.RoutingId, StageSequence = 4, StageName = "4. Khoan lỗ & Phay mộng mộng âm/dương", IsHoldPoint = false },
                    new() { RoutingId = routing.RoutingId, StageSequence = 5, StageName = "5. Lắp ráp kết cấu & Bắt phụ kiện bản lề/ray trượt", IsHoldPoint = true },
                    new() { RoutingId = routing.RoutingId, StageSequence = 6, StageName = "6. Sơn lót & Sơn phủ hoàn thiện PU", IsHoldPoint = false },
                    new() { RoutingId = routing.RoutingId, StageSequence = 7, StageName = "7. Kiểm tra xuất xưởng & Đóng gói carton", IsHoldPoint = false }
                };

                await context.RoutingStages.AddRangeAsync(stages);
                await context.SaveChangesAsync();
            }
        }
    }
}

