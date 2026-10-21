using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    customer_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    company_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    contact_person = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.customer_id);
                });

            migrationBuilder.CreateTable(
                name: "defect_catalogs",
                columns: table => new
                {
                    defect_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    defect_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    standard_stage = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    root_cause = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    recommended_action = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_defect_catalogs", x => x.defect_code);
                });

            migrationBuilder.CreateTable(
                name: "materials",
                columns: table => new
                {
                    material_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    material_code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    material_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    material_group = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    unit_of_measure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    dimension_spec = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    fsc_certified = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    safety_stock_qty = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_materials", x => x.material_id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    product_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    product_sku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    product_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    category_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    dimensions_cm = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    wood_species = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    finish_color = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    packing_spec = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    cbm_per_unit = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    image_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    cad_drawing_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.product_id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    role_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    role_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    role_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    permissions_json = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.role_id);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    supplier_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    supplier_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    supplier_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    supply_category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    contact_email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    lead_time_days = table.Column<int>(type: "int", nullable: false, defaultValue: 7),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.supplier_id);
                });

            migrationBuilder.CreateTable(
                name: "system_configs",
                columns: table => new
                {
                    config_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    config_key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    config_value = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    data_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_configs", x => x.config_id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    full_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    failed_login_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    locked_until = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_stocks",
                columns: table => new
                {
                    stock_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    material_id = table.Column<long>(type: "bigint", nullable: false),
                    physical_qty = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    allocated_qty = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    available_qty = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_stocks", x => x.stock_id);
                    table.ForeignKey(
                        name: "FK_inventory_stocks_materials_material_id",
                        column: x => x.material_id,
                        principalTable: "materials",
                        principalColumn: "material_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventory_transactions",
                columns: table => new
                {
                    transaction_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    material_id = table.Column<long>(type: "bigint", nullable: false),
                    tx_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    reference_code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_transactions", x => x.transaction_id);
                    table.ForeignKey(
                        name: "FK_inventory_transactions_materials_material_id",
                        column: x => x.material_id,
                        principalTable: "materials",
                        principalColumn: "material_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "routing_templates",
                columns: table => new
                {
                    routing_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    product_id = table.Column<long>(type: "bigint", nullable: false),
                    version_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routing_templates", x => x.routing_id);
                    table.ForeignKey(
                        name: "FK_routing_templates_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    log_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<long>(type: "bigint", nullable: true),
                    action_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    entity_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    before_state = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    after_state = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.log_id);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "consolidated_prs",
                columns: table => new
                {
                    pr_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    pr_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    aggregation_date = table.Column<DateTime>(type: "date", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: false),
                    approved_by = table.Column<long>(type: "bigint", nullable: true),
                    pr_status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    rejection_reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consolidated_prs", x => x.pr_id);
                    table.ForeignKey(
                        name: "FK_consolidated_prs_users_approved_by",
                        column: x => x.approved_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_consolidated_prs_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    token_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    token_hash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_revoked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.token_id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_orders",
                columns: table => new
                {
                    order_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    po_number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: false),
                    order_date = table.Column<DateTime>(type: "date", nullable: false),
                    etd_date = table.Column<DateTime>(type: "date", nullable: false),
                    total_cbm = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_orders", x => x.order_id);
                    table.ForeignKey(
                        name: "FK_sales_orders_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "customer_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_orders_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    role_id = table.Column<int>(type: "int", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "role_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "routing_stages",
                columns: table => new
                {
                    stage_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    routing_id = table.Column<long>(type: "bigint", nullable: false),
                    stage_sequence = table.Column<int>(type: "int", nullable: false),
                    stage_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    is_hold_point = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routing_stages", x => x.stage_id);
                    table.ForeignKey(
                        name: "FK_routing_stages_routing_templates_routing_id",
                        column: x => x.routing_id,
                        principalTable: "routing_templates",
                        principalColumn: "routing_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "consolidated_pr_items",
                columns: table => new
                {
                    pr_item_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    pr_id = table.Column<long>(type: "bigint", nullable: false),
                    material_id = table.Column<long>(type: "bigint", nullable: false),
                    total_shortage_qty = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    suggested_order_qty = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    source_order_ids_json = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consolidated_pr_items", x => x.pr_item_id);
                    table.ForeignKey(
                        name: "FK_consolidated_pr_items_consolidated_prs_pr_id",
                        column: x => x.pr_id,
                        principalTable: "consolidated_prs",
                        principalColumn: "pr_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_consolidated_pr_items_materials_material_id",
                        column: x => x.material_id,
                        principalTable: "materials",
                        principalColumn: "material_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                columns: table => new
                {
                    po_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    po_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    pr_id = table.Column<long>(type: "bigint", nullable: false),
                    supplier_id = table.Column<long>(type: "bigint", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: false),
                    issue_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    expected_arrival_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    total_amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    po_status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.po_id);
                    table.ForeignKey(
                        name: "FK_purchase_orders_consolidated_prs_pr_id",
                        column: x => x.pr_id,
                        principalTable: "consolidated_prs",
                        principalColumn: "pr_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "supplier_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    order_item_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    order_id = table.Column<long>(type: "bigint", nullable: false),
                    product_id = table.Column<long>(type: "bigint", nullable: false),
                    ordered_qty = table.Column<int>(type: "int", nullable: false),
                    unit_price_usd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    fsc_certified_req = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    length_mm = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    width_mm = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    height_mm = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    item_status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.order_item_id);
                    table.ForeignKey(
                        name: "FK_order_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_items_sales_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "sales_orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_spaces",
                columns: table => new
                {
                    space_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    order_id = table.Column<long>(type: "bigint", nullable: false),
                    space_key = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    space_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    drawing_folder_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    overall_progress_pct = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_spaces", x => x.space_id);
                    table.ForeignKey(
                        name: "FK_order_spaces_sales_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "sales_orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "goods_receipt_notes",
                columns: table => new
                {
                    grn_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    grn_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    po_id = table.Column<long>(type: "bigint", nullable: false),
                    warehouse_keeper_id = table.Column<long>(type: "bigint", nullable: false),
                    committed_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipt_notes", x => x.grn_id);
                    table.ForeignKey(
                        name: "FK_goods_receipt_notes_purchase_orders_po_id",
                        column: x => x.po_id,
                        principalTable: "purchase_orders",
                        principalColumn: "po_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_goods_receipt_notes_users_warehouse_keeper_id",
                        column: x => x.warehouse_keeper_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_details",
                columns: table => new
                {
                    po_detail_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    po_id = table.Column<long>(type: "bigint", nullable: false),
                    material_id = table.Column<long>(type: "bigint", nullable: false),
                    ordered_qty = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    received_qty = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    unit_price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_details", x => x.po_detail_id);
                    table.ForeignKey(
                        name: "FK_purchase_order_details_materials_material_id",
                        column: x => x.material_id,
                        principalTable: "materials",
                        principalColumn: "material_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_details_purchase_orders_po_id",
                        column: x => x.po_id,
                        principalTable: "purchase_orders",
                        principalColumn: "po_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "boms",
                columns: table => new
                {
                    bom_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    order_item_id = table.Column<long>(type: "bigint", nullable: false),
                    product_id = table.Column<long>(type: "bigint", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: false),
                    scrap_rate_percent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 3.00m),
                    bom_status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_boms", x => x.bom_id);
                    table.ForeignKey(
                        name: "FK_boms_order_items_order_item_id",
                        column: x => x.order_item_id,
                        principalTable: "order_items",
                        principalColumn: "order_item_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_boms_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_boms_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "material_allocations",
                columns: table => new
                {
                    allocation_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    order_item_id = table.Column<long>(type: "bigint", nullable: false),
                    material_id = table.Column<long>(type: "bigint", nullable: false),
                    reserved_qty = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    issued_qty = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    required_qty = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    allocation_status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    allocated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_allocations", x => x.allocation_id);
                    table.ForeignKey(
                        name: "FK_material_allocations_materials_material_id",
                        column: x => x.material_id,
                        principalTable: "materials",
                        principalColumn: "material_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_material_allocations_order_items_order_item_id",
                        column: x => x.order_item_id,
                        principalTable: "order_items",
                        principalColumn: "order_item_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "production_batches",
                columns: table => new
                {
                    batch_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    batch_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    order_item_id = table.Column<long>(type: "bigint", nullable: false),
                    parent_batch_id = table.Column<long>(type: "bigint", nullable: true),
                    batch_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    batch_qty = table.Column<int>(type: "int", nullable: false),
                    current_stage_seq = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    is_overdriver = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    hold_point_stage_seq = table.Column<int>(type: "int", nullable: true),
                    goods_issued_unlocked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    batch_status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_batches", x => x.batch_id);
                    table.ForeignKey(
                        name: "FK_production_batches_order_items_order_item_id",
                        column: x => x.order_item_id,
                        principalTable: "order_items",
                        principalColumn: "order_item_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_batches_production_batches_parent_batch_id",
                        column: x => x.parent_batch_id,
                        principalTable: "production_batches",
                        principalColumn: "batch_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bom_details",
                columns: table => new
                {
                    bom_detail_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    bom_id = table.Column<long>(type: "bigint", nullable: false),
                    material_id = table.Column<long>(type: "bigint", nullable: false),
                    net_qty_per_unit = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    gross_total_demand = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bom_details", x => x.bom_detail_id);
                    table.ForeignKey(
                        name: "FK_bom_details_boms_bom_id",
                        column: x => x.bom_id,
                        principalTable: "boms",
                        principalColumn: "bom_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bom_details_materials_material_id",
                        column: x => x.material_id,
                        principalTable: "materials",
                        principalColumn: "material_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "batch_routing_steps",
                columns: table => new
                {
                    batch_step_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    batch_id = table.Column<long>(type: "bigint", nullable: false),
                    stage_sequence = table.Column<int>(type: "int", nullable: false),
                    stage_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    is_hold_point = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    target_qty = table.Column<int>(type: "int", nullable: false),
                    passed_qty = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    defect_qty = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    step_status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    completed_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batch_routing_steps", x => x.batch_step_id);
                    table.ForeignKey(
                        name: "FK_batch_routing_steps_production_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "production_batches",
                        principalColumn: "batch_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "overdrive_logs",
                columns: table => new
                {
                    overdrive_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    batch_id = table.Column<long>(type: "bigint", nullable: false),
                    requested_by = table.Column<long>(type: "bigint", nullable: false),
                    approved_by = table.Column<long>(type: "bigint", nullable: false),
                    justification = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    hold_point_stage_seq = table.Column<int>(type: "int", nullable: false),
                    approved_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_overdrive_logs", x => x.overdrive_id);
                    table.ForeignKey(
                        name: "FK_overdrive_logs_production_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "production_batches",
                        principalColumn: "batch_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_overdrive_logs_users_approved_by",
                        column: x => x.approved_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_overdrive_logs_users_requested_by",
                        column: x => x.requested_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "qc_inspections",
                columns: table => new
                {
                    qc_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    batch_step_id = table.Column<long>(type: "bigint", nullable: false),
                    inspector_id = table.Column<long>(type: "bigint", nullable: false),
                    inspected_qty = table.Column<int>(type: "int", nullable: false),
                    pass_qty = table.Column<int>(type: "int", nullable: false),
                    fail_qty = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    result_status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    inspected_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qc_inspections", x => x.qc_id);
                    table.ForeignKey(
                        name: "FK_qc_inspections_batch_routing_steps_batch_step_id",
                        column: x => x.batch_step_id,
                        principalTable: "batch_routing_steps",
                        principalColumn: "batch_step_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_qc_inspections_users_inspector_id",
                        column: x => x.inspector_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "defect_logs",
                columns: table => new
                {
                    defect_log_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    qc_id = table.Column<long>(type: "bigint", nullable: false),
                    defect_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    defect_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    defect_qty = table.Column<int>(type: "int", nullable: false),
                    disposition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    spawned_sub_batch_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_defect_logs", x => x.defect_log_id);
                    table.ForeignKey(
                        name: "FK_defect_logs_defect_catalogs_defect_code",
                        column: x => x.defect_code,
                        principalTable: "defect_catalogs",
                        principalColumn: "defect_code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_defect_logs_production_batches_spawned_sub_batch_id",
                        column: x => x.spawned_sub_batch_id,
                        principalTable: "production_batches",
                        principalColumn: "batch_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_defect_logs_qc_inspections_qc_id",
                        column: x => x.qc_id,
                        principalTable: "qc_inspections",
                        principalColumn: "qc_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_user_id",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_batch_routing_steps_batch_id",
                table: "batch_routing_steps",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_bom_details_bom_id",
                table: "bom_details",
                column: "bom_id");

            migrationBuilder.CreateIndex(
                name: "IX_bom_details_material_id",
                table: "bom_details",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "IX_boms_created_by",
                table: "boms",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_boms_order_item_id",
                table: "boms",
                column: "order_item_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_boms_product_id",
                table: "boms",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_consolidated_pr_items_material_id",
                table: "consolidated_pr_items",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "IX_consolidated_pr_items_pr_id",
                table: "consolidated_pr_items",
                column: "pr_id");

            migrationBuilder.CreateIndex(
                name: "IX_consolidated_prs_approved_by",
                table: "consolidated_prs",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "IX_consolidated_prs_created_by",
                table: "consolidated_prs",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_consolidated_prs_pr_code",
                table: "consolidated_prs",
                column: "pr_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_customer_code",
                table: "customers",
                column: "customer_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_defect_logs_defect_code",
                table: "defect_logs",
                column: "defect_code");

            migrationBuilder.CreateIndex(
                name: "IX_defect_logs_qc_id",
                table: "defect_logs",
                column: "qc_id");

            migrationBuilder.CreateIndex(
                name: "IX_defect_logs_spawned_sub_batch_id",
                table: "defect_logs",
                column: "spawned_sub_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_notes_grn_code",
                table: "goods_receipt_notes",
                column: "grn_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_notes_po_id",
                table: "goods_receipt_notes",
                column: "po_id");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_notes_warehouse_keeper_id",
                table: "goods_receipt_notes",
                column: "warehouse_keeper_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_stocks_material_id",
                table: "inventory_stocks",
                column: "material_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_material_id",
                table: "inventory_transactions",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "IX_material_allocations_material_id",
                table: "material_allocations",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "IX_material_allocations_order_item_id",
                table: "material_allocations",
                column: "order_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_materials_material_code",
                table: "materials",
                column: "material_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_items_order_id",
                table: "order_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_product_id",
                table: "order_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_spaces_order_id",
                table: "order_spaces",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_spaces_space_key",
                table: "order_spaces",
                column: "space_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_overdrive_logs_approved_by",
                table: "overdrive_logs",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "IX_overdrive_logs_batch_id",
                table: "overdrive_logs",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_overdrive_logs_requested_by",
                table: "overdrive_logs",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "IX_production_batches_batch_code",
                table: "production_batches",
                column: "batch_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_production_batches_order_item_id",
                table: "production_batches",
                column: "order_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_production_batches_parent_batch_id",
                table: "production_batches",
                column: "parent_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_product_sku",
                table: "products",
                column: "product_sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_details_material_id",
                table: "purchase_order_details",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_details_po_id",
                table: "purchase_order_details",
                column: "po_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_created_by",
                table: "purchase_orders",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_po_code",
                table: "purchase_orders",
                column: "po_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_pr_id",
                table: "purchase_orders",
                column: "pr_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_supplier_id",
                table: "purchase_orders",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_qc_inspections_batch_step_id",
                table: "qc_inspections",
                column: "batch_step_id");

            migrationBuilder.CreateIndex(
                name: "IX_qc_inspections_inspector_id",
                table: "qc_inspections",
                column: "inspector_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_role_code",
                table: "roles",
                column: "role_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_routing_stages_routing_id",
                table: "routing_stages",
                column: "routing_id");

            migrationBuilder.CreateIndex(
                name: "IX_routing_templates_product_id",
                table: "routing_templates",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_created_by",
                table: "sales_orders",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_customer_id",
                table: "sales_orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_po_number",
                table: "sales_orders",
                column: "po_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_status_etd",
                table: "sales_orders",
                columns: new[] { "status", "etd_date" });

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_supplier_code",
                table: "suppliers",
                column: "supplier_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_system_configs_config_key",
                table: "system_configs",
                column: "config_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_role_id",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "bom_details");

            migrationBuilder.DropTable(
                name: "consolidated_pr_items");

            migrationBuilder.DropTable(
                name: "defect_logs");

            migrationBuilder.DropTable(
                name: "goods_receipt_notes");

            migrationBuilder.DropTable(
                name: "inventory_stocks");

            migrationBuilder.DropTable(
                name: "inventory_transactions");

            migrationBuilder.DropTable(
                name: "material_allocations");

            migrationBuilder.DropTable(
                name: "order_spaces");

            migrationBuilder.DropTable(
                name: "overdrive_logs");

            migrationBuilder.DropTable(
                name: "purchase_order_details");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "routing_stages");

            migrationBuilder.DropTable(
                name: "system_configs");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "boms");

            migrationBuilder.DropTable(
                name: "defect_catalogs");

            migrationBuilder.DropTable(
                name: "qc_inspections");

            migrationBuilder.DropTable(
                name: "materials");

            migrationBuilder.DropTable(
                name: "purchase_orders");

            migrationBuilder.DropTable(
                name: "routing_templates");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "batch_routing_steps");

            migrationBuilder.DropTable(
                name: "consolidated_prs");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "production_batches");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "sales_orders");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
