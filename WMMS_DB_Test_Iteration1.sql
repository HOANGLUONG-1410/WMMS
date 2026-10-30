/* ==============================================================================================
   HỆ THỐNG QUẢN LÝ SẢN XUẤT NỘI THẤT GỖ XUẤT KHẨU - WMMS (WOODWORKING MANUFACTURING MANAGEMENT SYSTEM)
   ĐỒ ÁN TỐT NGHIỆP SEP490 - NHÓM G122
   
   FILE: WMMS_DB_Test_Iteration1.sql
   MỤC ĐÍCH: Khởi tạo toàn diện CSDL và Nạp bộ dữ liệu kiểm thử (Master Case Test Data) phục vụ DEMO ITERATION 1:
     1. Module AUT & ADM (SC-01, SC-02, SC-03): 8 Vai trò RBAC, 14 Tài khoản người dùng, Phân quyền & Audit Log.
     2. Module Master Data (SC-04, SC-05, SC-06, SC-07): 14 Vật tư gỗ/phụ kiện/sơn, 4 SKU Thành phẩm, 6 Nhà cung cấp, 5 Khách hàng B2B, 5 Mã lỗi QC.
     3. Module Order Intake (SC-08, SC-09): 5 Đơn hàng B2B Master Case, 5 Không gian đơn hàng (Order Space), Đơn hàng mẫu Staging Area.
     4. Module BOM Management (SC-10, SC-11): Định mức kỹ thuật BOM đa cấp (Approved & Pending Approval), Chi tiết bóc tách vật tư.
     5. Module Inventory (SC-12, SC-13): Quản lý tồn kho 3 cấp (Physical, Allocated, Available), Sổ kho nhập tồn đầu kỳ (Stock Ledger).
   
   TÀI KHOẢN ĐĂNG NHẬP MẶC ĐỊNH (Mật khẩu chung cho tất cả tài khoản: Admin@123):
     - admin / Admin@123         : System Administrator (Quản trị hệ thống, phân quyền)
     - owner / Admin@123         : Factory Owner / Executive (Ban giám đốc xưởng mộc)
     - sales01 / Admin@123       : Sales Staff (Kinh doanh, tiếp nhận đơn hàng, khách hàng)
     - planner01 / Admin@123     : Production Planner (Lập kế hoạch, thiết kế và duyệt định mức BOM)
     - purchasing01 / Admin@123  : Purchasing Officer (Phụ trách mua sắm, quản lý nhà cung cấp)
     - warehouse01 / Admin@123   : Warehouse Keeper (Thủ kho, theo dõi tồn kho 3 cấp, nhập tồn đầu kỳ)
     - leader01 / Admin@123      : Production Team Leader (Quản đốc xưởng, quy trình công nghệ Routing)
     - qc01 / Admin@123          : QC Inspector (Kiểm toán chất lượng, danh mục lỗi khuyết tật gỗ)
     - Tài khoản các thành viên: luonghd, duynd, duongnt, cuonghd, anhth (Pass: Admin@123)
============================================================================================== */

-- 1. TẠO HOẶC CHỌN DATABASE WMMS_DB
IF DB_ID('WMMS_DB') IS NULL
BEGIN
    PRINT N'-- Đang khởi tạo Database WMMS_DB...';
    CREATE DATABASE WMMS_DB COLLATE Latin1_General_100_CI_AI_SC_UTF8;
END
GO

USE WMMS_DB;
GO

SET NOCOUNT ON;
GO

PRINT N'==============================================================================================';
PRINT N'== BẮT ĐẦU KHỞI TẠO CẤU TRÚC BẢNG (DDL) CHO HỆ THỐNG WMMS                                   ==';
PRINT N'==============================================================================================';

-- 2. TẠO CÁC BẢNG NỀN TẢNG (NẾU CHƯA CÓ)
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

IF OBJECT_ID(N'[roles]') IS NULL
BEGIN
    CREATE TABLE [roles] (
        [role_id] int NOT NULL IDENTITY(1,1),
        [role_code] nvarchar(50) NOT NULL,
        [role_name] nvarchar(100) NOT NULL,
        [permissions_json] nvarchar(max) NOT NULL,
        [description] nvarchar(500) NULL,
        CONSTRAINT [PK_roles] PRIMARY KEY ([role_id]),
        CONSTRAINT [UQ_roles_role_code] UNIQUE ([role_code])
    );
END;
GO

IF OBJECT_ID(N'[users]') IS NULL
BEGIN
    CREATE TABLE [users] (
        [user_id] bigint NOT NULL IDENTITY(1,1),
        [username] nvarchar(100) NOT NULL,
        [email] nvarchar(255) NOT NULL,
        [password_hash] nvarchar(500) NOT NULL,
        [full_name] nvarchar(255) NOT NULL,
        [phone] nvarchar(20) NULL,
        [status] nvarchar(50) NOT NULL,
        [failed_login_count] int NOT NULL CONSTRAINT [DF_users_failed_login_count] DEFAULT 0,
        [locked_until] datetime2 NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        [last_login_at] datetime2 NULL,
        CONSTRAINT [PK_users] PRIMARY KEY ([user_id]),
        CONSTRAINT [UQ_users_username] UNIQUE ([username]),
        CONSTRAINT [UQ_users_email] UNIQUE ([email])
    );
END;
GO

IF OBJECT_ID(N'[user_roles]') IS NULL
BEGIN
    CREATE TABLE [user_roles] (
        [user_id] bigint NOT NULL,
        [role_id] int NOT NULL,
        [assigned_at] datetime2 NOT NULL,
        CONSTRAINT [PK_user_roles] PRIMARY KEY ([user_id], [role_id]),
        CONSTRAINT [FK_user_roles_users] FOREIGN KEY ([user_id]) REFERENCES [users] ([user_id]) ON DELETE CASCADE,
        CONSTRAINT [FK_user_roles_roles] FOREIGN KEY ([role_id]) REFERENCES [roles] ([role_id]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[system_configs]') IS NULL
BEGIN
    CREATE TABLE [system_configs] (
        [config_id] bigint NOT NULL IDENTITY(1,1),
        [config_key] nvarchar(100) NOT NULL,
        [config_value] nvarchar(1000) NOT NULL,
        [data_type] nvarchar(50) NULL,
        [description] nvarchar(500) NULL,
        [updated_at] datetime2 NOT NULL,
        CONSTRAINT [PK_system_configs] PRIMARY KEY ([config_id]),
        CONSTRAINT [UQ_system_configs_config_key] UNIQUE ([config_key])
    );
END;
GO

IF OBJECT_ID(N'[defect_catalogs]') IS NULL
BEGIN
    CREATE TABLE [defect_catalogs] (
        [defect_code] nvarchar(50) NOT NULL,
        [defect_name] nvarchar(255) NOT NULL,
        [standard_stage] nvarchar(100) NULL,
        [root_cause] nvarchar(500) NULL,
        [recommended_action] nvarchar(500) NULL,
        CONSTRAINT [PK_defect_catalogs] PRIMARY KEY ([defect_code])
    );
END;
GO

IF OBJECT_ID(N'[customers]') IS NULL
BEGIN
    CREATE TABLE [customers] (
        [customer_id] bigint NOT NULL IDENTITY(1,1),
        [customer_code] nvarchar(50) NOT NULL,
        [company_name] nvarchar(255) NOT NULL,
        [contact_person] nvarchar(255) NULL,
        [email] nvarchar(255) NULL,
        [country] nvarchar(100) NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_customers] PRIMARY KEY ([customer_id]),
        CONSTRAINT [UQ_customers_customer_code] UNIQUE ([customer_code])
    );
END;
GO

IF OBJECT_ID(N'[suppliers]') IS NULL
BEGIN
    CREATE TABLE [suppliers] (
        [supplier_id] bigint NOT NULL IDENTITY(1,1),
        [supplier_code] nvarchar(50) NOT NULL,
        [supplier_name] nvarchar(255) NOT NULL,
        [supply_category] nvarchar(100) NOT NULL,
        [contact_email] nvarchar(100) NULL,
        [phone] nvarchar(20) NULL,
        [lead_time_days] int NOT NULL CONSTRAINT [DF_suppliers_lead_time_days] DEFAULT 7,
        [status] nvarchar(20) NOT NULL CONSTRAINT [DF_suppliers_status] DEFAULT N'ACTIVE',
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_suppliers] PRIMARY KEY ([supplier_id]),
        CONSTRAINT [UQ_suppliers_supplier_code] UNIQUE ([supplier_code])
    );
END;
GO

IF OBJECT_ID(N'[materials]') IS NULL
BEGIN
    CREATE TABLE [materials] (
        [material_id] bigint NOT NULL IDENTITY(1,1),
        [material_code] nvarchar(100) NOT NULL,
        [material_name] nvarchar(255) NOT NULL,
        [material_group] nvarchar(50) NOT NULL,
        [unit_of_measure] nvarchar(20) NOT NULL,
        [dimension_spec] nvarchar(150) NULL,
        [fsc_certified] bit NOT NULL CONSTRAINT [DF_materials_fsc_certified] DEFAULT 0,
        [safety_stock_qty] decimal(18,4) NOT NULL CONSTRAINT [DF_materials_safety_stock_qty] DEFAULT 0,
        [status] nvarchar(50) NOT NULL CONSTRAINT [DF_materials_status] DEFAULT N'ACTIVE',
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_materials] PRIMARY KEY ([material_id]),
        CONSTRAINT [UQ_materials_material_code] UNIQUE ([material_code])
    );
END;
GO

IF OBJECT_ID(N'[products]') IS NULL
BEGIN
    CREATE TABLE [products] (
        [product_id] bigint NOT NULL IDENTITY(1,1),
        [product_sku] nvarchar(100) NOT NULL,
        [product_name] nvarchar(255) NOT NULL,
        [category_name] nvarchar(100) NOT NULL,
        [dimensions_cm] nvarchar(100) NOT NULL,
        [wood_species] nvarchar(100) NULL,
        [finish_color] nvarchar(100) NULL,
        [packing_spec] nvarchar(150) NULL,
        [cbm_per_unit] decimal(18,4) NULL,
        [image_url] nvarchar(500) NULL,
        [cad_drawing_url] nvarchar(500) NULL,
        [status] nvarchar(50) NOT NULL CONSTRAINT [DF_products_status] DEFAULT N'ACTIVE',
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_products] PRIMARY KEY ([product_id]),
        CONSTRAINT [UQ_products_product_sku] UNIQUE ([product_sku])
    );
END;
GO

IF OBJECT_ID(N'[routing_templates]') IS NULL
BEGIN
    CREATE TABLE [routing_templates] (
        [routing_id] bigint NOT NULL IDENTITY(1,1),
        [product_id] bigint NOT NULL,
        [version_code] nvarchar(50) NOT NULL,
        [is_active] bit NOT NULL CONSTRAINT [DF_routing_templates_is_active] DEFAULT 1,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_routing_templates] PRIMARY KEY ([routing_id]),
        CONSTRAINT [FK_routing_templates_products] FOREIGN KEY ([product_id]) REFERENCES [products] ([product_id]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[routing_stages]') IS NULL
BEGIN
    CREATE TABLE [routing_stages] (
        [stage_id] bigint NOT NULL IDENTITY(1,1),
        [routing_id] bigint NOT NULL,
        [stage_sequence] int NOT NULL,
        [stage_name] nvarchar(100) NOT NULL,
        [is_hold_point] bit NOT NULL CONSTRAINT [DF_routing_stages_is_hold_point] DEFAULT 0,
        CONSTRAINT [PK_routing_stages] PRIMARY KEY ([stage_id]),
        CONSTRAINT [FK_routing_stages_routing_templates] FOREIGN KEY ([routing_id]) REFERENCES [routing_templates] ([routing_id]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[sales_orders]') IS NULL
BEGIN
    CREATE TABLE [sales_orders] (
        [order_id] bigint NOT NULL IDENTITY(1,1),
        [po_number] nvarchar(100) NOT NULL,
        [customer_id] bigint NOT NULL,
        [created_by] bigint NOT NULL,
        [order_date] date NOT NULL,
        [etd_date] date NOT NULL,
        [total_cbm] decimal(18,4) NULL,
        [status] nvarchar(50) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_sales_orders] PRIMARY KEY ([order_id]),
        CONSTRAINT [UQ_sales_orders_po_number] UNIQUE ([po_number]),
        CONSTRAINT [FK_sales_orders_customers] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([customer_id]),
        CONSTRAINT [FK_sales_orders_users] FOREIGN KEY ([created_by]) REFERENCES [users] ([user_id])
    );
END;
GO

IF OBJECT_ID(N'[order_items]') IS NULL
BEGIN
    CREATE TABLE [order_items] (
        [order_item_id] bigint NOT NULL IDENTITY(1,1),
        [order_id] bigint NOT NULL,
        [product_id] bigint NOT NULL,
        [ordered_qty] int NOT NULL,
        [unit_price_usd] decimal(18,4) NULL,
        [fsc_certified_req] bit NOT NULL CONSTRAINT [DF_order_items_fsc] DEFAULT 0,
        [length_mm] decimal(18,2) NULL,
        [width_mm] decimal(18,2) NULL,
        [height_mm] decimal(18,2) NULL,
        [item_status] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_order_items] PRIMARY KEY ([order_item_id]),
        CONSTRAINT [FK_order_items_sales_orders] FOREIGN KEY ([order_id]) REFERENCES [sales_orders] ([order_id]) ON DELETE CASCADE,
        CONSTRAINT [FK_order_items_products] FOREIGN KEY ([product_id]) REFERENCES [products] ([product_id])
    );
END;
GO

IF OBJECT_ID(N'[order_spaces]') IS NULL
BEGIN
    CREATE TABLE [order_spaces] (
        [space_id] bigint NOT NULL IDENTITY(1,1),
        [order_id] bigint NOT NULL,
        [space_key] nvarchar(50) NOT NULL,
        [space_name] nvarchar(255) NOT NULL,
        [drawing_folder_url] nvarchar(500) NULL,
        [overall_progress_pct] decimal(5,2) NOT NULL CONSTRAINT [DF_order_spaces_progress] DEFAULT 0,
        [status] nvarchar(50) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_order_spaces] PRIMARY KEY ([space_id]),
        CONSTRAINT [UQ_order_spaces_space_key] UNIQUE ([space_key]),
        CONSTRAINT [FK_order_spaces_sales_orders] FOREIGN KEY ([order_id]) REFERENCES [sales_orders] ([order_id]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[boms]') IS NULL
BEGIN
    CREATE TABLE [boms] (
        [bom_id] bigint NOT NULL IDENTITY(1,1),
        [order_item_id] bigint NOT NULL,
        [product_id] bigint NOT NULL,
        [created_by] bigint NOT NULL,
        [scrap_rate_percent] decimal(5,2) NOT NULL,
        [bom_status] nvarchar(50) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_boms] PRIMARY KEY ([bom_id]),
        CONSTRAINT [FK_boms_order_items] FOREIGN KEY ([order_item_id]) REFERENCES [order_items] ([order_item_id]),
        CONSTRAINT [FK_boms_products] FOREIGN KEY ([product_id]) REFERENCES [products] ([product_id]),
        CONSTRAINT [FK_boms_users] FOREIGN KEY ([created_by]) REFERENCES [users] ([user_id])
    );
END;
GO

IF OBJECT_ID(N'[bom_details]') IS NULL
BEGIN
    CREATE TABLE [bom_details] (
        [bom_detail_id] bigint NOT NULL IDENTITY(1,1),
        [bom_id] bigint NOT NULL,
        [material_id] bigint NOT NULL,
        [net_qty_per_unit] decimal(18,6) NOT NULL,
        [gross_total_demand] decimal(18,4) NOT NULL,
        [notes] nvarchar(500) NULL,
        CONSTRAINT [PK_bom_details] PRIMARY KEY ([bom_detail_id]),
        CONSTRAINT [FK_bom_details_boms] FOREIGN KEY ([bom_id]) REFERENCES [boms] ([bom_id]) ON DELETE CASCADE,
        CONSTRAINT [FK_bom_details_materials] FOREIGN KEY ([material_id]) REFERENCES [materials] ([material_id])
    );
END;
GO

IF OBJECT_ID(N'[inventory_stocks]') IS NULL
BEGIN
    CREATE TABLE [inventory_stocks] (
        [stock_id] bigint NOT NULL IDENTITY(1,1),
        [material_id] bigint NOT NULL,
        [physical_qty] decimal(18,4) NOT NULL CONSTRAINT [DF_inventory_stocks_physical] DEFAULT 0,
        [allocated_qty] decimal(18,4) NOT NULL CONSTRAINT [DF_inventory_stocks_allocated] DEFAULT 0,
        [available_qty] decimal(18,4) NOT NULL CONSTRAINT [DF_inventory_stocks_available] DEFAULT 0,
        [updated_at] datetime2 NOT NULL,
        [row_version] rowversion NOT NULL,
        CONSTRAINT [PK_inventory_stocks] PRIMARY KEY ([stock_id]),
        CONSTRAINT [UQ_inventory_stocks_material_id] UNIQUE ([material_id]),
        CONSTRAINT [FK_inventory_stocks_materials] FOREIGN KEY ([material_id]) REFERENCES [materials] ([material_id]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[inventory_transactions]') IS NULL
BEGIN
    CREATE TABLE [inventory_transactions] (
        [transaction_id] bigint NOT NULL IDENTITY(1,1),
        [material_id] bigint NOT NULL,
        [tx_type] nvarchar(50) NOT NULL,
        [quantity] decimal(18,4) NOT NULL,
        [reference_code] nvarchar(100) NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_inventory_transactions] PRIMARY KEY ([transaction_id]),
        CONSTRAINT [FK_inventory_transactions_materials] FOREIGN KEY ([material_id]) REFERENCES [materials] ([material_id])
    );
END;
GO

IF OBJECT_ID(N'[audit_logs]') IS NULL
BEGIN
    CREATE TABLE [audit_logs] (
        [log_id] bigint NOT NULL IDENTITY(1,1),
        [user_id] bigint NULL,
        [action_type] nvarchar(100) NOT NULL,
        [entity_name] nvarchar(100) NOT NULL,
        [entity_id] nvarchar(100) NOT NULL,
        [before_state] nvarchar(max) NULL,
        [after_state] nvarchar(max) NULL,
        [reason] nvarchar(500) NULL,
        [created_at] datetime2 NOT NULL,
        [ip_address] nvarchar(64) NULL,
        CONSTRAINT [PK_audit_logs] PRIMARY KEY ([log_id]),
        CONSTRAINT [FK_audit_logs_users] FOREIGN KEY ([user_id]) REFERENCES [users] ([user_id])
    );
END;
GO

-- 2.1. TỰ ĐỘNG BÙ CÁC CỘT MỚI (CHO TRƯỜNG HỢP CSDL ĐÃ TẠO TỪ PHIÊN BẢN CŨ TRƯỚC ĐÓ)
IF COL_LENGTH(N'[roles]', N'description') IS NULL
BEGIN
    PRINT N'-- Đang bổ sung cột description vào bảng roles...';
    ALTER TABLE [roles] ADD [description] nvarchar(500) NULL;
END;
GO

IF COL_LENGTH(N'[audit_logs]', N'ip_address') IS NULL
BEGIN
    PRINT N'-- Đang bổ sung cột ip_address vào bảng audit_logs...';
    ALTER TABLE [audit_logs] ADD [ip_address] nvarchar(64) NULL;
END;
GO

IF COL_LENGTH(N'[users]', N'last_login_at') IS NULL
BEGIN
    PRINT N'-- Đang bổ sung cột last_login_at vào bảng users...';
    ALTER TABLE [users] ADD [last_login_at] datetime2 NULL;
END;
GO

IF COL_LENGTH(N'[refresh_tokens]', N'created_by_ip') IS NULL AND OBJECT_ID(N'[refresh_tokens]') IS NOT NULL
BEGIN
    ALTER TABLE [refresh_tokens] ADD [created_by_ip] nvarchar(64) NULL;
    ALTER TABLE [refresh_tokens] ADD [replaced_by_token_hash] nvarchar(255) NULL;
    ALTER TABLE [refresh_tokens] ADD [revoked_at] datetime2 NULL;
END;
GO

-- Đăng ký EF Core Migration History để tránh xung đột migration
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261019072335_InitialCreate')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20261019072335_InitialCreate', N'8.0.10');
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261020160920_AddAuthAndPasswordResetEnhancements')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20261020160920_AddAuthAndPasswordResetEnhancements', N'8.0.10');
GO

PRINT N'==============================================================================================';
PRINT N'== BẮT ĐẦU NẠP DỮ LIỆU KIỂM THỬ (SEED DATA) CHO ITERATION 1                                 ==';
PRINT N'==============================================================================================';

-- Xóa dữ liệu cũ theo đúng thứ tự ràng buộc khóa ngoại (Foreign Key Safe Reset)
EXEC sp_MSforeachtable "ALTER TABLE ? NOCHECK CONSTRAINT all";
GO

DELETE FROM [audit_logs];
DELETE FROM [inventory_transactions];
DELETE FROM [inventory_stocks];
DELETE FROM [bom_details];
DELETE FROM [boms];
DELETE FROM [order_spaces];
DELETE FROM [order_items];
DELETE FROM [sales_orders];
DELETE FROM [routing_stages];
DELETE FROM [routing_templates];
DELETE FROM [products];
DELETE FROM [materials];
DELETE FROM [suppliers];
DELETE FROM [customers];
DELETE FROM [defect_catalogs];
DELETE FROM [system_configs];
IF OBJECT_ID(N'[password_reset_tokens]') IS NOT NULL DELETE FROM [password_reset_tokens];
IF OBJECT_ID(N'[refresh_tokens]') IS NOT NULL DELETE FROM [refresh_tokens];
DELETE FROM [user_roles];
DELETE FROM [users];
DELETE FROM [roles];
GO

EXEC sp_MSforeachtable "ALTER TABLE ? WITH CHECK CHECK CONSTRAINT all";
GO

-- 3. NẠP ROLES (8 Vai trò RBAC chuẩn theo Report 4 SDS)
SET IDENTITY_INSERT [roles] ON;
INSERT INTO [roles] ([role_id], [role_code], [role_name], [permissions_json], [description])
VALUES
(1, N'ADMIN', N'System Administrator', N'{"all": true}', N'Toàn quyền quản trị hệ thống, người dùng, cấu hình và audit log'),
(2, N'OWNER', N'Factory Owner / Executive', N'{"view_all": true, "approve_orders": true, "view_dashboard": true}', N'Ban giám đốc xưởng mộc, duyệt đơn hàng và theo dõi chỉ số tổng quan'),
(3, N'SALES', N'Sales Staff', N'{"manage_orders": true, "view_customers": true, "import_orders": true}', N'Nhân viên kinh doanh tiếp nhận đơn hàng, import đơn staging và quản lý khách hàng'),
(4, N'PLANNER', N'Production Planner', N'{"manage_boms": true, "approve_boms": true, "manage_routing": true}', N'Kỹ sư lập kế hoạch kỹ thuật, thiết lập định mức BOM và cấu hình routing'),
(5, N'PURCHASING', N'Purchasing Officer', N'{"manage_pos": true, "manage_suppliers": true, "manage_prs": true}', N'Cán bộ vật tư thu mua, theo dõi nhà cung cấp và tiến độ đơn mua PO'),
(6, N'WAREHOUSE', N'Warehouse Keeper', N'{"manage_inventory": true, "stock_ledger": true, "manage_grns": true}', N'Thủ kho, kiểm soát tồn kho 3 cấp, nhập tồn đầu kỳ và cấp phát vật tư'),
(7, N'LEADER', N'Production Team Leader', N'{"view_routing": true, "view_kanban": true, "shopfloor_exec": true}', N'Quản đốc tổ trưởng sản xuất, giám sát tiến độ công đoạn gia công mộc'),
(8, N'QC', N'Quality Control Inspector', N'{"manage_qc": true, "log_defects": true, "inspect_gates": true}', N'Kiểm soát viên chất lượng tại các trạm kiểm tra và nghiệm thu thành phẩm');
SET IDENTITY_INSERT [roles] OFF;
GO

-- 4. NẠP USERS & USER_ROLES (Mật khẩu: Admin@123)
-- Hash BCrypt chuẩn cho Admin@123: $2a$11$AaBPdtqXhM27hH3BPYWH2.nfILgc1WK0RZJniq3N6YwHbzHtKmJAu
SET IDENTITY_INSERT [users] ON;
INSERT INTO [users] ([user_id], [username], [email], [password_hash], [full_name], [phone], [status], [failed_login_count], [created_at], [updated_at])
VALUES
(1, N'admin', N'admin@wmms.local', N'$2a$11$AaBPdtqXhM27hH3BPYWH2.nfILgc1WK0RZJniq3N6YwHbzHtKmJAu', N'Quản trị viên Hệ thống', N'0901234567', N'ACTIVE', 0, GETUTCDATE(), GETUTCDATE()),
(2, N'owner', N'owner@nguyenkhoa.vn', N'$2a$11$AaBPdtqXhM27hH3BPYWH2.nfILgc1WK0RZJniq3N6YwHbzHtKmJAu', N'Nguyễn Khoa (Giám đốc Xưởng)', N'0902345678', N'ACTIVE', 0, GETUTCDATE(), GETUTCDATE()),
(3, N'sales01', N'sales01@nguyenkhoa.vn', N'$2a$11$AaBPdtqXhM27hH3BPYWH2.nfILgc1WK0RZJniq3N6YwHbzHtKmJAu', N'Lê Minh Sales (Trưởng phòng KD)', N'0903456789', N'ACTIVE', 0, GETUTCDATE(), GETUTCDATE()),
(4, N'sales02', N'sales02@nguyenkhoa.vn', N'$2a$11$AaBPdtqXhM27hH3BPYWH2.nfILgc1WK0RZJniq3N6YwHbzHtKmJAu', N'Phạm Thu Trang (Chuyên viên KD)', N'0904567890', N'ACTIVE', 0, GETUTCDATE(), GETUTCDATE()),
(5, N'planner01', N'planner01@nguyenkhoa.vn', N'$2a$11$AaBPdtqXhM27hH3BPYWH2.nfILgc1WK0RZJniq3N6YwHbzHtKmJAu', N'Trần Văn Kế Hoạch (Kỹ sư BOM)', N'0905678901', N'ACTIVE', 0, GETUTCDATE(), GETUTCDATE()),
(6, N'purchasing01', N'purchasing01@nguyenkhoa.vn', N'$2a$11$AaBPdtqXhM27hH3BPYWH2.nfILgc1WK0RZJniq3N6YwHbzHtKmJAu', N'Vũ Thị Thu Mua (Phụ trách vật tư)', N'0906789012', N'ACTIVE', 0, GETUTCDATE(), GETUTCDATE()),
(7, N'warehouse01', N'warehouse01@nguyenkhoa.vn', N'$2a$11$AaBPdtqXhM27hH3BPYWH2.nfILgc1WK0RZJniq3N6YwHbzHtKmJAu', N'Đỗ Hữu Kho (Thủ kho chính)', N'0907890123', N'ACTIVE', 0, GETUTCDATE(), GETUTCDATE()),
(8, N'leader01', N'leader01@nguyenkhoa.vn', N'$2a$11$AaBPdtqXhM27hH3BPYWH2.nfILgc1WK0RZJniq3N6YwHbzHtKmJAu', N'Ngô Văn Quản Đốc (Xưởng mộc)', N'0908901234', N'ACTIVE', 0, GETUTCDATE(), GETUTCDATE()),
(9, N'qc01', N'qc01@nguyenkhoa.vn', N'$2a$11$AaBPdtqXhM27hH3BPYWH2.nfILgc1WK0RZJniq3N6YwHbzHtKmJAu', N'Hoàng Thị KCS (Kiểm tra chất lượng)', N'0909012345', N'ACTIVE', 0, GETUTCDATE(), GETUTCDATE()),
-- Tài khoản 5 thành viên trong nhóm SEP490_G122
(10, N'luonghd', N'luonghd@wmms.vn', N'$2a$11$AaBPdtqXhM27hH3BPYWH2.nfILgc1WK0RZJniq3N6YwHbzHtKmJAu', N'Hoàng Đức Lương (Team Leader)', N'0911001001', N'ACTIVE', 0, GETUTCDATE(), GETUTCDATE()),
(11, N'duynd', N'duynd@wmms.vn', N'$2a$11$AaBPdtqXhM27hH3BPYWH2.nfILgc1WK0RZJniq3N6YwHbzHtKmJAu', N'Nguyễn Đức Duy (BE Developer)', N'0911002002', N'ACTIVE', 0, GETUTCDATE(), GETUTCDATE()),
(12, N'duongnt', N'duongnt@wmms.vn', N'$2a$11$AaBPdtqXhM27hH3BPYWH2.nfILgc1WK0RZJniq3N6YwHbzHtKmJAu', N'Nguyễn Tùng Dương (BE Developer)', N'0911003003', N'ACTIVE', 0, GETUTCDATE(), GETUTCDATE()),
(13, N'cuonghd', N'cuonghd@wmms.vn', N'$2a$11$AaBPdtqXhM27hH3BPYWH2.nfILgc1WK0RZJniq3N6YwHbzHtKmJAu', N'Hoàng Đại Cương (BE Developer)', N'0911004004', N'ACTIVE', 0, GETUTCDATE(), GETUTCDATE()),
(14, N'anhth', N'anhth@wmms.vn', N'$2a$11$AaBPdtqXhM27hH3BPYWH2.nfILgc1WK0RZJniq3N6YwHbzHtKmJAu', N'Trần Hùng Anh (FE Developer)', N'0911005005', N'ACTIVE', 0, GETUTCDATE(), GETUTCDATE());
SET IDENTITY_INSERT [users] OFF;
GO

INSERT INTO [user_roles] ([user_id], [role_id], [assigned_at])
VALUES
(1, 1, GETUTCDATE()), -- admin -> ADMIN
(2, 2, GETUTCDATE()), -- owner -> OWNER
(3, 3, GETUTCDATE()), -- sales01 -> SALES
(4, 3, GETUTCDATE()), -- sales02 -> SALES
(5, 4, GETUTCDATE()), -- planner01 -> PLANNER
(6, 5, GETUTCDATE()), -- purchasing01 -> PURCHASING
(7, 6, GETUTCDATE()), -- warehouse01 -> WAREHOUSE
(8, 7, GETUTCDATE()), -- leader01 -> LEADER
(9, 8, GETUTCDATE()), -- qc01 -> QC
(10, 1, GETUTCDATE()), (10, 2, GETUTCDATE()), -- luonghd -> ADMIN, OWNER
(11, 3, GETUTCDATE()), (11, 5, GETUTCDATE()), -- duynd -> SALES, PURCHASING
(12, 4, GETUTCDATE()), (12, 7, GETUTCDATE()), -- duongnt -> PLANNER, LEADER
(13, 6, GETUTCDATE()), (13, 8, GETUTCDATE()), -- cuonghd -> WAREHOUSE, QC
(14, 3, GETUTCDATE()), (14, 1, GETUTCDATE()); -- anhth -> SALES, ADMIN
GO

-- 5. NẠP SYSTEM CONFIGS
SET IDENTITY_INSERT [system_configs] ON;
INSERT INTO [system_configs] ([config_id], [config_key], [config_value], [data_type], [description], [updated_at])
VALUES
(1, N'DEFAULT_SCRAP_PERCENT_MIN', N'3.00', N'DECIMAL', N'Tỷ lệ hao hụt mặc định tối thiểu cho tính toán BOM và MRP (3%)', GETUTCDATE()),
(2, N'DEFAULT_SCRAP_PERCENT_MAX', N'5.00', N'DECIMAL', N'Tỷ lệ hao hụt mặc định tối đa cho tính toán BOM và MRP (5%)', GETUTCDATE()),
(3, N'ETD_WARNING_DAYS', N'7', N'INT', N'Ngưỡng cảnh báo trước hạn giao hàng ETD tính theo số ngày (7 ngày)', GETUTCDATE()),
(4, N'ORDER_STAGING_BATCH_SIZE', N'100', N'INT', N'Kích thước tối đa mỗi lô bóc tách đơn hàng trong vùng Staging Area', GETUTCDATE());
SET IDENTITY_INSERT [system_configs] OFF;
GO

-- 6. NẠP DEFECT CATALOGS (Danh mục lỗi chuẩn hóa)
INSERT INTO [defect_catalogs] ([defect_code], [defect_name], [standard_stage], [root_cause], [recommended_action])
VALUES
(N'CUT_ERR_01', N'Sai lệch kích thước phôi cưa xẻ', N'Pha phôi gỗ thô', N'Lưỡi cưa mòn hoặc bàn cưa rung lắc cơ khí', N'Căn chỉnh lại dưỡng cưa và cắt xẻ lại chi tiết'),
(N'WOOD_ERR_01', N'Khuyết tật mắt gỗ / Nứt tét phôi', N'Pha phôi gỗ thô', N'Mắt chết tự nhiên hoặc nứt đầu tấm gỗ xẻ sấy', N'Cắt bỏ đoạn nứt, dán trám keo hoặc phân loại sang thanh phụ'),
(N'SURF_ERR_02', N'Bề mặt gỗ sần sùi / Xước vết nhám', N'Bào & Chà nhám', N'Giấy nhám mòn hạt hoặc tốc độ đưa phôi không đều', N'Chà nhám lại bằng hạt nhám P240-P320 trước khi sơn'),
(N'ASSY_ERR_03', N'Lệch mộng kết cấu / Lắp ráp kênh', N'Lắp ráp kết cấu', N'Khoan lệch tâm lỗ mộng hoặc độ ẩm gỗ co ngót', N'Tháo dỡ, căn chỉnh chốt gỗ và ép vam keo đồng phẳng'),
(N'PAINT_ERR_04', N'Bọt khí / Chảy màng sơn PU', N'Sơn lót & Sơn phủ', N'Áp suất súng phun quá lớn hoặc bụi sơn bám bề mặt', N'Xả nhám cục bộ và dặm lại lớp sơn phủ bóng mờ');
GO

-- 7. NẠP CUSTOMERS (Khách hàng xuất khẩu & nội địa)
SET IDENTITY_INSERT [customers] ON;
INSERT INTO [customers] ([customer_id], [customer_code], [company_name], [contact_person], [email], [country], [created_at], [updated_at])
VALUES
(1, N'CUST-ASHLEY-US', N'Ashley Furniture HomeStore', N'Michael Vance (Sourcing Director)', N'procurement@ashleyfurniture.com', N'USA', GETUTCDATE(), GETUTCDATE()),
(2, N'CUST-IKEA-SE', N'IKEA Supply AG (Global Furniture)', N'Johan Lindqvist (Category Manager)', N'orders.apac@ikea.com', N'Sweden', GETUTCDATE(), GETUTCDATE()),
(3, N'CUST-NITORI-JP', N'Nitori Co., Ltd (Furniture Division)', N'Kenji Takahashi (Quality Supervisor)', N'import@nitori.co.jp', N'Japan', GETUTCDATE(), GETUTCDATE()),
(4, N'CUST-WAYFAIR-US', N'Wayfair E-Commerce Home Goods', N'Sarah Jenkins (Supply Chain Lead)', N'vendor@wayfair.com', N'USA', GETUTCDATE(), GETUTCDATE()),
(5, N'CUST-HOAPHAT-VN', N'Tập đoàn Nội Thất Hòa Phát', N'Nguyễn Văn Tuấn (Trưởng ban Cung ứng)', N'cungung@hoaphatfurniture.vn', N'Vietnam', GETUTCDATE(), GETUTCDATE());
SET IDENTITY_INSERT [customers] OFF;
GO

-- 8. NẠP SUPPLIERS (Nhà cung cấp vật tư)
SET IDENTITY_INSERT [suppliers] ON;
INSERT INTO [suppliers] ([supplier_id], [supplier_code], [supplier_name], [supply_category], [contact_email], [phone], [lead_time_days], [status], [created_at], [updated_at])
VALUES
(1, N'SUP-WOOD-LAMVIET', N'Công ty CP Chế biến Gỗ Lâm Việt', N'LUMBER', N'sales@lamvietwood.vn', N'02743789123', 10, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(2, N'SUP-WOOD-TAVICO', N'Công ty CP Gỗ Tây Ngô Tavico Timber', N'LUMBER', N'info@tavicowood.com', N'02838992233', 14, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(3, N'SUP-HARDWARE-HAFELE', N'Công ty TNHH Hafele Việt Nam', N'HARDWARE', N'support.vn@hafele.com', N'02839113111', 5, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(4, N'SUP-HARDWARE-BLUM', N'Công ty TNHH Phụ kiện Mộc Blum', N'HARDWARE', N'sales@blum.vn', N'02838224455', 7, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(5, N'SUP-PAINT-OSEVEN', N'Công ty Cổ phần Sơn Oseven PU', N'FINISH_PAINT', N'kinhdoanh@osevenpaint.com', N'02837651234', 3, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(6, N'SUP-PACK-THANGLONG', N'Bao bì & Thùng Carton Thăng Long', N'PACKAGING', N'sales@thanglongbox.com.vn', N'02438556677', 4, N'ACTIVE', GETUTCDATE(), GETUTCDATE());
SET IDENTITY_INSERT [suppliers] OFF;
GO

-- 9. NẠP MATERIALS (14 Vật tư đại diện đầy đủ 4 nhóm: Gỗ, Cơ kim, Sơn, Đóng gói)
SET IDENTITY_INSERT [materials] ON;
INSERT INTO [materials] ([material_id], [material_code], [material_name], [material_group], [unit_of_measure], [dimension_spec], [fsc_certified], [safety_stock_qty], [status], [created_at], [updated_at])
VALUES
-- Nhóm Gỗ (LUMBER)
(1, N'WOOD_OAK_01', N'Gỗ sồi Bắc Mỹ xẻ sấy FAS (Oak Timber)', N'LUMBER', N'M3', N'26mm x 150mm x 2400mm', 1, 15.0000, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(2, N'WOOD_ASH_02', N'Gỗ tần bì nhập khẩu sấy đạt chuẩn (Ash Timber)', N'LUMBER', N'M3', N'25mm x 120mm x 2200mm', 1, 10.0000, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(3, N'WOOD_RUBBER_03', N'Gỗ cao su ghép thanh tiêu chuẩn AB', N'LUMBER', N'M3', N'20mm x 1200mm x 2400mm', 1, 20.0000, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(4, N'WOOD_PLY_04', N'Gỗ dán Plywood cốt Birch phủ Veneer', N'LUMBER', N'M3', N'18mm x 1220mm x 2440mm', 1, 10.0000, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
-- Nhóm Cơ kim / Phụ kiện (HARDWARE)
(5, N'HW_SCREW_M4', N'Vít bắt gỗ tự khoan mạ kẽm M4x30', N'HARDWARE', N'PCS', N'M4 x 30mm', 0, 5000.0000, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(6, N'HW_SCREW_M5', N'Vít liên kết mộng giàn chân chìm M5x50', N'HARDWARE', N'PCS', N'M5 x 50mm', 0, 3000.0000, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(7, N'HW_HINGE_01', N'Bản lề giảm chấn Hafele SUS304 trùm nửa', N'HARDWARE', N'PCS', N'Góc mở 110 độ Hafele', 0, 800.0000, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(8, N'HW_HANDLE_01', N'Tay nắm hợp kim nhôm xước mờ cao cấp', N'HARDWARE', N'PCS', N'Tâm lỗ 96mm x Dài 120mm', 0, 400.0000, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(9, N'HW_SLIDE_450', N'Ray trượt bi 3 tầng giảm chấn 450mm', N'HARDWARE', N'PCS', N'Bản rộng 45mm x Dài 450mm', 0, 300.0000, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
-- Nhóm Sơn & Hóa chất (FINISH_PAINT)
(10, N'PAINT_SEALER_01', N'Sơn lót PU 2K kháng ẩm gốc nước', N'FINISH_PAINT', N'LITER', N'Thùng tiêu chuẩn 20L', 0, 150.0000, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(11, N'PAINT_TOP_MATT', N'Sơn phủ bóng mờ PU 50% chống trầy xước', N'FINISH_PAINT', N'LITER', N'Thùng tiêu chuẩn 20L', 0, 120.0000, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(12, N'GLUE_WOOD_T3', N'Keo dán mộng gỗ công nghiệp Titebond II', N'FINISH_PAINT', N'LITER', N'Can công nghiệp 5 Gallon', 0, 50.0000, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
-- Nhóm Đóng gói (PACKAGING)
(13, N'BOX_CARTON_01', N'Thùng carton 5 lớp xuất khẩu Master Case', N'PACKAGING', N'PCS', N'600mm x 550mm x 950mm', 1, 600.0000, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(14, N'FOAM_PE_01', N'Màng xốp PE Foam 2mm bọc chống sốc bề mặt', N'PACKAGING', N'M2', N'Cuộn 1.05m x 150m', 0, 500.0000, N'ACTIVE', GETUTCDATE(), GETUTCDATE());
SET IDENTITY_INSERT [materials] OFF;
GO

-- 10. NẠP PRODUCTS (4 SKU Thành phẩm tiêu biểu xưởng mộc)
SET IDENTITY_INSERT [products] ON;
INSERT INTO [products] ([product_id], [product_sku], [product_name], [category_name], [dimensions_cm], [wood_species], [finish_color], [packing_spec], [cbm_per_unit], [image_url], [cad_drawing_url], [status], [created_at], [updated_at])
VALUES
(1, N'CHAIR-DINING-OAK-01', N'Ghế ăn tựa nan gỗ sồi Bắc Mỹ', N'CHAIR', N'45 x 52 x 88 cm', N'White Oak', N'Natural Walnut', N'2 pcs / Master Carton', 0.1200, N'/images/products/chair-oak-01.jpg', N'/drawings/cad/chair-oak-01.dwg', N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(2, N'TABLE-DINING-EXT-02', N'Bàn ăn thông minh kéo dài gỗ sồi', N'TABLE', N'160/210 x 90 x 75 cm', N'White Oak', N'Smoked Oak', N'1 pc / 2 Cartons', 0.3500, N'/images/products/table-ext-02.jpg', N'/drawings/cad/table-ext-02.dwg', N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(3, N'CABINET-SIDEBOARD-03', N'Tủ sideboard 3 cánh 2 hộc kéo', N'CABINET', N'150 x 42 x 82 cm', N'Rubber Wood + Oak Veneer', N'Warm Chestnut', N'1 pc / Carton kèm xốp', 0.5800, N'/images/products/sideboard-03.jpg', N'/drawings/cad/sideboard-03.dwg', N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(4, N'DESK-WORK-MODERN-04', N'Bàn làm việc tối giản chân tiện gỗ tần bì', N'DESK', N'120 x 60 x 75 cm', N'Ash Wood', N'Natural Oak', N'1 pc / Carton Flat-pack', 0.2200, N'/images/products/desk-modern-04.jpg', N'/drawings/cad/desk-modern-04.dwg', N'ACTIVE', GETUTCDATE(), GETUTCDATE());
SET IDENTITY_INSERT [products] OFF;
GO

-- 11. NẠP ROUTING TEMPLATES & ROUTING STAGES (Quy trình công nghệ sản xuất)
SET IDENTITY_INSERT [routing_templates] ON;
INSERT INTO [routing_templates] ([routing_id], [product_id], [version_code], [is_active], [created_at], [updated_at])
VALUES
(1, 1, N'v1.0', 1, GETUTCDATE(), GETUTCDATE()),
(2, 2, N'v1.0', 1, GETUTCDATE(), GETUTCDATE()),
(3, 3, N'v1.0', 1, GETUTCDATE(), GETUTCDATE()),
(4, 4, N'v1.0', 1, GETUTCDATE(), GETUTCDATE());
SET IDENTITY_INSERT [routing_templates] OFF;
GO

SET IDENTITY_INSERT [routing_stages] ON;
INSERT INTO [routing_stages] ([stage_id], [routing_id], [stage_sequence], [stage_name], [is_hold_point])
VALUES
-- Routing cho Ghế (routing_id = 1)
(1, 1, 1, N'1. Pha phôi gỗ sồi thô theo chiều dài nan ghế', 0),
(2, 1, 2, N'2. Cắt/Xẻ định hình chân ghế và nan cong', 0),
(3, 1, 3, N'3. Bào 4 mặt & Chà nhám phẳng mịn chi tiết', 0),
(4, 1, 4, N'4. Khoan lỗ chốt & Phay mộng âm dương chân/mặt ghế', 0),
(5, 1, 5, N'5. Lắp ráp khung giàn ghế & Vào mộng keo dán ép vam', 1), -- Hold Point 1: Kiểm tra liên kết mộng
(6, 1, 6, N'6. Phun sơn lót PU và Phủ mờ 50% hoàn thiện', 0),
(7, 1, 7, N'7. Kiểm tra xuất xưởng KCS & Đóng thùng carton (2 cái/thùng)', 0),
-- Routing cho Bàn (routing_id = 2)
(8, 2, 1, N'1. Pha phôi gỗ tấm mặt bàn & giàn chân trụ', 0),
(9, 2, 2, N'2. Ghép mộng mang cá mặt bàn mở rộng', 0),
(10, 2, 3, N'3. Chà nhám thùng bề mặt đạt độ phẳng tiêu chuẩn', 0),
(11, 2, 4, N'4. Gia công rãnh lắp cơ cấu ray trượt ray thông minh', 0),
(12, 2, 5, N'5. Lắp ráp cơ cấu mở rộng & Thử tải trượt êm', 1), -- Hold Point 2: Kiểm tra ray trượt mở rộng
(13, 2, 6, N'6. Sơn phủ chống ẩm 3 lớp tiêu chuẩn xuất khẩu', 0),
(14, 2, 7, N'7. Bọc xốp PE Foam góc & Đóng gói 2 kiện/bộ bàn', 0),
-- Routing cho Tủ (routing_id = 3)
(15, 3, 1, N'1. Cắt ván Plywood & Gỗ xẻ theo kích thước hộp tủ', 0),
(16, 3, 2, N'2. Dán nẹp chỉ cạnh Veneer và phay soi hèm', 0),
(17, 3, 3, N'3. Khoan giàn lỗ bản lề bật và ray trượt hộc kéo', 0),
(18, 3, 4, N'4. Lắp ráp thùng tủ và cân chỉnh cánh tủ', 1), -- Hold Point 3: Kiểm tra khe hở cánh & ray
(19, 3, 5, N'5. Hoàn thiện bề mặt sơn bóng mờ màu hạt dẻ', 0),
(20, 3, 6, N'6. Gắn tay nắm nhôm và bọc phụ kiện dự phòng', 0),
(21, 3, 7, N'7. Kiểm định xuất khẩu & Đóng thùng carton xuất khẩu', 0);
SET IDENTITY_INSERT [routing_stages] OFF;
GO

-- 12. NẠP SALES ORDERS, ORDER ITEMS, ORDER SPACES (Đơn hàng B2B Master Case)
SET IDENTITY_INSERT [sales_orders] ON;
INSERT INTO [sales_orders] ([order_id], [po_number], [customer_id], [created_by], [order_date], [etd_date], [total_cbm], [status], [created_at], [updated_at])
VALUES
(1, N'ASH-2026-OCT-880504', 1, 3, DATEADD(day, -4, CAST(GETUTCDATE() AS date)), DATEADD(day, 18, CAST(GETUTCDATE() AS date)), 60.0000, N'CONFIRMED', GETUTCDATE(), GETUTCDATE()),
(2, N'ASH-2026-OCT-880505', 1, 3, DATEADD(day, -3, CAST(GETUTCDATE() AS date)), DATEADD(day, 22, CAST(GETUTCDATE() AS date)), 122.5000, N'CONFIRMED', GETUTCDATE(), GETUTCDATE()),
(3, N'IKE-2026-OCT-990102', 2, 4, DATEADD(day, -2, CAST(GETUTCDATE() AS date)), DATEADD(day, 25, CAST(GETUTCDATE() AS date)), 174.0000, N'CONFIRMED', GETUTCDATE(), GETUTCDATE()),
(4, N'NIT-2026-OCT-770301', 3, 3, DATEADD(day, -1, CAST(GETUTCDATE() AS date)), DATEADD(day, 30, CAST(GETUTCDATE() AS date)), 36.0000, N'CONFIRMED', GETUTCDATE(), GETUTCDATE()),
(5, N'HOA-2026-OCT-110205', 5, 4, CAST(GETUTCDATE() AS date), DATEADD(day, 35, CAST(GETUTCDATE() AS date)), 44.0000, N'DRAFT', GETUTCDATE(), GETUTCDATE());
SET IDENTITY_INSERT [sales_orders] OFF;
GO

SET IDENTITY_INSERT [order_items] ON;
INSERT INTO [order_items] ([order_item_id], [order_id], [product_id], [ordered_qty], [unit_price_usd], [fsc_certified_req], [length_mm], [width_mm], [height_mm], [item_status])
VALUES
(1, 1, 1, 500, 48.5000, 1, 450.00, 520.00, 880.00, N'BOM_APPROVED'),
(2, 2, 2, 350, 165.0000, 1, 1600.00, 900.00, 750.00, N'BOM_APPROVED'),
(3, 3, 3, 300, 210.0000, 1, 1500.00, 420.00, 820.00, N'PENDING_BOM'),
(4, 4, 1, 300, 48.0000, 1, 450.00, 520.00, 880.00, N'PENDING_BOM'),
(5, 5, 4, 200, 85.0000, 1, 1200.00, 600.00, 750.00, N'PENDING_BOM');
SET IDENTITY_INSERT [order_items] OFF;
GO

SET IDENTITY_INSERT [order_spaces] ON;
INSERT INTO [order_spaces] ([space_id], [order_id], [space_key], [space_name], [drawing_folder_url], [overall_progress_pct], [status], [created_at], [updated_at])
VALUES
(1, 1, N'SPACE-PO-880504', N'SPACE-PO-880504 (500x Ghế ăn sồi - Ashley Furniture)', N'/spaces/SPACE-PO-880504/drawings', 15.00, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(2, 2, N'SPACE-PO-880505', N'SPACE-PO-880505 (350x Bàn ăn thông minh - Ashley Furniture)', N'/spaces/SPACE-PO-880505/drawings', 10.00, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(3, 3, N'SPACE-PO-990102', N'SPACE-PO-990102 (300x Tủ sideboard 3 cánh - IKEA Supply)', N'/spaces/SPACE-PO-990102/drawings', 5.00, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(4, 4, N'SPACE-PO-770301', N'SPACE-PO-770301 (300x Ghế ăn sồi - Nitori Japan)', N'/spaces/SPACE-PO-770301/drawings', 0.00, N'ACTIVE', GETUTCDATE(), GETUTCDATE()),
(5, 5, N'SPACE-PO-110205', N'SPACE-PO-110205 (200x Bàn làm việc tần bì - Hòa Phát VN)', N'/spaces/SPACE-PO-110205/drawings', 0.00, N'ACTIVE', GETUTCDATE(), GETUTCDATE());
SET IDENTITY_INSERT [order_spaces] OFF;
GO

-- 13. NẠP BOMS & BOM DETAILS (Định mức kỹ thuật sản phẩm đa cấp)
SET IDENTITY_INSERT [boms] ON;
INSERT INTO [boms] ([bom_id], [order_item_id], [product_id], [created_by], [scrap_rate_percent], [bom_status], [created_at], [updated_at])
VALUES
(1, 1, 1, 5, 4.00, N'APPROVED', GETUTCDATE(), GETUTCDATE()),
(2, 2, 2, 5, 3.50, N'APPROVED', GETUTCDATE(), GETUTCDATE()),
(3, 3, 3, 5, 5.00, N'PENDING_APPROVAL', GETUTCDATE(), GETUTCDATE());
SET IDENTITY_INSERT [boms] OFF;
GO

SET IDENTITY_INSERT [bom_details] ON;
INSERT INTO [bom_details] ([bom_detail_id], [bom_id], [material_id], [net_qty_per_unit], [gross_total_demand], [notes])
VALUES
-- BOM cho Ghế ăn sồi (bom_id = 1, OrderItem 1: 500 cái, hao hụt 4%)
(1, 1, 1, 0.025000, 13.0000, N'Gỗ sồi xẻ sấy làm 4 chân ghế và nan tựa cong lưng'),
(2, 1, 5, 12.000000, 6240.0000, N'Vít tự khoan M4x30 gia cố liên kết khung giằng mộng ghế'),
(3, 1, 10, 0.150000, 78.0000, N'Sơn lót PU 2K kháng ẩm 2 lớp'),
(4, 1, 11, 0.100000, 52.0000, N'Sơn phủ bóng mờ 50% chống trầy hoàn thiện màu tự nhiên'),
(5, 1, 12, 0.050000, 26.0000, N'Keo sữa ghép mộng dán Titebond II chống nước'),
(6, 1, 13, 0.500000, 260.0000, N'Thùng carton 5 lớp (Quy cách 2 ghế đóng chung 1 thùng)'),
(7, 1, 14, 1.200000, 624.0000, N'Xốp PE bọc bảo vệ bề mặt nan gỗ chống trầy'),
-- BOM cho Bàn ăn thông minh (bom_id = 2, OrderItem 2: 350 cái, hao hụt 3.5%)
(8, 2, 1, 0.080000, 28.9800, N'Gỗ sồi FAS làm mặt bàn mở rộng và chân bàn hộp'),
(9, 2, 9, 1.000000, 362.2500, N'Bộ ray trượt mở rộng thông minh 450mm'),
(10, 2, 6, 24.000000, 8694.0000, N'Vít chìm liên kết chân M5x50'),
(11, 2, 10, 0.450000, 162.9900, N'Sơn lót kháng ẩm PU bề mặt bàn lớn'),
(12, 2, 11, 0.300000, 108.6800, N'Sơn phủ mờ 50% chống trầy xước mặt bàn ăn'),
(13, 2, 13, 2.000000, 724.5000, N'Thùng carton đóng gói (1 bàn đóng 2 kiện: mặt bàn + chân bàn)'),
-- BOM cho Tủ Sideboard (bom_id = 3, OrderItem 3: 300 cái, hao hụt 5%)
(14, 3, 3, 0.110000, 34.6500, N'Gỗ cao su ghép thanh làm khung vách tủ và đợt ngang'),
(15, 3, 4, 0.040000, 12.6000, N'Gỗ dán Plywood làm hậu tủ và đáy hộc kéo'),
(16, 3, 7, 6.000000, 1890.0000, N'Bản lề giảm chấn Hafele SUS304 (2 bản lề x 3 cánh tủ)'),
(17, 3, 8, 5.000000, 1575.0000, N'Tay nắm hợp kim nhôm (3 cánh + 2 hộc kéo)'),
(18, 3, 9, 2.000000, 630.0000, N'Ray bi giảm chấn 450mm cho 2 hộc kéo tủ'),
(19, 3, 11, 0.500000, 157.5000, N'Sơn phủ mờ màu hạt dẻ Warm Chestnut'),
(20, 3, 13, 1.000000, 315.0000, N'Thùng carton kiện lớn kèm xốp định hình góc');
SET IDENTITY_INSERT [bom_details] OFF;
GO

-- 14. NẠP INVENTORY STOCKS & INITIAL TRANSACTIONS (Tồn kho 3 cấp & Sổ kho đầu kỳ)
SET IDENTITY_INSERT [inventory_stocks] ON;
INSERT INTO [inventory_stocks] ([stock_id], [material_id], [physical_qty], [allocated_qty], [available_qty], [updated_at])
VALUES
-- Gỗ sồi: Có 60 M3 thực tế trong kho, đã giữ chỗ (Allocated) 13 M3 cho Đơn hàng 1, khả dụng 47 M3
(1, 1, 60.0000, 13.0000, 47.0000, GETUTCDATE()),
(2, 2, 45.0000, 0.0000, 45.0000, GETUTCDATE()),
(3, 3, 80.0000, 0.0000, 80.0000, GETUTCDATE()),
(4, 4, 50.0000, 0.0000, 50.0000, GETUTCDATE()),
(5, 5, 50000.0000, 6240.0000, 43760.0000, GETUTCDATE()),
(6, 6, 30000.0000, 8694.0000, 21306.0000, GETUTCDATE()),
(7, 7, 5000.0000, 0.0000, 5000.0000, GETUTCDATE()),
(8, 8, 3000.0000, 0.0000, 3000.0000, GETUTCDATE()),
(9, 9, 2000.0000, 362.2500, 1637.7500, GETUTCDATE()),
(10, 10, 800.0000, 240.9900, 559.0100, GETUTCDATE()),
(11, 11, 600.0000, 160.6800, 439.3200, GETUTCDATE()),
(12, 12, 300.0000, 26.0000, 274.0000, GETUTCDATE()),
(13, 13, 4000.0000, 984.5000, 3015.5000, GETUTCDATE()),
(14, 14, 2000.0000, 624.0000, 1376.0000, GETUTCDATE());
SET IDENTITY_INSERT [inventory_stocks] OFF;
GO

SET IDENTITY_INSERT [inventory_transactions] ON;
INSERT INTO [inventory_transactions] ([transaction_id], [material_id], [tx_type], [quantity], [reference_code], [created_at])
VALUES
(1, 1, N'INITIAL_IMPORT', 60.0000, N'INIT-STOCK-2026-OCT-01', DATEADD(day, -5, GETUTCDATE())),
(2, 2, N'INITIAL_IMPORT', 45.0000, N'INIT-STOCK-2026-OCT-01', DATEADD(day, -5, GETUTCDATE())),
(3, 3, N'INITIAL_IMPORT', 80.0000, N'INIT-STOCK-2026-OCT-01', DATEADD(day, -5, GETUTCDATE())),
(4, 4, N'INITIAL_IMPORT', 50.0000, N'INIT-STOCK-2026-OCT-01', DATEADD(day, -5, GETUTCDATE())),
(5, 5, N'INITIAL_IMPORT', 50000.0000, N'INIT-STOCK-2026-OCT-01', DATEADD(day, -5, GETUTCDATE())),
(6, 6, N'INITIAL_IMPORT', 30000.0000, N'INIT-STOCK-2026-OCT-01', DATEADD(day, -5, GETUTCDATE())),
(7, 7, N'INITIAL_IMPORT', 5000.0000, N'INIT-STOCK-2026-OCT-01', DATEADD(day, -5, GETUTCDATE())),
(8, 8, N'INITIAL_IMPORT', 3000.0000, N'INIT-STOCK-2026-OCT-01', DATEADD(day, -5, GETUTCDATE())),
(9, 9, N'INITIAL_IMPORT', 2000.0000, N'INIT-STOCK-2026-OCT-01', DATEADD(day, -5, GETUTCDATE())),
(10, 10, N'INITIAL_IMPORT', 800.0000, N'INIT-STOCK-2026-OCT-01', DATEADD(day, -5, GETUTCDATE())),
(11, 11, N'INITIAL_IMPORT', 600.0000, N'INIT-STOCK-2026-OCT-01', DATEADD(day, -5, GETUTCDATE())),
(12, 12, N'INITIAL_IMPORT', 300.0000, N'INIT-STOCK-2026-OCT-01', DATEADD(day, -5, GETUTCDATE())),
(13, 13, N'INITIAL_IMPORT', 4000.0000, N'INIT-STOCK-2026-OCT-01', DATEADD(day, -5, GETUTCDATE())),
(14, 14, N'INITIAL_IMPORT', 2000.0000, N'INIT-STOCK-2026-OCT-01', DATEADD(day, -5, GETUTCDATE()));
SET IDENTITY_INSERT [inventory_transactions] OFF;
GO

-- 15. NẠP AUDIT LOGS (Nhật ký kiểm toán mẫu phục vụ demo màn hình Audit Logs)
SET IDENTITY_INSERT [audit_logs] ON;
INSERT INTO [audit_logs] ([log_id], [user_id], [action_type], [entity_name], [entity_id], [before_state], [after_state], [reason], [created_at], [ip_address])
VALUES
(1, 1, N'AUTH_LOGIN_SUCCESS', N'User', N'1', NULL, N'{"username": "admin", "role": "ADMIN"}', N'Đăng nhập quản trị viên thành công', DATEADD(minute, -120, GETUTCDATE()), N'127.0.0.1'),
(2, 1, N'USER_CREATED', N'User', N'2', NULL, N'{"username": "owner", "full_name": "Nguyen Khoa"}', N'Khởi tạo tài khoản Giám đốc xưởng mộc', DATEADD(minute, -110, GETUTCDATE()), N'127.0.0.1'),
(3, 3, N'ORDER_CREATED', N'SalesOrder', N'1', NULL, N'{"po_number": "ASH-2026-OCT-880504", "status": "CONFIRMED"}', N'Tiếp nhận và xác nhận đơn hàng Master Case 500 cái ghế Ashley', DATEADD(minute, -90, GETUTCDATE()), N'192.168.1.15'),
(4, 5, N'BOM_APPROVED', N'BomHeader', N'1', N'{"bom_status": "DRAFT"}', N'{"bom_status": "APPROVED", "scrap_rate": 4.0}', N'Phê duyệt định mức kỹ thuật BOM ghế ăn sồi v1.0', DATEADD(minute, -60, GETUTCDATE()), N'192.168.1.20'),
(5, 7, N'STOCK_INITIAL_IMPORT', N'MaterialStock', N'1', NULL, N'{"material_code": "WOOD_OAK_01", "physical_qty": 60.0}', N'Kiểm kê và nạp số dư tồn kho ban đầu', DATEADD(minute, -30, GETUTCDATE()), N'192.168.1.25');
SET IDENTITY_INSERT [audit_logs] OFF;
GO

PRINT N'==============================================================================================';
PRINT N'== BÁO CÁO TỔNG HỢP DỮ LIỆU NẠP THÀNH CÔNG VÀO CSDL WMMS_DB                                ==';
PRINT N'==============================================================================================';

SELECT N'1. Roles (Vai trò RBAC)' AS [Phân hệ], COUNT(*) AS [Số bản ghi] FROM [roles]
UNION ALL SELECT N'2. Users (Người dùng hệ thống)', COUNT(*) FROM [users]
UNION ALL SELECT N'3. User Roles (Gán quyền)', COUNT(*) FROM [user_roles]
UNION ALL SELECT N'4. System Configs (Tham số hệ thống)', COUNT(*) FROM [system_configs]
UNION ALL SELECT N'5. Defect Catalogs (Danh mục lỗi QC)', COUNT(*) FROM [defect_catalogs]
UNION ALL SELECT N'6. Customers (Khách hàng B2B)', COUNT(*) FROM [customers]
UNION ALL SELECT N'7. Suppliers (Nhà cung cấp vật tư)', COUNT(*) FROM [suppliers]
UNION ALL SELECT N'8. Materials (Danh mục vật tư 4 nhóm)', COUNT(*) FROM [materials]
UNION ALL SELECT N'9. Products (SKU Thành phẩm)', COUNT(*) FROM [products]
UNION ALL SELECT N'10. Routing Templates & Stages', COUNT(*) FROM [routing_stages]
UNION ALL SELECT N'11. Sales Orders (Đơn hàng Master Case)', COUNT(*) FROM [sales_orders]
UNION ALL SELECT N'12. Order Items & Order Spaces', COUNT(*) FROM [order_spaces]
UNION ALL SELECT N'13. BOM Headers & BOM Details', COUNT(*) FROM [bom_details]
UNION ALL SELECT N'14. Inventory Stocks (Tồn kho 3 cấp)', COUNT(*) FROM [inventory_stocks]
UNION ALL SELECT N'15. Inventory Transactions (Sổ kho)', COUNT(*) FROM [inventory_transactions]
UNION ALL SELECT N'16. Audit Logs (Nhật ký hệ thống)', COUNT(*) FROM [audit_logs];
GO

PRINT N'==============================================================================================';
PRINT N'== HOÀN TẤT THÀNH CÔNG! HỆ THỐNG ĐÃ SẴN SÀNG CHO BUỔI DEMO INTERATION 1                     ==';
PRINT N'==============================================================================================';
