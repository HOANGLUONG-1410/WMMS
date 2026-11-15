using Microsoft.EntityFrameworkCore;
using WMMS.Domain.Constants;
using WMMS.Domain.Entities.CatalogBom;
using WMMS.Domain.Entities.Identity;
using WMMS.Domain.Entities.InventoryPr;
using WMMS.Domain.Entities.KanbanQc;
using WMMS.Domain.Entities.Orders;
using WMMS.Domain.Entities.System;
using WMMS.Domain.Entities.WarehousePo;
using WMMS.Domain.Enums;

namespace WMMS.Infrastructure.Persistence.Seed;

public static class DbInitializer
{
    public static async Task SeedAsync(WmmsDbContext context)
    {
        var now = DateTime.UtcNow;

        // 1. Seed Roles
        if (!await context.Roles.AnyAsync())
        {
            var roles = new List<Role>
            {
                new() { RoleCode = RoleConstants.Admin, RoleName = "Quản trị viên Hệ thống", PermissionsJson = "{\"all\": true}" },
                new() { RoleCode = RoleConstants.Owner, RoleName = "Chủ xưởng / Ban giám đốc", PermissionsJson = "{\"view_all\": true, \"approve_orders\": true}" },
                new() { RoleCode = RoleConstants.Sales, RoleName = "Nhân viên Kinh doanh", PermissionsJson = "{\"manage_orders\": true, \"view_customers\": true}" },
                new() { RoleCode = RoleConstants.Planner, RoleName = "Kỹ sư Kế hoạch / Kỹ thuật BOM", PermissionsJson = "{\"manage_boms\": true, \"manage_batches\": true, \"manage_prs\": true}" },
                new() { RoleCode = RoleConstants.Purchasing, RoleName = "Chuyên viên Thu mua", PermissionsJson = "{\"manage_pos\": true, \"manage_suppliers\": true}" },
                new() { RoleCode = RoleConstants.Warehouse, RoleName = "Thủ kho", PermissionsJson = "{\"manage_inventory\": true, \"manage_grns\": true}" },
                new() { RoleCode = RoleConstants.Leader, RoleName = "Tổ trưởng / Quản đốc Sản xuất", PermissionsJson = "{\"manage_routing\": true, \"view_kanban\": true}" },
                new() { RoleCode = RoleConstants.Qc, RoleName = "Kiểm tra Chất lượng (KCS/QC)", PermissionsJson = "{\"manage_qc\": true, \"log_defects\": true}" }
            };

            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();
        }

        // 2. Seed Default Admin User
        if (!await context.Users.AnyAsync(u => u.Username == "admin"))
        {
            var adminRole = await context.Roles.FirstAsync(r => r.RoleCode == RoleConstants.Admin);

            var adminUser = new User
            {
                Username = "admin",
                Email = "admin@wmms.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                FullName = "Quản trị viên Hệ thống",
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

        // 2b. Seed Owner + sample staff users (UC-06), audit logs (UC-13/GBR-05)
        {
            var roleMap = await context.Roles.ToDictionaryAsync(r => r.RoleCode, r => r.RoleId);
            var staff = new (string Username, string Email, string FullName, string Role)[]
            {
                ("owner", "owner@nguyenkhoa.vn", "Nguyễn Khoa (Giám đốc Xưởng)", RoleConstants.Owner),
                ("sales01", "sales01@nguyenkhoa.vn", "Lê Minh Sales (Trưởng phòng KD)", RoleConstants.Sales),
                ("sales02", "sales02@nguyenkhoa.vn", "Phạm Thu Trang (Chuyên viên KD)", RoleConstants.Sales),
                ("planner01", "planner01@nguyenkhoa.vn", "Trần Văn Kế Hoạch (Kỹ sư BOM)", RoleConstants.Planner),
                ("purchasing01", "purchasing01@nguyenkhoa.vn", "Vũ Thị Thu Mua (Phụ trách vật tư)", RoleConstants.Purchasing),
                ("warehouse01", "warehouse01@nguyenkhoa.vn", "Đỗ Hữu Kho (Thủ kho chính)", RoleConstants.Warehouse),
                ("leader01", "leader01@nguyenkhoa.vn", "Ngô Văn Quản Đốc (Xưởng mộc)", RoleConstants.Leader),
                ("qc01", "qc01@nguyenkhoa.vn", "Hoàng Thị KCS (Kiểm tra chất lượng)", RoleConstants.Qc)
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
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await context.Users.AddAsync(u);
                await context.SaveChangesAsync();
                await context.UserRoles.AddAsync(new UserRole { UserId = u.UserId, RoleId = roleMap[s.Role], AssignedAt = now });
                await context.SaveChangesAsync();
            }

            var seedAdmin = await context.Users.FirstAsync(u => u.Username == "admin");
            var seedOwner = await context.Users.FirstAsync(u => u.Username == "owner");

            if (!await context.AuditLogs.AnyAsync())
            {
                await context.AuditLogs.AddRangeAsync(
                    new AuditLog { UserId = seedAdmin.UserId, ActionType = "AUTH_LOGIN_SUCCESS", EntityName = "User", EntityId = seedAdmin.UserId.ToString(), Reason = "Khởi tạo dữ liệu hệ thống", CreatedAt = now },
                    new AuditLog { UserId = seedAdmin.UserId, ActionType = "USER_CREATED", EntityName = "User", EntityId = seedOwner.UserId.ToString(), Reason = "Khởi tạo dữ liệu hệ thống", CreatedAt = now },
                    new AuditLog { UserId = seedOwner.UserId, ActionType = "AUTH_LOGIN_SUCCESS", EntityName = "User", EntityId = seedOwner.UserId.ToString(), Reason = "Khởi tạo dữ liệu hệ thống", CreatedAt = now });
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
                    DefectName = "Sai lệch kích thước phôi cưa xẻ",
                    StandardStage = "Pha phôi gỗ thô",
                    RootCause = "Lưỡi cưa mòn hoặc bàn cưa rung lắc cơ khí",
                    RecommendedAction = "Căn chỉnh lại dưỡng cưa và cắt xẻ lại chi tiết"
                },
                new()
                {
                    DefectCode = DefectCodeConstants.WoodErr01,
                    DefectName = "Khuyết tật mắt gỗ / Nứt tét phôi",
                    StandardStage = "Pha phôi gỗ thô",
                    RootCause = "Mắt chết tự nhiên hoặc nứt đầu tấm gỗ xẻ sấy",
                    RecommendedAction = "Cắt bỏ đoạn nứt, dán trám keo hoặc phân loại sang thanh phụ"
                },
                new()
                {
                    DefectCode = DefectCodeConstants.SurfErr02,
                    DefectName = "Bề mặt gỗ sần sùi / Xước vết nhám",
                    StandardStage = "Bào & Chà nhám",
                    RootCause = "Giấy nhám mòn hạt hoặc tốc độ đưa phôi không đều",
                    RecommendedAction = "Chà nhám lại bằng hạt nhám P240-P320 trước khi sơn"
                },
                new()
                {
                    DefectCode = DefectCodeConstants.AssyErr03,
                    DefectName = "Lệch mộng kết cấu / Lắp ráp kênh",
                    StandardStage = "Lắp ráp kết cấu",
                    RootCause = "Khoan lệch tâm lỗ mộng hoặc độ ẩm gỗ co ngót",
                    RecommendedAction = "Tháo dỡ, căn chỉnh chốt gỗ và ép vam keo đồng phẳng"
                },
                new()
                {
                    DefectCode = DefectCodeConstants.PaintErr04,
                    DefectName = "Bọt khí / Chảy màng sơn PU",
                    StandardStage = "Sơn lót & Sơn phủ",
                    RootCause = "Áp suất súng phun quá lớn hoặc bụi sơn bám bề mặt",
                    RecommendedAction = "Xả nhám cục bộ và dặm lại lớp sơn phủ bóng mờ"
                }
            };

            await context.DefectCatalogs.AddRangeAsync(defects);
            await context.SaveChangesAsync();
        }

        // 4. Seed Default System Configs
        if (!await context.SystemConfigs.AnyAsync())
        {
            var configs = new List<SystemConfig>
            {
                new()
                {
                    ConfigKey = SystemConfigKeys.DefaultScrapMin,
                    ConfigValue = "3.00",
                    DataType = "DECIMAL",
                    Description = "Tỷ lệ hao hụt tự nhiên mặc định tối thiểu (3%)",
                    UpdatedAt = now
                },
                new()
                {
                    ConfigKey = SystemConfigKeys.DefaultScrapMax,
                    ConfigValue = "5.00",
                    DataType = "DECIMAL",
                    Description = "Tỷ lệ hao hụt tự nhiên mặc định tối đa (5%)",
                    UpdatedAt = now
                },
                new()
                {
                    ConfigKey = SystemConfigKeys.EtdWarningDays,
                    ConfigValue = "7",
                    DataType = "INT",
                    Description = "Ngưỡng cảnh báo trước số ngày so với hạn giao hàng ETD (7 ngày)",
                    UpdatedAt = now
                }
            };

            await context.SystemConfigs.AddRangeAsync(configs);
            await context.SaveChangesAsync();
        }

        // 5. Seed 6 Suppliers (theo WMMS_DB_Test_Iteration1.sql)
        if (!await context.Suppliers.AnyAsync())
        {
            var suppliers = new List<Supplier>
            {
                new() { SupplierCode = "SUP-WOOD-LAMVIET", SupplierName = "Công ty CP Chế biến Gỗ Lâm Việt", SupplyCategory = "LUMBER", ContactEmail = "sales@lamvietwood.vn", Phone = "02743789123", LeadTimeDays = 10, Status = "ACTIVE", CreatedAt = now, UpdatedAt = now },
                new() { SupplierCode = "SUP-WOOD-TAVICO", SupplierName = "Công ty CP Gỗ Tây Ngô Tavico Timber", SupplyCategory = "LUMBER", ContactEmail = "info@tavicowood.com", Phone = "02838992233", LeadTimeDays = 14, Status = "ACTIVE", CreatedAt = now, UpdatedAt = now },
                new() { SupplierCode = "SUP-HARDWARE-HAFELE", SupplierName = "Công ty TNHH Hafele Việt Nam", SupplyCategory = "HARDWARE", ContactEmail = "support.vn@hafele.com", Phone = "02839113111", LeadTimeDays = 5, Status = "ACTIVE", CreatedAt = now, UpdatedAt = now },
                new() { SupplierCode = "SUP-HARDWARE-BLUM", SupplierName = "Công ty TNHH Phụ kiện Mộc Blum", SupplyCategory = "HARDWARE", ContactEmail = "sales@blum.vn", Phone = "02838224455", LeadTimeDays = 7, Status = "ACTIVE", CreatedAt = now, UpdatedAt = now },
                new() { SupplierCode = "SUP-PAINT-OSEVEN", SupplierName = "Công ty Cổ phần Sơn Oseven PU", SupplyCategory = "FINISH_PAINT", ContactEmail = "kinhdoanh@osevenpaint.com", Phone = "02837651234", LeadTimeDays = 3, Status = "ACTIVE", CreatedAt = now, UpdatedAt = now },
                new() { SupplierCode = "SUP-PACK-THANGLONG", SupplierName = "Bao bì & Thùng Carton Thăng Long", SupplyCategory = "PACKAGING", ContactEmail = "sales@thanglongbox.com.vn", Phone = "02438556677", LeadTimeDays = 4, Status = "ACTIVE", CreatedAt = now, UpdatedAt = now }
            };
            await context.Suppliers.AddRangeAsync(suppliers);
            await context.SaveChangesAsync();
        }

        // 6. Seed 14 Materials (khớp 100% WMMS_DB_Test_Iteration1.sql: 4 LUMBER, 5 HARDWARE, 3 FINISH_PAINT, 2 PACKAGING)
        if (!await context.Materials.AnyAsync())
        {
            var materials = new List<Material>
            {
                // LUMBER (4)
                new() { MaterialCode = "WOOD_OAK_01", MaterialName = "Gỗ sồi Bắc Mỹ xẻ sấy FAS (Oak Timber)", MaterialGroup = MaterialGroupEnum.LUMBER, UnitOfMeasure = "M3", DimensionSpec = "26mm x 150mm x 2400mm", FscCertified = true, SafetyStockQty = 15.0000m, Status = "ACTIVE", CreatedAt = now, UpdatedAt = now },
                new() { MaterialCode = "WOOD_ASH_02", MaterialName = "Gỗ tần bì nhập khẩu sấy đạt chuẩn (Ash Timber)", MaterialGroup = MaterialGroupEnum.LUMBER, UnitOfMeasure = "M3", DimensionSpec = "25mm x 120mm x 2200mm", FscCertified = true, SafetyStockQty = 10.0000m, Status = "ACTIVE", CreatedAt = now, UpdatedAt = now },
                new() { MaterialCode = "WOOD_RUBBER_03", MaterialName = "Gỗ cao su ghép thanh tiêu chuẩn AB", MaterialGroup = MaterialGroupEnum.LUMBER, UnitOfMeasure = "M3", DimensionSpec = "20mm x 1200mm x 2400mm", FscCertified = true, SafetyStockQty = 20.0000m, Status = "ACTIVE", CreatedAt = now, UpdatedAt = now },
                new() { MaterialCode = "WOOD_PLY_04", MaterialName = "Gỗ dán Plywood cốt Birch phủ Veneer", MaterialGroup = MaterialGroupEnum.LUMBER, UnitOfMeasure = "M3", DimensionSpec = "18mm x 1220mm x 2440mm", FscCertified = true, SafetyStockQty = 10.0000m, Status = "ACTIVE", CreatedAt = now, UpdatedAt = now },
                // HARDWARE (5)
                new() { MaterialCode = "HW_SCREW_M4", MaterialName = "Vít bắt gỗ tự khoan mạ kẽm M4x30", MaterialGroup = MaterialGroupEnum.HARDWARE, UnitOfMeasure = "PCS", DimensionSpec = "M4 x 30mm", FscCertified = false, SafetyStockQty = 5000.0000m, Status = "ACTIVE", CreatedAt = now, UpdatedAt = now },
                new() { MaterialCode = "HW_SCREW_M5", MaterialName = "Vít liên kết mộng giàn chân chìm M5x50", MaterialGroup = MaterialGroupEnum.HARDWARE, UnitOfMeasure = "PCS", DimensionSpec = "M5 x 50mm", FscCertified = false, SafetyStockQty = 3000.0000m, Status = "ACTIVE", CreatedAt = now, UpdatedAt = now },
                new() { MaterialCode = "HW_HINGE_01", MaterialName = "Bản lề giảm chấn Hafele SUS304 trùm nửa", MaterialGroup = MaterialGroupEnum.HARDWARE, UnitOfMeasure = "PCS", DimensionSpec = "Góc mở 110 độ Hafele", FscCertified = false, SafetyStockQty = 800.0000m, Status = "ACTIVE", CreatedAt = now, UpdatedAt = now },
                new() { MaterialCode = "HW_HANDLE_01", MaterialName = "Tay nắm hợp kim nhôm xước mờ cao cấp", MaterialGroup = MaterialGroupEnum.HARDWARE, UnitOfMeasure = "PCS", DimensionSpec = "Tâm lỗ 96mm x Dài 120mm", FscCertified = false, SafetyStockQty = 400.0000m, Status = "ACTIVE", CreatedAt = now, UpdatedAt = now },
                new() { MaterialCode = "HW_SLIDE_450", MaterialName = "Ray trượt bi 3 tầng giảm chấn 450mm", MaterialGroup = MaterialGroupEnum.HARDWARE, UnitOfMeasure = "PCS", DimensionSpec = "Bản rộng 45mm x Dài 450mm", FscCertified = false, SafetyStockQty = 300.0000m, Status = "ACTIVE", CreatedAt = now, UpdatedAt = now },
                // FINISH_PAINT (3)
                new() { MaterialCode = "PAINT_SEALER_01", MaterialName = "Sơn lót PU 2K kháng ẩm gốc nước", MaterialGroup = MaterialGroupEnum.FINISH_PAINT, UnitOfMeasure = "LITER", DimensionSpec = "Thùng tiêu chuẩn 20L", FscCertified = false, SafetyStockQty = 150.0000m, Status = "ACTIVE", CreatedAt = now, UpdatedAt = now },
                new() { MaterialCode = "PAINT_TOP_MATT", MaterialName = "Sơn phủ bóng mờ PU 50% chống trầy xước", MaterialGroup = MaterialGroupEnum.FINISH_PAINT, UnitOfMeasure = "LITER", DimensionSpec = "Thùng tiêu chuẩn 20L", FscCertified = false, SafetyStockQty = 120.0000m, Status = "ACTIVE", CreatedAt = now, UpdatedAt = now },
                new() { MaterialCode = "GLUE_WOOD_T3", MaterialName = "Keo dán mộng gỗ công nghiệp Titebond II", MaterialGroup = MaterialGroupEnum.FINISH_PAINT, UnitOfMeasure = "LITER", DimensionSpec = "Can công nghiệp 5 Gallon", FscCertified = false, SafetyStockQty = 50.0000m, Status = "ACTIVE", CreatedAt = now, UpdatedAt = now },
                // PACKAGING (2)
                new() { MaterialCode = "BOX_CARTON_01", MaterialName = "Thùng carton 5 lớp xuất khẩu Master Case", MaterialGroup = MaterialGroupEnum.PACKAGING, UnitOfMeasure = "PCS", DimensionSpec = "600mm x 550mm x 950mm", FscCertified = true, SafetyStockQty = 600.0000m, Status = "ACTIVE", CreatedAt = now, UpdatedAt = now },
                new() { MaterialCode = "FOAM_PE_01", MaterialName = "Màng xốp PE Foam 2mm bọc chống sốc bề mặt", MaterialGroup = MaterialGroupEnum.PACKAGING, UnitOfMeasure = "M2", DimensionSpec = "Cuộn 1.05m x 150m", FscCertified = false, SafetyStockQty = 500.0000m, Status = "ACTIVE", CreatedAt = now, UpdatedAt = now }
            };

            await context.Materials.AddRangeAsync(materials);
            await context.SaveChangesAsync();
        }

        // 7. Seed 4 Visual Catalog Products & Default Routing
        if (!await context.Products.AnyAsync())
        {
            var products = new List<Product>
            {
                new()
                {
                    ProductSku = "CHAIR-DINING-OAK-01",
                    ProductName = "Ghế ăn tựa nan gỗ sồi Bắc Mỹ",
                    CategoryName = "Ghế ăn",
                    DimensionsCm = "45 x 52 x 88 cm",
                    WoodSpecies = "Gỗ sồi trắng Bắc Mỹ",
                    FinishColor = "Màu gỗ óc chó tự nhiên",
                    PackingSpec = "2 chiếc / Thùng Master",
                    CbmPerUnit = 0.1200m,
                    ImageUrl = "/images/products/chair-oak-01.jpg",
                    CadDrawingUrl = "/drawings/cad/chair-oak-01.dwg",
                    Status = "ACTIVE",
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new()
                {
                    ProductSku = "TABLE-DINING-EXT-02",
                    ProductName = "Bàn ăn thông minh kéo dài gỗ sồi",
                    CategoryName = "Bàn ăn",
                    DimensionsCm = "160/210 x 90 x 75 cm",
                    WoodSpecies = "Gỗ sồi trắng Bắc Mỹ",
                    FinishColor = "Màu gỗ sồi khói",
                    PackingSpec = "1 chiếc / 2 Thùng carton",
                    CbmPerUnit = 0.3500m,
                    ImageUrl = "/images/products/table-ext-02.jpg",
                    CadDrawingUrl = "/drawings/cad/table-ext-02.dwg",
                    Status = "ACTIVE",
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new()
                {
                    ProductSku = "CABINET-SIDEBOARD-03",
                    ProductName = "Tủ sideboard 3 cánh 2 hộc kéo",
                    CategoryName = "Tủ trang trí",
                    DimensionsCm = "150 x 42 x 82 cm",
                    WoodSpecies = "Gỗ cao su + Veneer sồi",
                    FinishColor = "Màu hạt dẻ ấm",
                    PackingSpec = "1 chiếc / Thùng kèm xốp bảo vệ",
                    CbmPerUnit = 0.5800m,
                    ImageUrl = "/images/products/sideboard-03.jpg",
                    CadDrawingUrl = "/drawings/cad/sideboard-03.dwg",
                    Status = "ACTIVE",
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new()
                {
                    ProductSku = "DESK-WORK-MODERN-04",
                    ProductName = "Bàn làm việc tối giản chân tiện gỗ tần bì",
                    CategoryName = "Bàn làm việc",
                    DimensionsCm = "120 x 60 x 75 cm",
                    WoodSpecies = "Gỗ tần bì nhập khẩu",
                    FinishColor = "Màu gỗ sồi tự nhiên",
                    PackingSpec = "1 chiếc / Thùng lắp ráp phẳng (Flat-pack)",
                    CbmPerUnit = 0.2200m,
                    ImageUrl = "/images/products/desk-modern-04.jpg",
                    CadDrawingUrl = "/drawings/cad/desk-modern-04.dwg",
                    Status = "ACTIVE",
                    CreatedAt = now,
                    UpdatedAt = now
                }
            };

            await context.Products.AddRangeAsync(products);
            await context.SaveChangesAsync();

            foreach (var p in products)
            {
                var routing = new RoutingTemplate
                {
                    ProductId = p.ProductId,
                    VersionCode = "v1.0",
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                await context.RoutingTemplates.AddAsync(routing);
                await context.SaveChangesAsync();

                var stages = new List<RoutingStage>
                {
                    new() { RoutingId = routing.RoutingId, StageSequence = 1, StageName = "1. Pha phôi gỗ thô", IsHoldPoint = false },
                    new() { RoutingId = routing.RoutingId, StageSequence = 2, StageName = "2. Cắt/Xẻ định hình chi tiết", IsHoldPoint = false },
                    new() { RoutingId = routing.RoutingId, StageSequence = 3, StageName = "3. Bào 4 mặt & Chà nhám phẳng mịn", IsHoldPoint = false },
                    new() { RoutingId = routing.RoutingId, StageSequence = 4, StageName = "4. Khoan lỗ chốt & Phay mộng âm dương", IsHoldPoint = false },
                    new() { RoutingId = routing.RoutingId, StageSequence = 5, StageName = "5. Lắp ráp kết cấu & Vào mộng dán keo ép vam", IsHoldPoint = true },
                    new() { RoutingId = routing.RoutingId, StageSequence = 6, StageName = "6. Phun sơn lót PU & Sơn phủ hoàn thiện", IsHoldPoint = false },
                    new() { RoutingId = routing.RoutingId, StageSequence = 7, StageName = "7. Kiểm tra xuất xưởng KCS & Đóng thùng carton", IsHoldPoint = false }
                };

                await context.RoutingStages.AddRangeAsync(stages);
                await context.SaveChangesAsync();
            }
        }

        // 8. Seed B2B Customers & Sales Orders / Order Spaces
        if (!await context.OrderSpaces.AnyAsync())
        {
            var oAdmin = await context.Users.FirstAsync(u => u.Username == "admin");
            var oCreator = await context.Users.FirstOrDefaultAsync(u => u.Username == "sales01") ?? oAdmin;
            var oProducts = await context.Products.OrderBy(p => p.ProductId).ToListAsync();
            if (oProducts.Count > 0)
            {
                if (!await context.Customers.AnyAsync())
                {
                    await context.Customers.AddRangeAsync(
                        new Customer { CustomerCode = "CUST-ASHLEY-US", CompanyName = "Ashley Furniture HomeStore", Country = "Hoa Kỳ", ContactPerson = "Michael Vance (Giám đốc Thu mua)", Email = "procurement@ashleyfurniture.com", CreatedAt = now, UpdatedAt = now },
                        new Customer { CustomerCode = "CUST-IKEA-SE", CompanyName = "IKEA Supply AG (Tập đoàn Nội thất Toàn cầu)", Country = "Thụy Điển", ContactPerson = "Johan Lindqvist (Quản lý Ngành hàng)", Email = "orders.apac@ikea.com", CreatedAt = now, UpdatedAt = now },
                        new Customer { CustomerCode = "CUST-NITORI-JP", CompanyName = "Nitori Co., Ltd (Khối Nội thất)", Country = "Nhật Bản", ContactPerson = "Kenji Takahashi (Giám sát Chất lượng)", Email = "import@nitori.co.jp", CreatedAt = now, UpdatedAt = now },
                        new Customer { CustomerCode = "CUST-WAYFAIR-US", CompanyName = "Wayfair E-Commerce Home Goods", Country = "Hoa Kỳ", ContactPerson = "Sarah Jenkins (Trưởng nhóm Chuỗi cung ứng)", Email = "vendor@wayfair.com", CreatedAt = now, UpdatedAt = now },
                        new Customer { CustomerCode = "CUST-HOAPHAT-VN", CompanyName = "Tập đoàn Nội Thất Hòa Phát", Country = "Việt Nam", ContactPerson = "Nguyễn Văn Tuấn (Trưởng ban Cung ứng)", Email = "cungung@hoaphatfurniture.vn", CreatedAt = now, UpdatedAt = now }
                    );
                    await context.SaveChangesAsync();
                }

                var oCustomer = await context.Customers.FirstAsync(c => c.CustomerCode == "CUST-ASHLEY-US");
                var orderSeeds = new (string Po, string Key, int Qty, Product Prod, int Days)[]
                {
                    ("ASH-2026-OCT-880504", "SPACE-PO-880504", 500, oProducts[0], 18),
                    ("ASH-2026-OCT-880505", "SPACE-PO-880505", 350, oProducts[1 % oProducts.Count], 22),
                    ("IKE-2026-OCT-990102", "SPACE-PO-990102", 300, oProducts[2 % oProducts.Count], 25),
                    ("NIT-2026-OCT-770301", "SPACE-PO-770301", 300, oProducts[0], 30),
                    ("HOA-2026-OCT-110205", "SPACE-PO-110205", 200, oProducts[3 % oProducts.Count], 35)
                };
                foreach (var os in orderSeeds)
                {
                    var order = new CustomerOrder
                    {
                        PoNumber = os.Po,
                        CustomerId = oCustomer.CustomerId,
                        CreatedBy = oCreator.UserId,
                        OrderDate = now.Date.AddDays(-3),
                        EtdDate = now.Date.AddDays(os.Days),
                        TotalCbm = Math.Round((os.Prod.CbmPerUnit ?? 0.15m) * os.Qty, 4),
                        Status = OrderStatusEnum.CONFIRMED,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    await context.CustomerOrders.AddAsync(order);
                    await context.SaveChangesAsync();

                    await context.OrderItems.AddAsync(new OrderItem
                    {
                        OrderId = order.OrderId,
                        ProductId = os.Prod.ProductId,
                        OrderedQty = os.Qty,
                        UnitPriceUsd = 48.50m,
                        FscCertifiedReq = true,
                        LengthMm = 450,
                        WidthMm = 520,
                        HeightMm = 880,
                        Status = OrderItemStatusEnum.PENDING_BOM
                    });

                    await context.OrderSpaces.AddAsync(new OrderSpace
                    {
                        OrderId = order.OrderId,
                        SpaceKey = os.Key,
                        SpaceName = $"{os.Key} ({os.Prod.ProductName} - {oCustomer.CompanyName})",
                        DrawingFolderUrl = $"/spaces/{os.Key}/drawings",
                        OverallProgressPct = 0m,
                        Status = SpaceStatusEnum.ACTIVE,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                    await context.SaveChangesAsync();
                }
            }
        }

        // 9. Seed 14 Inventory Stocks & 14 Initial Ledger Transactions (SC-12, SC-13 theo WMMS_DB_Test_Iteration1.sql)
        if (!await context.MaterialStocks.AnyAsync())
        {
            var matMap = await context.Materials.ToDictionaryAsync(m => m.MaterialCode, m => m.MaterialId);
            var stockSeeds = new (string Code, decimal Physical, decimal Allocated, decimal Available)[]
            {
                ("WOOD_OAK_01", 60.0000m, 13.0000m, 47.0000m),
                ("WOOD_ASH_02", 45.0000m, 0.0000m, 45.0000m),
                ("WOOD_RUBBER_03", 80.0000m, 0.0000m, 80.0000m),
                ("WOOD_PLY_04", 50.0000m, 0.0000m, 50.0000m),
                ("HW_SCREW_M4", 50000.0000m, 6240.0000m, 43760.0000m),
                ("HW_SCREW_M5", 30000.0000m, 8694.0000m, 21306.0000m),
                ("HW_HINGE_01", 5000.0000m, 0.0000m, 5000.0000m),
                ("HW_HANDLE_01", 3000.0000m, 0.0000m, 3000.0000m),
                ("HW_SLIDE_450", 2000.0000m, 362.2500m, 1637.7500m),
                ("PAINT_SEALER_01", 800.0000m, 240.9900m, 559.0100m),
                ("PAINT_TOP_MATT", 600.0000m, 160.6800m, 439.3200m),
                ("GLUE_WOOD_T3", 300.0000m, 26.0000m, 274.0000m),
                ("BOX_CARTON_01", 4000.0000m, 984.5000m, 3015.5000m),
                ("FOAM_PE_01", 2000.0000m, 624.0000m, 1376.0000m)
            };

            foreach (var st in stockSeeds)
            {
                if (!matMap.TryGetValue(st.Code, out var mId)) continue;

                await context.MaterialStocks.AddAsync(new MaterialStock
                {
                    MaterialId = mId,
                    PhysicalQty = st.Physical,
                    AllocatedQty = st.Allocated,
                    AvailableQty = st.Available,
                    UpdatedAt = now
                });

                await context.InventoryTransactions.AddAsync(new InventoryTransaction
                {
                    MaterialId = mId,
                    TxType = InventoryTransactionTypeEnum.INITIAL_IMPORT,
                    Quantity = st.Physical,
                    ReferenceCode = "INIT-STOCK-2026-OCT-01",
                    CreatedAt = now.AddDays(-5)
                });
            }

            await context.SaveChangesAsync();
        }

        // 10. Seed Initial Material Allocations for Order 1 & Order 2 (SC-12 Detail)
        if (!await context.MaterialAllocations.AnyAsync())
        {
            var matMap = await context.Materials.ToDictionaryAsync(m => m.MaterialCode, m => m.MaterialId);
            var orderItems = await context.OrderItems
                .Include(oi => oi.CustomerOrder)
                .OrderBy(oi => oi.OrderItemId)
                .ToListAsync();

            var order1Item = orderItems.FirstOrDefault(oi => oi.CustomerOrder.PoNumber == "ASH-2026-OCT-880504");
            var order2Item = orderItems.FirstOrDefault(oi => oi.CustomerOrder.PoNumber == "ASH-2026-OCT-880505");

            if (order1Item != null && matMap.TryGetValue("WOOD_OAK_01", out var oakId))
            {
                await context.MaterialAllocations.AddAsync(new MaterialAllocation
                {
                    OrderItemId = order1Item.OrderItemId,
                    MaterialId = oakId,
                    RequiredQty = 13.0000m,
                    ReservedQty = 13.0000m,
                    IssuedQty = 0m,
                    AllocationStatus = AllocationStatusEnum.RESERVED,
                    AllocatedAt = now.AddDays(-3)
                });
            }

            if (order1Item != null && matMap.TryGetValue("HW_SCREW_M4", out var screwM4Id))
            {
                await context.MaterialAllocations.AddAsync(new MaterialAllocation
                {
                    OrderItemId = order1Item.OrderItemId,
                    MaterialId = screwM4Id,
                    RequiredQty = 6240.0000m,
                    ReservedQty = 6240.0000m,
                    IssuedQty = 0m,
                    AllocationStatus = AllocationStatusEnum.RESERVED,
                    AllocatedAt = now.AddDays(-3)
                });
            }

            if (order2Item != null && matMap.TryGetValue("HW_SCREW_M5", out var screwM5Id))
            {
                await context.MaterialAllocations.AddAsync(new MaterialAllocation
                {
                    OrderItemId = order2Item.OrderItemId,
                    MaterialId = screwM5Id,
                    RequiredQty = 8694.0000m,
                    ReservedQty = 8694.0000m,
                    IssuedQty = 0m,
                    AllocationStatus = AllocationStatusEnum.RESERVED,
                    AllocatedAt = now.AddDays(-2)
                });
            }

            await context.SaveChangesAsync();
        }
    }
}
