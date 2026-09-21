using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace AssetsManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "assets");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:ltree", ",,");

            migrationBuilder.CreateTable(
                name: "asset_class",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_class", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "asset_status",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_terminal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    allows_assignment = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_status", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "attribute_group",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: true),
                    is_collapsible = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attribute_group", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "currency",
                schema: "assets",
                columns: table => new
                {
                    code = table.Column<string>(type: "char(3)", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    minor_units = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)2),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_currency", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "depreciation_method",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_depreciation_method", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "disposal_method",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    requires_value = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_disposal_method", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_categories",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lifecycle_event_type",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lifecycle_event_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "location",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    location_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    path = table.Column<string>(type: "ltree", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_location", x => x.id);
                    table.ForeignKey(
                        name: "fk_location_location_parent_location_id",
                        column: x => x.parent_location_id,
                        principalSchema: "assets",
                        principalTable: "location",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "manufacturer",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    contact_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    address_building = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_latitude = table.Column<double>(type: "double precision", precision: 10, scale: 7, nullable: true),
                    address_longitude = table.Column<double>(type: "double precision", precision: 10, scale: 7, nullable: true),
                    address_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address_state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_street = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_manufacturer", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "option_set",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_option_set", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "person",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    address_building = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_latitude = table.Column<double>(type: "double precision", precision: 10, scale: 7, nullable: true),
                    address_longitude = table.Column<double>(type: "double precision", precision: 10, scale: 7, nullable: true),
                    address_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address_state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_street = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_person", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "production_order",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    production_order_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    planned_start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    planned_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actual_start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actual_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    super_visor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_approved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_production_order", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purchases",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Draft"),
                    expected_delivery_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    currency_value = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    delivery_address_building = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    delivery_address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    delivery_address_country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    delivery_address_latitude = table.Column<double>(type: "double precision", nullable: true),
                    delivery_address_longitude = table.Column<double>(type: "double precision", nullable: true),
                    delivery_address_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    delivery_address_state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    delivery_address_street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    discount_amount_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    discount_amount_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    payment_term_advance_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    payment_term_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    payment_term_due_days = table.Column<int>(type: "integer", nullable: false),
                    sub_total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    sub_total_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    tax_amount_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tax_amount_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    total_amount_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_amount_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchases", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scraps",
                schema: "assets",
                columns: table => new
                {
                    inventory_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    price_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    price_currency = table.Column<string>(type: "text", nullable: false),
                    id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scraps", x => x.inventory_item_id);
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    warehouse_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Main"),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Active"),
                    manager_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_number = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    address_building = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address_country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address_latitude = table.Column<double>(type: "double precision", nullable: true),
                    address_longitude = table.Column<double>(type: "double precision", nullable: true),
                    address_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    address_state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_warehouses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "asset_type",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_depreciable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    requires_location = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    requires_custodian = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    display_order = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_type", x => x.id);
                    table.UniqueConstraint("ak_asset_types_id_asset_class_id", x => new { x.id, x.asset_class_id });
                    table.ForeignKey(
                        name: "fk_asset_type_asset_class_asset_class_id",
                        column: x => x.asset_class_id,
                        principalSchema: "assets",
                        principalTable: "asset_class",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_types",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    inventory_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_types", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventory_types_inventory_categories_inventory_category_id",
                        column: x => x.inventory_category_id,
                        principalSchema: "assets",
                        principalTable: "inventory_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attribute_definition",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    data_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    option_set_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference_entity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    numeric_precision = table.Column<int>(type: "integer", nullable: true),
                    numeric_scale = table.Column<int>(type: "integer", nullable: true),
                    min_number = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    max_number = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    min_length = table.Column<int>(type: "integer", nullable: true),
                    max_length = table.Column<int>(type: "integer", nullable: true),
                    min_date = table.Column<DateOnly>(type: "date", nullable: true),
                    max_date = table.Column<DateOnly>(type: "date", nullable: true),
                    regex_pattern = table.Column<string>(type: "text", nullable: true),
                    is_unique_per_category = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    validation_message = table.Column<string>(type: "text", nullable: true),
                    is_multi_value = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_pii = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attribute_definition", x => x.id);
                    table.CheckConstraint("ck_attribute_definition_date_range", "min_date IS NULL OR max_date IS NULL OR min_date <= max_date");
                    table.CheckConstraint("ck_attribute_definition_length_range", "min_length IS NULL OR max_length IS NULL OR min_length <= max_length");
                    table.CheckConstraint("ck_attribute_definition_number_range", "min_number IS NULL OR max_number IS NULL OR min_number <= max_number");
                    table.CheckConstraint("ck_attribute_definition_option_set", "(data_type NOT IN ('Select','MultiSelect')) OR option_set_id IS NOT NULL");
                    table.CheckConstraint("ck_attribute_definition_reference_entity", "(data_type <> 'Reference') OR reference_entity IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_attribute_definition_option_set_option_set_id",
                        column: x => x.option_set_id,
                        principalSchema: "assets",
                        principalTable: "option_set",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "option_set_value",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    option_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_value_id = table.Column<Guid>(type: "uuid", nullable: true),
                    value = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    label = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_option_set_value", x => x.id);
                    table.ForeignKey(
                        name: "fk_option_set_value_option_set_option_set_id",
                        column: x => x.option_set_id,
                        principalSchema: "assets",
                        principalTable: "option_set",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_option_set_value_option_set_value_parent_value_id",
                        column: x => x.parent_value_id,
                        principalSchema: "assets",
                        principalTable: "option_set_value",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finished_good_item",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    production_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_cost_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_cost_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    unit_cost_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    unit_cost_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_finished_good_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_finished_good_item_production_order_production_order_id",
                        column: x => x.production_order_id,
                        principalSchema: "assets",
                        principalTable: "production_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "material_consumption",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    inventory_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    production_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_cost_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_cost_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    unit_cost_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    unit_cost_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_material_consumption", x => x.id);
                    table.ForeignKey(
                        name: "fk_material_consumption_production_order_production_order_id",
                        column: x => x.production_order_id,
                        principalSchema: "assets",
                        principalTable: "production_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_in_progress",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    production_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_stage = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "NotStarted"),
                    progress_percentage = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_in_progress", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_in_progress_production_order_production_order_id",
                        column: x => x.production_order_id,
                        principalSchema: "assets",
                        principalTable: "production_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset_category",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    path = table.Column<string>(type: "ltree", nullable: false),
                    depth = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_leaf = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    display_order = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_category", x => x.id);
                    table.UniqueConstraint("ak_asset_category_id_asset_class_id", x => new { x.id, x.asset_class_id });
                    table.ForeignKey(
                        name: "fk_asset_category_asset_category_parent_category_id",
                        column: x => x.parent_category_id,
                        principalSchema: "assets",
                        principalTable: "asset_category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asset_category_asset_class_asset_class_id",
                        column: x => x.asset_class_id,
                        principalSchema: "assets",
                        principalTable: "asset_class",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asset_category_asset_types_asset_type_id",
                        column: x => x.asset_type_id,
                        principalSchema: "assets",
                        principalTable: "asset_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_items",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    file_url = table.Column<string>(type: "text", nullable: true),
                    inventory_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_owner_ship_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Purchase"),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Available"),
                    unit_of_measure_unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    unit_of_measure_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventory_items_inventory_types_inventory_type_id",
                        column: x => x.inventory_type_id,
                        principalSchema: "assets",
                        principalTable: "inventory_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    ownership = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    asset_class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    custodian_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    serial_number = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    rfid_tag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    attributes_validated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    extra_attributes = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset", x => x.id);
                    table.ForeignKey(
                        name: "fk_asset_asset_category_category_id_asset_class_id",
                        columns: x => new { x.category_id, x.asset_class_id },
                        principalSchema: "assets",
                        principalTable: "asset_category",
                        principalColumns: new[] { "id", "asset_class_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asset_asset_class_asset_class_id",
                        column: x => x.asset_class_id,
                        principalSchema: "assets",
                        principalTable: "asset_class",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asset_asset_parent_asset_id",
                        column: x => x.parent_asset_id,
                        principalSchema: "assets",
                        principalTable: "asset",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asset_asset_status_status_id",
                        column: x => x.status_id,
                        principalSchema: "assets",
                        principalTable: "asset_status",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asset_asset_types_asset_type_id_asset_class_id",
                        columns: x => new { x.asset_type_id, x.asset_class_id },
                        principalSchema: "assets",
                        principalTable: "asset_type",
                        principalColumns: new[] { "id", "asset_class_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asset_location_current_location_id",
                        column: x => x.current_location_id,
                        principalSchema: "assets",
                        principalTable: "location",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_stocks",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    available_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    reserved_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_stocks", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventory_stocks_inventory_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "assets",
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_inventory_stocks_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "assets",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_lines",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordered_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    received_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    line_total = table.Column<decimal>(type: "numeric", nullable: false),
                    remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    file_url = table.Column<string>(type: "text", nullable: true),
                    purchase_id1 = table.Column<Guid>(type: "uuid", nullable: true),
                    unit_of_measure_unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    unit_of_measure_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    unit_price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchase_lines_inventory_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "assets",
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_purchase_lines_purchases_purchase_id",
                        column: x => x.purchase_id,
                        principalSchema: "assets",
                        principalTable: "purchases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_purchase_lines_purchases_purchase_id1",
                        column: x => x.purchase_id1,
                        principalSchema: "assets",
                        principalTable: "purchases",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "asset_acquisition",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    acquisition_date = table.Column<DateOnly>(type: "date", nullable: false),
                    acquisition_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    exchange_rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    base_currency_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purchase_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    acquisition_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    warranty_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    warranty_expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_acquisition", x => x.id);
                    table.CheckConstraint("ck_asset_acquisition_cost", "acquisition_cost >= 0");
                    table.CheckConstraint("ck_asset_acquisition_warranty", "warranty_expiry_date IS NULL OR warranty_expiry_date >= acquisition_date");
                    table.ForeignKey(
                        name: "fk_asset_acquisition_asset_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "assets",
                        principalTable: "asset",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asset_acquisition_currency_currency_code",
                        column: x => x.currency_code,
                        principalSchema: "assets",
                        principalTable: "currency",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "asset_assignment",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    from_custodian_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_custodian_id = table.Column<Guid>(type: "uuid", nullable: true),
                    from_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assignment_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expected_return_date = table.Column<DateOnly>(type: "date", nullable: true),
                    actual_return_date = table.Column<DateOnly>(type: "date", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_assignment", x => x.id);
                    table.ForeignKey(
                        name: "fk_asset_assignment_asset_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "assets",
                        principalTable: "asset",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asset_assignment_location_from_location_id",
                        column: x => x.from_location_id,
                        principalSchema: "assets",
                        principalTable: "location",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asset_assignment_location_to_location_id",
                        column: x => x.to_location_id,
                        principalSchema: "assets",
                        principalTable: "location",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "asset_attachment",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attachment_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    stored_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    storage_path = table.Column<string>(type: "text", nullable: false),
                    checksum_sha256 = table.Column<string>(type: "char(64)", nullable: true),
                    is_primary_image = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_attachment", x => x.id);
                    table.ForeignKey(
                        name: "fk_asset_attachment_asset_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "assets",
                        principalTable: "asset",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset_attribute_history",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attribute_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attribute_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    old_value = table.Column<string>(type: "jsonb", nullable: true),
                    new_value = table.Column<string>(type: "jsonb", nullable: true),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    change_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_attribute_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_asset_attribute_history_asset_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "assets",
                        principalTable: "asset",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asset_attribute_history_attribute_definition_attribute_defi",
                        column: x => x.attribute_definition_id,
                        principalSchema: "assets",
                        principalTable: "attribute_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset_attribute_value",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attribute_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attribute_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value_text = table.Column<string>(type: "text", nullable: true),
                    value_number = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    value_boolean = table.Column<bool>(type: "boolean", nullable: true),
                    value_date = table.Column<DateOnly>(type: "date", nullable: true),
                    value_datetime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    value_json = table.Column<string>(type: "jsonb", nullable: true),
                    option_value_id = table.Column<Guid>(type: "uuid", nullable: true),
                    value_index = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true)
                        .Annotation("Npgsql:TsVectorConfig", "english")
                        .Annotation("Npgsql:TsVectorProperties", new[] { "value_text" }),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_attribute_value", x => x.id);
                    table.ForeignKey(
                        name: "fk_asset_attribute_value_asset_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "assets",
                        principalTable: "asset",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asset_attribute_value_attribute_definition_attribute_defini",
                        column: x => x.attribute_definition_id,
                        principalSchema: "assets",
                        principalTable: "attribute_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asset_attribute_value_option_set_value_option_value_id",
                        column: x => x.option_value_id,
                        principalSchema: "assets",
                        principalTable: "option_set_value",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "asset_depreciation_schedule",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method_id = table.Column<Guid>(type: "uuid", nullable: false),
                    useful_life_months = table.Column<int>(type: "integer", nullable: false),
                    salvage_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    depreciable_base = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    declining_rate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_depreciation_schedule", x => x.id);
                    table.CheckConstraint("ck_asset_depreciation_schedule_life", "useful_life_months > 0");
                    table.CheckConstraint("ck_asset_depreciation_schedule_salvage", "salvage_value >= 0");
                    table.ForeignKey(
                        name: "fk_asset_depreciation_schedule_asset_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "assets",
                        principalTable: "asset",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asset_depreciation_schedule_depreciation_method_method_id",
                        column: x => x.method_id,
                        principalSchema: "assets",
                        principalTable: "depreciation_method",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "asset_disposal",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    disposal_date = table.Column<DateOnly>(type: "date", nullable: false),
                    disposal_method_id = table.Column<Guid>(type: "uuid", nullable: false),
                    disposal_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    currency_code = table.Column<string>(type: "char(3)", nullable: true),
                    net_book_value_at_disposal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    gain_loss = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    buyer_info = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_disposal", x => x.id);
                    table.CheckConstraint("ck_asset_disposal_currency", "disposal_value IS NULL OR currency_code IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_asset_disposal_asset_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "assets",
                        principalTable: "asset",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asset_disposal_currency_currency_code",
                        column: x => x.currency_code,
                        principalSchema: "assets",
                        principalTable: "currency",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asset_disposal_disposal_method_disposal_method_id",
                        column: x => x.disposal_method_id,
                        principalSchema: "assets",
                        principalTable: "disposal_method",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "asset_lifecycle_event",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    from_status_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_status_id = table.Column<Guid>(type: "uuid", nullable: true),
                    performed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    details = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_lifecycle_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_asset_lifecycle_event_asset_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "assets",
                        principalTable: "asset",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asset_lifecycle_event_asset_status_from_status_id",
                        column: x => x.from_status_id,
                        principalSchema: "assets",
                        principalTable: "asset_status",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asset_lifecycle_event_asset_status_to_status_id",
                        column: x => x.to_status_id,
                        principalSchema: "assets",
                        principalTable: "asset_status",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asset_lifecycle_event_lifecycle_event_type_event_type_id",
                        column: x => x.event_type_id,
                        principalSchema: "assets",
                        principalTable: "lifecycle_event_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "asset_valuation_history",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valuation_date = table.Column<DateOnly>(type: "date", nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    valuation_method = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    valued_by = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_valuation_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_asset_valuation_history_asset_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "assets",
                        principalTable: "asset",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asset_valuation_history_currency_currency_code",
                        column: x => x.currency_code,
                        principalSchema: "assets",
                        principalTable: "currency",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attribute_assignment",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    attribute_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    asset_class_id = table.Column<Guid>(type: "uuid", nullable: true),
                    asset_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attribute_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    label_override = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    is_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_readonly = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_searchable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_filterable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_visible_in_list = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    inherit_to_children = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    default_value = table.Column<string>(type: "jsonb", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: true),
                    depends_on_assignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    depends_on_value = table.Column<string>(type: "jsonb", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attribute_assignment", x => x.id);
                    table.CheckConstraint("ck_attribute_assignment_scope", "(scope = 'AssetClass' AND asset_class_id IS NOT NULL AND asset_type_id IS NULL AND category_id IS NULL AND asset_id IS NULL) OR (scope = 'AssetType'  AND asset_type_id  IS NOT NULL AND asset_class_id IS NULL AND category_id IS NULL AND asset_id IS NULL) OR (scope = 'Category'   AND category_id    IS NOT NULL AND asset_class_id IS NULL AND asset_type_id IS NULL AND asset_id IS NULL) OR (scope = 'Asset'      AND asset_id       IS NOT NULL AND asset_class_id IS NULL AND asset_type_id IS NULL AND category_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_attribute_assignment_asset_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "assets",
                        principalTable: "asset",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_attribute_assignment_asset_category_category_id",
                        column: x => x.category_id,
                        principalSchema: "assets",
                        principalTable: "asset_category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_attribute_assignment_asset_class_asset_class_id",
                        column: x => x.asset_class_id,
                        principalSchema: "assets",
                        principalTable: "asset_class",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_attribute_assignment_asset_type_asset_type_id",
                        column: x => x.asset_type_id,
                        principalSchema: "assets",
                        principalTable: "asset_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_attribute_assignment_attribute_assignment_depends_on_assign",
                        column: x => x.depends_on_assignment_id,
                        principalSchema: "assets",
                        principalTable: "attribute_assignment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_attribute_assignment_attribute_definition_attribute_definit",
                        column: x => x.attribute_definition_id,
                        principalSchema: "assets",
                        principalTable: "attribute_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_attribute_assignment_attribute_group_attribute_group_id",
                        column: x => x.attribute_group_id,
                        principalSchema: "assets",
                        principalTable: "attribute_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "asset_depreciation_entry",
                schema: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    opening_book_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    depreciation_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    accumulated_depreciation = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    book_value_after = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    posted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    posted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    posted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reversed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_depreciation_entry", x => x.id);
                    table.CheckConstraint("ck_asset_depreciation_entry_amount", "depreciation_amount >= 0");
                    table.CheckConstraint("ck_asset_depreciation_entry_period", "period_end > period_start");
                    table.ForeignKey(
                        name: "fk_asset_depreciation_entry_asset_depreciation_schedule_schedu",
                        column: x => x.schedule_id,
                        principalSchema: "assets",
                        principalTable: "asset_depreciation_schedule",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_asset_asset_class_id",
                schema: "assets",
                table: "asset",
                column: "asset_class_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_asset_code",
                schema: "assets",
                table: "asset",
                column: "asset_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_asset_type_id",
                schema: "assets",
                table: "asset",
                column: "asset_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_asset_type_id_asset_class_id",
                schema: "assets",
                table: "asset",
                columns: new[] { "asset_type_id", "asset_class_id" });

            migrationBuilder.CreateIndex(
                name: "ix_asset_barcode",
                schema: "assets",
                table: "asset",
                column: "barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_category_id",
                schema: "assets",
                table: "asset",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_category_id_asset_class_id",
                schema: "assets",
                table: "asset",
                columns: new[] { "category_id", "asset_class_id" });

            migrationBuilder.CreateIndex(
                name: "ix_asset_category_id_serial_number",
                schema: "assets",
                table: "asset",
                columns: new[] { "category_id", "serial_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_current_location_id",
                schema: "assets",
                table: "asset",
                column: "current_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_custodian_id",
                schema: "assets",
                table: "asset",
                column: "custodian_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_department_id",
                schema: "assets",
                table: "asset",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_extra_attributes",
                schema: "assets",
                table: "asset",
                column: "extra_attributes")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "jsonb_path_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_asset_parent_asset_id",
                schema: "assets",
                table: "asset",
                column: "parent_asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_rfid_tag",
                schema: "assets",
                table: "asset",
                column: "rfid_tag",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_status_id",
                schema: "assets",
                table: "asset",
                column: "status_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_acquisition_asset_id",
                schema: "assets",
                table: "asset_acquisition",
                column: "asset_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_acquisition_currency_code",
                schema: "assets",
                table: "asset_acquisition",
                column: "currency_code");

            migrationBuilder.CreateIndex(
                name: "ix_asset_assignment_asset_id",
                schema: "assets",
                table: "asset_assignment",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_assignment_asset_id_assignment_date",
                schema: "assets",
                table: "asset_assignment",
                columns: new[] { "asset_id", "assignment_date" });

            migrationBuilder.CreateIndex(
                name: "ix_asset_assignment_assignment_date",
                schema: "assets",
                table: "asset_assignment",
                column: "assignment_date");

            migrationBuilder.CreateIndex(
                name: "ix_asset_assignment_from_location_id",
                schema: "assets",
                table: "asset_assignment",
                column: "from_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_assignment_to_location_id",
                schema: "assets",
                table: "asset_assignment",
                column: "to_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_attachment_asset_id",
                schema: "assets",
                table: "asset_attachment",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_attachment_asset_id_attachment_type",
                schema: "assets",
                table: "asset_attachment",
                columns: new[] { "asset_id", "attachment_type" });

            migrationBuilder.CreateIndex(
                name: "ix_asset_attribute_history_asset_id_changed_at",
                schema: "assets",
                table: "asset_attribute_history",
                columns: new[] { "asset_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_asset_attribute_history_attribute_definition_id",
                schema: "assets",
                table: "asset_attribute_history",
                column: "attribute_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_attribute_value_asset_id",
                schema: "assets",
                table: "asset_attribute_value",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_attribute_value_asset_id_attribute_definition_id_valu",
                schema: "assets",
                table: "asset_attribute_value",
                columns: new[] { "asset_id", "attribute_definition_id", "value_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_attribute_value_attribute_definition_id_option_value_",
                schema: "assets",
                table: "asset_attribute_value",
                columns: new[] { "attribute_definition_id", "option_value_id" });

            migrationBuilder.CreateIndex(
                name: "ix_asset_attribute_value_attribute_definition_id_value_date",
                schema: "assets",
                table: "asset_attribute_value",
                columns: new[] { "attribute_definition_id", "value_date" });

            migrationBuilder.CreateIndex(
                name: "ix_asset_attribute_value_attribute_definition_id_value_number",
                schema: "assets",
                table: "asset_attribute_value",
                columns: new[] { "attribute_definition_id", "value_number" });

            migrationBuilder.CreateIndex(
                name: "ix_asset_attribute_value_attribute_definition_id_value_text",
                schema: "assets",
                table: "asset_attribute_value",
                columns: new[] { "attribute_definition_id", "value_text" });

            migrationBuilder.CreateIndex(
                name: "ix_asset_attribute_value_option_value_id",
                schema: "assets",
                table: "asset_attribute_value",
                column: "option_value_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_attribute_value_search_vector",
                schema: "assets",
                table: "asset_attribute_value",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_asset_category_asset_class_id_code",
                schema: "assets",
                table: "asset_category",
                columns: new[] { "asset_class_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_category_asset_type_id",
                schema: "assets",
                table: "asset_category",
                column: "asset_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_category_parent_category_id",
                schema: "assets",
                table: "asset_category",
                column: "parent_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_category_path",
                schema: "assets",
                table: "asset_category",
                column: "path")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_asset_class_code",
                schema: "assets",
                table: "asset_class",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_depreciation_entry_period_start",
                schema: "assets",
                table: "asset_depreciation_entry",
                column: "period_start");

            migrationBuilder.CreateIndex(
                name: "ix_asset_depreciation_entry_schedule_id",
                schema: "assets",
                table: "asset_depreciation_entry",
                column: "schedule_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_depreciation_entry_schedule_id_period_start_period_end",
                schema: "assets",
                table: "asset_depreciation_entry",
                columns: new[] { "schedule_id", "period_start", "period_end" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_depreciation_schedule_method_id",
                schema: "assets",
                table: "asset_depreciation_schedule",
                column: "method_id");

            migrationBuilder.CreateIndex(
                name: "ux_asset_depreciation_schedule_active",
                schema: "assets",
                table: "asset_depreciation_schedule",
                column: "asset_id",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_asset_disposal_asset_id",
                schema: "assets",
                table: "asset_disposal",
                column: "asset_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_disposal_currency_code",
                schema: "assets",
                table: "asset_disposal",
                column: "currency_code");

            migrationBuilder.CreateIndex(
                name: "ix_asset_disposal_disposal_method_id",
                schema: "assets",
                table: "asset_disposal",
                column: "disposal_method_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_lifecycle_event_asset_id",
                schema: "assets",
                table: "asset_lifecycle_event",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_lifecycle_event_details",
                schema: "assets",
                table: "asset_lifecycle_event",
                column: "details")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_asset_lifecycle_event_event_date",
                schema: "assets",
                table: "asset_lifecycle_event",
                column: "event_date");

            migrationBuilder.CreateIndex(
                name: "ix_asset_lifecycle_event_event_type_id",
                schema: "assets",
                table: "asset_lifecycle_event",
                column: "event_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_lifecycle_event_from_status_id",
                schema: "assets",
                table: "asset_lifecycle_event",
                column: "from_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_lifecycle_event_to_status_id",
                schema: "assets",
                table: "asset_lifecycle_event",
                column: "to_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_status_code",
                schema: "assets",
                table: "asset_status",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_type_asset_class_id_code",
                schema: "assets",
                table: "asset_type",
                columns: new[] { "asset_class_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_valuation_history_asset_id",
                schema: "assets",
                table: "asset_valuation_history",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_valuation_history_asset_id_valuation_date",
                schema: "assets",
                table: "asset_valuation_history",
                columns: new[] { "asset_id", "valuation_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_valuation_history_currency_code",
                schema: "assets",
                table: "asset_valuation_history",
                column: "currency_code");

            migrationBuilder.CreateIndex(
                name: "ix_asset_valuation_history_valuation_date",
                schema: "assets",
                table: "asset_valuation_history",
                column: "valuation_date");

            migrationBuilder.CreateIndex(
                name: "ix_attribute_assignment_asset_class_id_attribute_definition_id",
                schema: "assets",
                table: "attribute_assignment",
                columns: new[] { "asset_class_id", "attribute_definition_id" },
                unique: true,
                filter: "asset_class_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_attribute_assignment_asset_id",
                schema: "assets",
                table: "attribute_assignment",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_attribute_assignment_asset_id_attribute_definition_id",
                schema: "assets",
                table: "attribute_assignment",
                columns: new[] { "asset_id", "attribute_definition_id" },
                unique: true,
                filter: "asset_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_attribute_assignment_asset_type_id_attribute_definition_id",
                schema: "assets",
                table: "attribute_assignment",
                columns: new[] { "asset_type_id", "attribute_definition_id" },
                unique: true,
                filter: "asset_type_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_attribute_assignment_attribute_definition_id",
                schema: "assets",
                table: "attribute_assignment",
                column: "attribute_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_attribute_assignment_attribute_group_id",
                schema: "assets",
                table: "attribute_assignment",
                column: "attribute_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_attribute_assignment_category_id_attribute_definition_id",
                schema: "assets",
                table: "attribute_assignment",
                columns: new[] { "category_id", "attribute_definition_id" },
                unique: true,
                filter: "category_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_attribute_assignment_depends_on_assignment_id",
                schema: "assets",
                table: "attribute_assignment",
                column: "depends_on_assignment_id");

            migrationBuilder.CreateIndex(
                name: "ix_attribute_assignment_scope_asset_class_id",
                schema: "assets",
                table: "attribute_assignment",
                columns: new[] { "scope", "asset_class_id" });

            migrationBuilder.CreateIndex(
                name: "ix_attribute_assignment_scope_asset_type_id",
                schema: "assets",
                table: "attribute_assignment",
                columns: new[] { "scope", "asset_type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_attribute_assignment_scope_category_id",
                schema: "assets",
                table: "attribute_assignment",
                columns: new[] { "scope", "category_id" });

            migrationBuilder.CreateIndex(
                name: "ix_attribute_definition_code",
                schema: "assets",
                table: "attribute_definition",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attribute_definition_data_type",
                schema: "assets",
                table: "attribute_definition",
                column: "data_type");

            migrationBuilder.CreateIndex(
                name: "ix_attribute_definition_option_set_id",
                schema: "assets",
                table: "attribute_definition",
                column: "option_set_id");

            migrationBuilder.CreateIndex(
                name: "ix_attribute_group_code",
                schema: "assets",
                table: "attribute_group",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_depreciation_method_code",
                schema: "assets",
                table: "depreciation_method",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_disposal_method_code",
                schema: "assets",
                table: "disposal_method",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_finished_good_item_production_order_id",
                schema: "assets",
                table: "finished_good_item",
                column: "production_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_items_inventory_type_id",
                schema: "assets",
                table: "inventory_items",
                column: "inventory_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_stocks_item_id",
                schema: "assets",
                table: "inventory_stocks",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_stocks_warehouse_id",
                schema: "assets",
                table: "inventory_stocks",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_types_inventory_category_id",
                schema: "assets",
                table: "inventory_types",
                column: "inventory_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_lifecycle_event_type_code",
                schema: "assets",
                table: "lifecycle_event_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_location_code",
                schema: "assets",
                table: "location",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_location_parent_location_id",
                schema: "assets",
                table: "location",
                column: "parent_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_location_path",
                schema: "assets",
                table: "location",
                column: "path")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_manufacturer_name",
                schema: "assets",
                table: "manufacturer",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_material_consumption_production_order_id",
                schema: "assets",
                table: "material_consumption",
                column: "production_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_option_set_code",
                schema: "assets",
                table: "option_set",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_option_set_value_option_set_id",
                schema: "assets",
                table: "option_set_value",
                column: "option_set_id");

            migrationBuilder.CreateIndex(
                name: "ix_option_set_value_option_set_id_value",
                schema: "assets",
                table: "option_set_value",
                columns: new[] { "option_set_id", "value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_option_set_value_parent_value_id",
                schema: "assets",
                table: "option_set_value",
                column: "parent_value_id");

            migrationBuilder.CreateIndex(
                name: "ix_person_email",
                schema: "assets",
                table: "person",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_lines_item_id",
                schema: "assets",
                table: "purchase_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_lines_purchase_id",
                schema: "assets",
                table: "purchase_lines",
                column: "purchase_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_lines_purchase_id1",
                schema: "assets",
                table: "purchase_lines",
                column: "purchase_id1");

            migrationBuilder.CreateIndex(
                name: "ix_work_in_progress_production_order_id",
                schema: "assets",
                table: "work_in_progress",
                column: "production_order_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asset_acquisition",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "asset_assignment",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "asset_attachment",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "asset_attribute_history",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "asset_attribute_value",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "asset_depreciation_entry",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "asset_disposal",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "asset_lifecycle_event",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "asset_valuation_history",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "attribute_assignment",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "finished_good_item",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "inventory_stocks",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "manufacturer",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "material_consumption",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "person",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "purchase_lines",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "scraps",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "work_in_progress",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "option_set_value",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "asset_depreciation_schedule",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "disposal_method",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "lifecycle_event_type",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "currency",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "attribute_definition",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "attribute_group",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "warehouses",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "inventory_items",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "purchases",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "production_order",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "asset",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "depreciation_method",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "option_set",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "inventory_types",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "asset_category",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "asset_status",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "location",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "inventory_categories",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "asset_type",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "asset_class",
                schema: "assets");
        }
    }
}
