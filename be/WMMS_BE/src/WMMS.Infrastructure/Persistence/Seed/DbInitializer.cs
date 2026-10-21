using Microsoft.EntityFrameworkCore;
using WMMS.Domain.Constants;
using WMMS.Domain.Entities.CatalogBom;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Entities.KanbanQc;
using WMMS.Domain.Entities.Orders;
using WMMS.Domain.Entities.System;
using WMMS.Domain.Enums;

namespace WMMS.Infrastructure.Persistence.Seed;

/// <summary>
/// Khởi tạo dữ liệu nền tảng (Seed Data) cho hệ thống WMMS khớp với đặc tả Report 3 (SCR-ADM-01, SCR-ADM-02, SCR-ORD-01..03).
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(WmmsDbContext context)
    {
        // 1. Seed 8 Roles chuẩn
        if (!await context.Roles.AnyAsync())
        {
            var roles = new List<Role>
            {
                new() { RoleCode = RoleConstants.Admin, RoleName = "System Administrator", Description = "Full system control, user & multi-role RBAC management, and system audit log inspection.", PermissionsJson = "{\"all\": true, \"users.manage\": true, \"roles.manage\": true, \"audit.view\": true}" },
                new() { RoleCode = RoleConstants.Owner, RoleName = "Workshop Owner", Description = "BOM Breakdown, Purchasing Approval, Dispatch Release", PermissionsJson = "{\"view_all\": true, \"approve_orders\": true, \"approve_prs\": true, \"dashboard.view\": true}" },
                new() { RoleCode = RoleConstants.Sales, RoleName = "Sales Staff", Description = "Excel PO Staging, CRUD Sanitization, Space Init", PermissionsJson = "{\"manage_orders\": true, \"view_customers\": true}" },
                new() { RoleCode = RoleConstants.Planner, RoleName = "Material Planning Staff", Description = "BOM definition, routing configuration, inventory reconciliation, PR consolidation.", PermissionsJson = "{\"manage_boms\": true, \"manage_batches\": true, \"manage_prs\": true}" },
                new() { RoleCode = RoleConstants.Purchasing, RoleName = "Purchasing Staff", Description = "Vendor Sourcing, PO Creation & ETA Management", PermissionsJson = "{\"manage_pos\": true, \"manage_suppliers\": true}" },
                new() { RoleCode = RoleConstants.Warehouse, RoleName = "Warehouse Keeper", Description = "Goods Receipt Commit, Goods Issue Floor Handover", PermissionsJson = "{\"manage_inventory\": true, \"manage_grns\": true}" },
                new() { RoleCode = RoleConstants.Leader, RoleName = "Production Leader", Description = "Kanban Floor Dispatch, Overdriver Requests", PermissionsJson = "{\"manage_routing\": true, \"view_kanban\": true, \"execute_steps\": true}" },
                new() { RoleCode = RoleConstants.Qc, RoleName = "QC Staff", Description = "Workstation Quality Gate, Defect Logging", PermissionsJson = "{\"manage_qc\": true, \"log_defects\": true}" }
            };

            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();
        }

        var now = DateTime.UtcNow;
        var allRoles = await context.Roles.ToDictionaryAsync(r => r.RoleCode, r => r);

        // 2. Seed Default Users khớp với Employee User Directory trên màn hình SCR-ADM-01
        var seedUsers = new (string Username, string Email, string FullName, string Phone, string RoleCode, UserStatusEnum Status)[]
        {
            ("admin", "admin@nguyenkhoa.vn", "Le Van Admin", "0901000001", RoleConstants.Admin, UserStatusEnum.ACTIVE),
            ("owner", "owner@nguyenkhoa.vn", "Nguyen Van Khoa", "0901000002", RoleConstants.Owner, UserStatusEnum.ACTIVE),
            ("sales", "sales@nguyenkhoa.vn", "Tran Thi Mai", "0901000003", RoleConstants.Sales, UserStatusEnum.ACTIVE),
            ("leader", "leader@nguyenkhoa.vn", "Pham Van Tien", "0901000004", RoleConstants.Leader, UserStatusEnum.ACTIVE),
            ("qc", "qc@nguyenkhoa.vn", "Hoang Thi Lan", "0901000005", RoleConstants.Qc, UserStatusEnum.ACTIVE),
            ("warehouse", "warehouse@nguyenkhoa.vn", "Vu Duc Thang", "0901000006", RoleConstants.Warehouse, UserStatusEnum.ACTIVE),
            ("purchaser", "purchaser@nguyenkhoa.vn", "Do Quang Huy", "0901000007", RoleConstants.Purchasing, UserStatusEnum.ACTIVE),
            ("tam.ex", "tam.ex@nguyenkhoa.vn", "Nguyen Van Tam", "0901000008", RoleConstants.Planner, UserStatusEnum.BANNED)
        };

        var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123");

        foreach (var su in seedUsers)
        {
            if (!await context.Users.AnyAsync(u => u.Username == su.Username || u.Email == su.Email))
            {
                var user = new User
                {
                    Username = su.Username,
                    Email = su.Email,
                    PasswordHash = defaultPasswordHash,
                    FullName = su.FullName,
                    Phone = su.Phone,
                    Status = su.Status,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                await context.Users.AddAsync(user);
                await context.SaveChangesAsync();

                if (allRoles.TryGetValue(su.RoleCode, out var role))
                {
                    await context.UserRoles.AddAsync(new UserRole
                    {
                        UserId = user.UserId,
                        RoleId = role.RoleId,
                        AssignedAt = now
                    });
                    await context.SaveChangesAsync();
                }
            }
        }

        // 3. Seed Defect Catalog (BR-QC-02)
        if (!await context.DefectCatalogs.AnyAsync())
        {
            var defects = new List<DefectCatalog>
            {
                new()
                {
                    DefectCode = DefectCodeConstants.CutErr01,
                    DefectName = "Dimensional Out of Tolerance (Cut size error)",
                    StandardStage = "Station 1: Rough Cutting",
                    RootCause = "Fence guide alignment shift / operator push rate",
                    RecommendedAction = "Rework (trim down) or Scrap if undersized"
                },
                new()
                {
                    DefectCode = DefectCodeConstants.WoodErr01,
                    DefectName = "Lumber Knot / Split Crack Defect",
                    StandardStage = "Station 1: Rough Cutting",
                    RootCause = "Natural grain knot / structural weakness",
                    RecommendedAction = "Cut out knot or replace wood piece"
                },
                new()
                {
                    DefectCode = DefectCodeConstants.SurfErr02,
                    DefectName = "Surface Unevenness / Scratches / Tearout",
                    StandardStage = "Station 2: Planing & Sanding",
                    RootCause = "Worn planer knives or improper sandpaper grit",
                    RecommendedAction = "Rework (re-sand with standard grit)"
                },
                new()
                {
                    DefectCode = DefectCodeConstants.AssyErr03,
                    DefectName = "Mortise Misalignment / Square Joint Gap",
                    StandardStage = "Station 4: Structure Assembly",
                    RootCause = "Drill bit off-center or uneven clamp pressure",
                    RecommendedAction = "Rework (unclamp, realign & re-clamp)"
                },
                new()
                {
                    DefectCode = DefectCodeConstants.PaintErr04,
                    DefectName = "Paint Runs / Orange Peel / Off-Spec Shade",
                    StandardStage = "Station 5: PU Spray Coating",
                    RootCause = "Improper solvent ratio or uneven gun pass",
                    RecommendedAction = "Rework (strip, re-sand and re-spray)"
                }
            };

            await context.DefectCatalogs.AddRangeAsync(defects);
            await context.SaveChangesAsync();
        }

        // 4. Seed Default System Configs (UC-15)
        if (!await context.SystemConfigs.AnyAsync())
        {
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
                    ConfigValue = "10",
                    DataType = "INT",
                    Description = "Advance warning threshold in days before Order ETD",
                    UpdatedAt = now
                }
            };

            await context.SystemConfigs.AddRangeAsync(configs);
            await context.SaveChangesAsync();
        }

        // 5. Seed Products, Customers, Sales Orders & Order Spaces (SCR-ORD-01, SCR-ORD-02, SCR-ORD-03)
        if (!await context.Products.AnyAsync())
        {
            var products = new List<Product>
            {
                new()
                {
                    ProductSku = "CHAIR-OAK-01",
                    ProductName = "Nordic Oak Dining Chair",
                    CategoryName = "CHAIRS & SEATING",
                    DimensionsCm = "450 x 520 x 850",
                    WoodSpecies = "American White Oak",
                    FinishColor = "Natural PU Clear Coat",
                    PackingSpec = "2 Chairs / Master Carton",
                    CbmPerUnit = 0.1989m,
                    CadDrawingUrl = "/drawings/CAD_Chair_880504_Rev2.pdf",
                    Status = "ACTIVE",
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new()
                {
                    ProductSku = "TABLE-OAK-02",
                    ProductName = "Solid Oak Dining Table 1.8m",
                    CategoryName = "TABLES & STORAGE",
                    DimensionsCm = "1800 x 900 x 750",
                    WoodSpecies = "American White Oak + Steel Bracket",
                    FinishColor = "Natural PU Clear Coat",
                    PackingSpec = "1 Table / Flat-Pack Carton",
                    CbmPerUnit = 1.2150m,
                    CadDrawingUrl = "/drawings/CAD_Table_180_Rev1.pdf",
                    Status = "ACTIVE",
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new()
                {
                    ProductSku = "COFFEE-OAK-06",
                    ProductName = "Oak Coffee Table",
                    CategoryName = "TABLES & STORAGE",
                    DimensionsCm = "1200 x 600 x 450",
                    WoodSpecies = "European White Oak",
                    FinishColor = "Natural Matte",
                    PackingSpec = "1 Unit / Box",
                    CbmPerUnit = 0.3240m,
                    CadDrawingUrl = "/drawings/CAD_CoffeeTable_06.pdf",
                    Status = "ACTIVE",
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new()
                {
                    ProductSku = "BED-QUEEN-07",
                    ProductName = "Queen Bed Frame",
                    CategoryName = "BEDROOM & CABINETS",
                    DimensionsCm = "2100 x 1600 x 1100",
                    WoodSpecies = "European White Ash",
                    FinishColor = "Walnut Stain",
                    PackingSpec = "2 Boxes / Bed",
                    CbmPerUnit = 0.8500m,
                    CadDrawingUrl = "/drawings/CAD_QueenBed_07.pdf",
                    Status = "ACTIVE",
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new()
                {
                    ProductSku = "STOOL-OAK-04",
                    ProductName = "Solid Wood Stools",
                    CategoryName = "CHAIRS & SEATING",
                    DimensionsCm = "380 x 380 x 450",
                    WoodSpecies = "American White Oak",
                    FinishColor = "Natural Finish",
                    PackingSpec = "4 Stools / Carton",
                    CbmPerUnit = 0.0650m,
                    CadDrawingUrl = "/drawings/CAD_Stool_V1.pdf",
                    Status = "ACTIVE",
                    CreatedAt = now,
                    UpdatedAt = now
                }
            };

            await context.Products.AddRangeAsync(products);
            await context.SaveChangesAsync();
        }

        if (!await context.Customers.AnyAsync())
        {
            var customers = new List<Customer>
            {
                new() { CustomerCode = "CUST-ASHLEY-US", CompanyName = "Ashley Furniture Homestore", Country = "USA", ContactPerson = "Michael Vance", Email = "orders@ashleyfurniture.com", CreatedAt = now, UpdatedAt = now },
                new() { CustomerCode = "CUST-JYSK-DK", CompanyName = "JYSK Nordic", Country = "Denmark", ContactPerson = "Soren Lind", Email = "procurement@jysk.com", CreatedAt = now, UpdatedAt = now },
                new() { CustomerCode = "CUST-IKEA-AU", CompanyName = "IKEA Supply AG", Country = "Australia", ContactPerson = "Claire Evans", Email = "supply.au@ikea.com", CreatedAt = now, UpdatedAt = now },
                new() { CustomerCode = "CUST-NITORI-JP", CompanyName = "Nitori Holdings", Country = "Japan", ContactPerson = "Kenji Sato", Email = "sourcing@nitori.jp", CreatedAt = now, UpdatedAt = now }
            };

            await context.Customers.AddRangeAsync(customers);
            await context.SaveChangesAsync();
        }

        if (!await context.CustomerOrders.AnyAsync())
        {
            var salesUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "sales")
                            ?? await context.Users.FirstAsync();

            var prodChair = await context.Products.FirstAsync(p => p.ProductSku == "CHAIR-OAK-01");
            var prodTable = await context.Products.FirstAsync(p => p.ProductSku == "TABLE-OAK-02");
            var prodCoffee = await context.Products.FirstAsync(p => p.ProductSku == "COFFEE-OAK-06");
            var prodBed = await context.Products.FirstAsync(p => p.ProductSku == "BED-QUEEN-07");
            var prodStool = await context.Products.FirstAsync(p => p.ProductSku == "STOOL-OAK-04");

            var custAshley = await context.Customers.FirstAsync(c => c.CustomerCode == "CUST-ASHLEY-US");
            var custJysk = await context.Customers.FirstAsync(c => c.CustomerCode == "CUST-JYSK-DK");
            var custIkea = await context.Customers.FirstAsync(c => c.CustomerCode == "CUST-IKEA-AU");
            var custNitori = await context.Customers.FirstAsync(c => c.CustomerCode == "CUST-NITORI-JP");

            // Order 1: SPACE-PO-880504 (Ashley Furniture - 500 Sets: 400 Dining Chairs + 100 Tables)
            var order1 = new CustomerOrder
            {
                PoNumber = "ASH-2026-OCT-880504",
                CustomerId = custAshley.CustomerId,
                CreatedBy = salesUser.UserId,
                OrderDate = now.Date.AddDays(-8),
                EtdDate = now.Date.AddDays(8),
                TotalCbm = 201.06m,
                Status = OrderStatusEnum.IN_PRODUCTION,
                CreatedAt = now.AddDays(-8),
                UpdatedAt = now
            };
            await context.CustomerOrders.AddAsync(order1);
            await context.SaveChangesAsync();

            await context.OrderItems.AddRangeAsync(
                new OrderItem
                {
                    OrderId = order1.OrderId,
                    ProductId = prodChair.ProductId,
                    OrderedQty = 400,
                    FscCertifiedReq = true,
                    LengthMm = 450,
                    WidthMm = 520,
                    HeightMm = 850,
                    Status = OrderItemStatusEnum.READY_PROD
                },
                new OrderItem
                {
                    OrderId = order1.OrderId,
                    ProductId = prodTable.ProductId,
                    OrderedQty = 100,
                    FscCertifiedReq = true,
                    LengthMm = 1800,
                    WidthMm = 900,
                    HeightMm = 750,
                    Status = OrderItemStatusEnum.READY_PROD
                });

            await context.OrderSpaces.AddAsync(new OrderSpace
            {
                OrderId = order1.OrderId,
                SpaceKey = "SPACE-PO-880504",
                SpaceName = "SPACE-PO-880504 (Dining Sets - Ashley US)",
                DrawingFolderUrl = "/spaces/SPACE-PO-880504/drawings",
                OverallProgressPct = 62m,
                Status = SpaceStatusEnum.ACTIVE,
                CreatedAt = now.AddDays(-8),
                UpdatedAt = now
            });

            // Order 2: SPACE-PO-880506 (JYSK Nordic - 250 Pcs Oak Coffee Table)
            var order2 = new CustomerOrder
            {
                PoNumber = "JYSK-DK-880506",
                CustomerId = custJysk.CustomerId,
                CreatedBy = salesUser.UserId,
                OrderDate = now.Date.AddDays(-6),
                EtdDate = now.Date.AddDays(16),
                TotalCbm = 81.00m,
                Status = OrderStatusEnum.IN_PRODUCTION,
                CreatedAt = now.AddDays(-6),
                UpdatedAt = now
            };
            await context.CustomerOrders.AddAsync(order2);
            await context.SaveChangesAsync();

            await context.OrderItems.AddAsync(new OrderItem
            {
                OrderId = order2.OrderId,
                ProductId = prodCoffee.ProductId,
                OrderedQty = 250,
                FscCertifiedReq = true,
                LengthMm = 1200,
                WidthMm = 600,
                HeightMm = 450,
                Status = OrderItemStatusEnum.IN_PROD
            });

            await context.OrderSpaces.AddAsync(new OrderSpace
            {
                OrderId = order2.OrderId,
                SpaceKey = "SPACE-PO-880506",
                SpaceName = "SPACE-PO-880506 (Coffee Tables - JYSK DK)",
                DrawingFolderUrl = "/spaces/SPACE-PO-880506/drawings",
                OverallProgressPct = 45m,
                Status = SpaceStatusEnum.ACTIVE,
                CreatedAt = now.AddDays(-6),
                UpdatedAt = now
            });

            // Order 3: SPACE-PO-880507 (IKEA Supply AG - 300 Pcs Queen Bed Frame)
            var order3 = new CustomerOrder
            {
                PoNumber = "IKEA-AU-880507",
                CustomerId = custIkea.CustomerId,
                CreatedBy = salesUser.UserId,
                OrderDate = now.Date.AddDays(-3),
                EtdDate = now.Date.AddDays(31),
                TotalCbm = 255.00m,
                Status = OrderStatusEnum.IN_PLANNING,
                CreatedAt = now.AddDays(-3),
                UpdatedAt = now
            };
            await context.CustomerOrders.AddAsync(order3);
            await context.SaveChangesAsync();

            await context.OrderItems.AddAsync(new OrderItem
            {
                OrderId = order3.OrderId,
                ProductId = prodBed.ProductId,
                OrderedQty = 300,
                FscCertifiedReq = true,
                LengthMm = 2100,
                WidthMm = 1600,
                HeightMm = 1100,
                Status = OrderItemStatusEnum.WAITING_MAT
            });

            await context.OrderSpaces.AddAsync(new OrderSpace
            {
                OrderId = order3.OrderId,
                SpaceKey = "SPACE-PO-880507",
                SpaceName = "SPACE-PO-880507 (Bed Frames - IKEA AU)",
                DrawingFolderUrl = "/spaces/SPACE-PO-880507/drawings",
                OverallProgressPct = 0m,
                Status = SpaceStatusEnum.ACTIVE,
                CreatedAt = now.AddDays(-3),
                UpdatedAt = now
            });

            // Order 4: SPACE-PO-880512 (Nitori Holdings - 400 Pcs Solid Wood Stools)
            var order4 = new CustomerOrder
            {
                PoNumber = "NIT-JP-880512",
                CustomerId = custNitori.CustomerId,
                CreatedBy = salesUser.UserId,
                OrderDate = now.Date.AddDays(-1),
                EtdDate = now.Date.AddDays(51),
                TotalCbm = 26.00m,
                Status = OrderStatusEnum.CONFIRMED,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now
            };
            await context.CustomerOrders.AddAsync(order4);
            await context.SaveChangesAsync();

            await context.OrderItems.AddAsync(new OrderItem
            {
                OrderId = order4.OrderId,
                ProductId = prodStool.ProductId,
                OrderedQty = 400,
                FscCertifiedReq = true,
                LengthMm = 380,
                WidthMm = 380,
                HeightMm = 450,
                Status = OrderItemStatusEnum.PENDING_BOM
            });

            await context.OrderSpaces.AddAsync(new OrderSpace
            {
                OrderId = order4.OrderId,
                SpaceKey = "SPACE-PO-880512",
                SpaceName = "SPACE-PO-880512 (Wood Stools - Nitori JP)",
                DrawingFolderUrl = "/spaces/SPACE-PO-880512/drawings",
                OverallProgressPct = 0m,
                Status = SpaceStatusEnum.ACTIVE,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now
            });

            await context.SaveChangesAsync();
        }

        // 6. Seed Immutable System Audit Logs khớp với màn hình SCR-ADM-02
        if (!await context.AuditLogs.AnyAsync())
        {
            var ownerUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "owner");
            var salesUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "sales");
            var warehouseUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "warehouse");

            var sampleLogs = new List<AuditLog>
            {
                new()
                {
                    UserId = ownerUser?.UserId,
                    ActionType = "AUTHORIZE_OVERDRIVER",
                    EntityName = "Batch #LOT-880504-01",
                    EntityId = "LOT-880504-01",
                    BeforeState = "LOCKED_WAITING_MAT",
                    AfterState = "OVERDRIVER_RUNNING",
                    Reason = "Customer urgent ship request (ETD: Oct 28). Missing hinges. Approved rough milling up to Assembly Hold Point.",
                    IpAddress = "192.168.1.10",
                    CreatedAt = now.AddHours(-3)
                },
                new()
                {
                    UserId = salesUser?.UserId,
                    ActionType = "CONFIRM_PO_STAGING",
                    EntityName = "Order Space #SPACE-880507",
                    EntityId = "SPACE-PO-880507",
                    BeforeState = "STAGING_RAW",
                    AfterState = "NEWLY_RECEIVED",
                    Reason = "Cleaned 4 merged cell format errors. Converted inches to mm. Generated Order Space for 300 bed frames.",
                    IpAddress = "192.168.1.15",
                    CreatedAt = now.AddHours(-5)
                },
                new()
                {
                    UserId = ownerUser?.UserId,
                    ActionType = "APPROVE_SCRAP_REPLACEMENT",
                    EntityName = "Order #PO-880506",
                    EntityId = "JYSK-DK-880506",
                    BeforeState = "SCRAP_PENDING",
                    AfterState = "SUB_BATCH_ISSUED",
                    Reason = "QC reported 3 broken mortise parts at Assembly. Released replacement job for 3 table legs.",
                    IpAddress = "192.168.1.10",
                    CreatedAt = now.AddDays(-1).AddHours(-2)
                },
                new()
                {
                    UserId = warehouseUser?.UserId,
                    ActionType = "COMMIT_GOODS_RECEIPT",
                    EntityName = "PO-MAT-2026-042",
                    EntityId = "PO-MAT-2026-042",
                    BeforeState = "IN_TRANSIT",
                    AfterState = "RECEIVED_COMMITTED",
                    Reason = "Received 500 pcs Oak boards from Supplier Hoang Thong. Auto-allocated 320 pcs to Order 880504 by ETD priority.",
                    IpAddress = "192.168.1.22",
                    CreatedAt = now.AddDays(-1).AddHours(-5)
                },
                new()
                {
                    UserId = ownerUser?.UserId,
                    ActionType = "APPROVE_CONSOLIDATED_PR",
                    EntityName = "Daily PR-2026-10-19",
                    EntityId = "PR-2026-10-19",
                    BeforeState = "DRAFT_PR",
                    AfterState = "APPROVED_FOR_PURCHASING",
                    Reason = "Aggregated material deficits across 3 daily orders. Endorsed 1,200m board and 4,000 screws.",
                    IpAddress = "192.168.1.10",
                    CreatedAt = now.AddDays(-1).AddHours(-8)
                }
            };

            await context.AuditLogs.AddRangeAsync(sampleLogs);
            await context.SaveChangesAsync();
        }
    }
}
