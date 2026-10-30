using Microsoft.EntityFrameworkCore;
using WMMS.Domain.Constants;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Entities.KanbanQc;
using WMMS.Domain.Entities.CatalogBom;
using WMMS.Domain.Entities.Orders;
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

        // 2b. Seed Owner + sample staff users (UC-06), audit logs (UC-13/GBR-05), sample Orders/OrderSpaces (UC-33/34/35)
        {
            var seedNow = DateTime.UtcNow;
            var roleMap = await context.Roles.ToDictionaryAsync(r => r.RoleCode, r => r.RoleId);
            var staff = new (string Username, string Email, string FullName, string Role)[]
            {
                ("owner", "owner@nguyenkhoa.vn", "Nguyen Khoa Owner", RoleConstants.Owner),
                ("sales01", "sales01@nguyenkhoa.vn", "Sales Staff 01", RoleConstants.Sales),
                ("planner01", "planner01@nguyenkhoa.vn", "Planner 01", RoleConstants.Planner),
                ("purchasing01", "purchasing01@nguyenkhoa.vn", "Purchasing 01", RoleConstants.Purchasing),
                ("warehouse01", "warehouse01@nguyenkhoa.vn", "Warehouse 01", RoleConstants.Warehouse),
                ("leader01", "leader01@nguyenkhoa.vn", "Team Leader 01", RoleConstants.Leader),
                ("qc01", "qc01@nguyenkhoa.vn", "QC Inspector 01", RoleConstants.Qc),
                ("sales02", "sales02@nguyenkhoa.vn", "Sales Staff 02", RoleConstants.Sales)
            };
            foreach (var s in staff)
            {
                if (await context.Users.AnyAsync(u => u.Username == s.Username)) continue;
                var u = new User
                {
                    Username = s.Username,
                    Email = s.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    FullName = s.FullName,
                    Status = UserStatusEnum.ACTIVE,
                    CreatedAt = seedNow,
                    UpdatedAt = seedNow
                };
                await context.Users.AddAsync(u);
                await context.SaveChangesAsync();
                await context.UserRoles.AddAsync(new UserRole { UserId = u.UserId, RoleId = roleMap[s.Role], AssignedAt = seedNow });
                await context.SaveChangesAsync();
            }

            var seedAdmin = await context.Users.FirstAsync(u => u.Username == "admin");
            var seedOwner = await context.Users.FirstAsync(u => u.Username == "owner");

            if (!await context.AuditLogs.AnyAsync())
            {
                await context.AuditLogs.AddRangeAsync(
                    new AuditLog { UserId = seedAdmin.UserId, ActionType = "AUTH_LOGIN_SUCCESS", EntityName = "User", EntityId = seedAdmin.UserId.ToString(), Reason = "Seed data", CreatedAt = seedNow },
                    new AuditLog { UserId = seedAdmin.UserId, ActionType = "USER_CREATED", EntityName = "User", EntityId = seedOwner.UserId.ToString(), Reason = "Seed data", CreatedAt = seedNow },
                    new AuditLog { UserId = seedOwner.UserId, ActionType = "AUTH_LOGIN_SUCCESS", EntityName = "User", EntityId = seedOwner.UserId.ToString(), Reason = "Seed data", CreatedAt = seedNow });
                await context.SaveChangesAsync();
            }

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
                    MaterialName = "GÃ¡Â»â€” sÃ¡Â»â€œi BÃ¡ÂºÂ¯c MÃ¡Â»Â¹ xÃ¡ÂºÂ» sÃ¡ÂºÂ¥y FAS (Oak)",
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
                    MaterialName = "GÃ¡Â»â€” cao su ghÃƒÂ©p thanh tiÃƒÂªu chuÃ¡ÂºÂ©n AB",
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
                    MaterialName = "VÃƒÂ­t bÃ¡ÂºÂ¯t gÃ¡Â»â€” tÃ¡Â»Â± khoan mÃ¡ÂºÂ¡ kÃ¡ÂºÂ½m M4x30",
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
                    MaterialName = "BÃ¡ÂºÂ£n lÃ¡Â»Â giÃ¡ÂºÂ£m chÃ¡ÂºÂ¥n Hafele SUS304",
                    MaterialGroup = MaterialGroupEnum.HARDWARE,
                    UnitOfMeasure = "PCS",
                    DimensionSpec = "GÃƒÂ³c mÃ¡Â»Å¸ 110 Ã„â€˜Ã¡Â»â„¢",
                    FscCertified = false,
                    SafetyStockQty = 500.0m,
                    Status = "ACTIVE",
                    CreatedAt = now
                },
                new()
                {
                    MaterialCode = "HW_HANDLE_01",
                    MaterialName = "Tay nÃ¡ÂºÂ¯m hÃ¡Â»Â£p kim nhÃƒÂ´m xÃ†Â°Ã¡Â»â€ºc mÃ¡Â»Â",
                    MaterialGroup = MaterialGroupEnum.HARDWARE,
                    UnitOfMeasure = "PCS",
                    DimensionSpec = "TÃƒÂ¢m lÃ¡Â»â€” 96mm",
                    FscCertified = false,
                    SafetyStockQty = 300.0m,
                    Status = "ACTIVE",
                    CreatedAt = now
                },
                new()
                {
                    MaterialCode = "PAINT_SEALER_01",
                    MaterialName = "SÃ†Â¡n lÃƒÂ³t PU 2K khÃƒÂ¡ng Ã¡ÂºÂ©m gÃ¡Â»â€˜c nÃ†Â°Ã¡Â»â€ºc",
                    MaterialGroup = MaterialGroupEnum.FINISH_PAINT,
                    UnitOfMeasure = "LITER",
                    DimensionSpec = "ThÃƒÂ¹ng 20L",
                    FscCertified = false,
                    SafetyStockQty = 100.0m,
                    Status = "ACTIVE",
                    CreatedAt = now
                },
                new()
                {
                    MaterialCode = "PAINT_TOP_MATT",
                    MaterialName = "SÃ†Â¡n phÃ¡Â»Â§ bÃƒÂ³ng mÃ¡Â»Â PU 50%",
                    MaterialGroup = MaterialGroupEnum.FINISH_PAINT,
                    UnitOfMeasure = "LITER",
                    DimensionSpec = "ThÃƒÂ¹ng 20L",
                    FscCertified = false,
                    SafetyStockQty = 80.0m,
                    Status = "ACTIVE",
                    CreatedAt = now
                },
                new()
                {
                    MaterialCode = "BOX_CARTON_01",
                    MaterialName = "ThÃƒÂ¹ng carton 5 lÃ¡Â»â€ºp xuÃ¡ÂºÂ¥t khÃ¡ÂºÂ©u Master",
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
                ProductName = "GhÃ¡ÂºÂ¿ Ã„Æ’n tÃ¡Â»Â±a nan gÃ¡Â»â€” sÃ¡Â»â€œi BÃ¡ÂºÂ¯c MÃ¡Â»Â¹",
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
                ProductName = "BÃƒÂ n Ã„Æ’n thÃƒÂ´ng minh kÃƒÂ©o dÃƒÂ i gÃ¡Â»â€” sÃ¡Â»â€œi",
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
                ProductName = "TÃ¡Â»Â§ sideboard 3 cÃƒÂ¡nh 2 hÃ¡Â»â„¢c kÃƒÂ©o",
                CategoryName = "CABINET",
                DimensionsCm = "150 x 42 x 82 cm",
                WoodSpecies = "Rubber Wood + Oak Veneer",
                FinishColor = "Warm Chestnut",
                PackingSpec = "1 pc / Carton kÃƒÂ¨m xÃ¡Â»â€˜p Ã„â€˜Ã¡Â»â€¹nh hÃƒÂ¬nh",
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
                    new() { RoutingId = routing.RoutingId, StageSequence = 1, StageName = "1. Pha phÃƒÂ´i gÃ¡Â»â€” thÃƒÂ´", IsHoldPoint = false },
                    new() { RoutingId = routing.RoutingId, StageSequence = 2, StageName = "2. CÃ¡ÂºÂ¯t/XÃ¡ÂºÂ» Ã„â€˜Ã¡Â»â€¹nh hÃƒÂ¬nh chi tiÃ¡ÂºÂ¿t", IsHoldPoint = false },
                    new() { RoutingId = routing.RoutingId, StageSequence = 3, StageName = "3. BÃƒÂ o 4 mÃ¡ÂºÂ·t & ChÃƒÂ  nhÃƒÂ¡m phÃ¡ÂºÂ³ng", IsHoldPoint = false },
                    new() { RoutingId = routing.RoutingId, StageSequence = 4, StageName = "4. Khoan lÃ¡Â»â€” & Phay mÃ¡Â»â„¢ng mÃ¡Â»â„¢ng ÃƒÂ¢m/dÃ†Â°Ã†Â¡ng", IsHoldPoint = false },
                    new() { RoutingId = routing.RoutingId, StageSequence = 5, StageName = "5. LÃ¡ÂºÂ¯p rÃƒÂ¡p kÃ¡ÂºÂ¿t cÃ¡ÂºÂ¥u & BÃ¡ÂºÂ¯t phÃ¡Â»Â¥ kiÃ¡Â»â€¡n bÃ¡ÂºÂ£n lÃ¡Â»Â/ray trÃ†Â°Ã¡Â»Â£t", IsHoldPoint = true },
                    new() { RoutingId = routing.RoutingId, StageSequence = 6, StageName = "6. SÃ†Â¡n lÃƒÂ³t & SÃ†Â¡n phÃ¡Â»Â§ hoÃƒÂ n thiÃ¡Â»â€¡n PU", IsHoldPoint = false },
                    new() { RoutingId = routing.RoutingId, StageSequence = 7, StageName = "7. KiÃ¡Â»Æ’m tra xuÃ¡ÂºÂ¥t xÃ†Â°Ã¡Â»Å¸ng & Ã„ÂÃƒÂ³ng gÃƒÂ³i carton", IsHoldPoint = false }
                };

                await context.RoutingStages.AddRangeAsync(stages);
                await context.SaveChangesAsync();
            }
        }

        // 7. Seed sample B2B Orders (CustomerOrder + OrderItem + OrderSpace) - UC-33/34/35. Total 1,450 Pcs across 4 spaces; SPACE-PO-880504 = 500 Pcs.
        if (!await context.OrderSpaces.AnyAsync())
        {
            var oNow = DateTime.UtcNow;
            var oAdmin = await context.Users.FirstAsync(u => u.Username == "admin");
            var oCreator = await context.Users.FirstOrDefaultAsync(u => u.Username == "sales") ?? oAdmin;
            var oProducts = await context.Products.OrderBy(p => p.ProductId).ToListAsync();
            if (oProducts.Count > 0)
            {
                var oCustomer = new Customer { CustomerCode = "CUST-ASHLEY-US", CompanyName = "Ashley Furniture HomeStore", Country = "USA", ContactPerson = "Michael Vance", Email = "procurement@ashleyfurniture.com", CreatedAt = oNow, UpdatedAt = oNow };
                await context.Customers.AddAsync(oCustomer);
                await context.SaveChangesAsync();

                var orderSeeds = new (string Po, string Key, int Qty, Product Prod, int Days)[]
                {
                    ("ASH-2026-OCT-880504", "SPACE-PO-880504", 500, oProducts[0], 18),
                    ("ASH-2026-OCT-880505", "SPACE-PO-880505", 350, oProducts[1 % oProducts.Count], 22),
                    ("ASH-2026-OCT-880506", "SPACE-PO-880506", 300, oProducts[2 % oProducts.Count], 25),
                    ("ASH-2026-OCT-880507", "SPACE-PO-880507", 300, oProducts[0], 30)
                };
                foreach (var os in orderSeeds)
                {
                    var order = new CustomerOrder { PoNumber = os.Po, CustomerId = oCustomer.CustomerId, CreatedBy = oCreator.UserId, OrderDate = oNow.Date.AddDays(-3), EtdDate = oNow.Date.AddDays(os.Days), TotalCbm = Math.Round((os.Prod.CbmPerUnit ?? 0.15m) * os.Qty, 4), Status = OrderStatusEnum.CONFIRMED, CreatedAt = oNow, UpdatedAt = oNow };
                    await context.CustomerOrders.AddAsync(order);
                    await context.SaveChangesAsync();
                    await context.OrderItems.AddAsync(new OrderItem { OrderId = order.OrderId, ProductId = os.Prod.ProductId, OrderedQty = os.Qty, FscCertifiedReq = true, LengthMm = 450, WidthMm = 520, HeightMm = 880, Status = OrderItemStatusEnum.PENDING_BOM });
                    await context.OrderSpaces.AddAsync(new OrderSpace { OrderId = order.OrderId, SpaceKey = os.Key, SpaceName = $"{os.Key} ({os.Prod.ProductName} - {oCustomer.CompanyName})", DrawingFolderUrl = $"/spaces/{os.Key}/drawings", OverallProgressPct = 0m, Status = SpaceStatusEnum.ACTIVE, CreatedAt = oNow, UpdatedAt = oNow });
                    await context.SaveChangesAsync();
                }
            }
        }    }
}

