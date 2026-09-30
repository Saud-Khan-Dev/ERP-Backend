using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPropertySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "property");

            migrationBuilder.CreateTable(
                name: "agreement_type",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agreement_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "allotment_status",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_allotment_status", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "allotment_type",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_allotment_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "attribute_group",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attribute_group", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "auction_status",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auction_status", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "auction_type",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auction_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "building_plan_status",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_building_plan_status", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "building_plan_type",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_building_plan_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "code_sequence",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    prefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    separator = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    minimum_digits = table.Column<int>(type: "integer", nullable: false),
                    next_number = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_code_sequence", x => x.id);
                    table.CheckConstraint("ck_code_sequence_minimum_digits", "minimum_digits BETWEEN 1 AND 10");
                    table.CheckConstraint("ck_code_sequence_next_number", "next_number >= 1");
                });

            migrationBuilder.CreateTable(
                name: "contact_type",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contact_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contract_status",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contract_status", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_type",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_folder = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "encroachment_status",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_encroachment_status", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "encumbrance_type",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_encumbrance_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lease_status",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lease_status", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lease_type",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lease_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "litigation_status",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_litigation_status", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "litigation_type",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_litigation_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "measurement_unit",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    factor_to_base = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    is_base = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_measurement_unit", x => x.id);
                    table.CheckConstraint("ck_measurement_unit_factor", "factor_to_base > 0");
                });

            migrationBuilder.CreateTable(
                name: "outsourcing_type",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outsourcing_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "owner_type",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_owner_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "property_classification",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_classification", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "property_status",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_status", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "property_type",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rental_status",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rental_status", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rental_type",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rental_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenure_type",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenure_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "town",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_town", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transfer_type",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requires_relationship = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transfer_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "attribute_definition",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    attribute_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    data_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    options_csv = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    default_value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attribute_definition", x => x.id);
                    table.ForeignKey(
                        name: "fk_attribute_definition_attribute_group_attribute_group_id",
                        column: x => x.attribute_group_id,
                        principalSchema: "property",
                        principalTable: "attribute_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    town_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_classification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    address_line = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    khasra_survey_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    property_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property", x => x.id);
                    table.ForeignKey(
                        name: "fk_property_property_classification_property_classification_id",
                        column: x => x.property_classification_id,
                        principalSchema: "property",
                        principalTable: "property_classification",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_property_status_property_status_id",
                        column: x => x.property_status_id,
                        principalSchema: "property",
                        principalTable: "property_status",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_property_type_property_type_id",
                        column: x => x.property_type_id,
                        principalSchema: "property",
                        principalTable: "property_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_town_town_id",
                        column: x => x.town_id,
                        principalSchema: "property",
                        principalTable: "town",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_area_regularization",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    additional_area = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    measurement_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    additional_area_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    regularization_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    application_date = table.Column<DateOnly>(type: "date", nullable: true),
                    regularization_date = table.Column<DateOnly>(type: "date", nullable: true),
                    order_reference_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    approved_by = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: true),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_area_regularization", x => x.id);
                    table.CheckConstraint("ck_property_area_regularization_area", "additional_area > 0");
                    table.ForeignKey(
                        name: "fk_property_area_regularization_measurement_unit_measurement_u",
                        column: x => x.measurement_unit_id,
                        principalSchema: "property",
                        principalTable: "measurement_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_area_regularization_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "property",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_attribute_value",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attribute_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_attribute_value", x => x.id);
                    table.ForeignKey(
                        name: "fk_property_attribute_value_attribute_definition_attribute_def",
                        column: x => x.attribute_definition_id,
                        principalSchema: "property",
                        principalTable: "attribute_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_attribute_value_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "property",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_document",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "PROPERTY"),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    stored_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    relative_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    checksum_sha256 = table.Column<string>(type: "char(64)", nullable: false),
                    document_date = table.Column<DateOnly>(type: "date", nullable: true),
                    reference_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    version_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    supersedes_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_confidential = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_document", x => x.id);
                    table.CheckConstraint("ck_property_document_property", "entity_type = 'OWNER' OR property_id IS NOT NULL");
                    table.CheckConstraint("ck_property_document_version", "version_no >= 1");
                    table.ForeignKey(
                        name: "fk_property_document_document_type_document_type_id",
                        column: x => x.document_type_id,
                        principalSchema: "property",
                        principalTable: "document_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_document_property_document_supersedes_document_id",
                        column: x => x.supersedes_document_id,
                        principalSchema: "property",
                        principalTable: "property_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_document_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "property",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_measurement",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    measurement_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_area = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    built_up_area = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total_area_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    built_up_area_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    measured_on = table.Column<DateOnly>(type: "date", nullable: true),
                    measurement_source = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_measurement", x => x.id);
                    table.CheckConstraint("ck_property_measurement_built_up_area", "built_up_area >= 0");
                    table.CheckConstraint("ck_property_measurement_total_area", "total_area > 0");
                    table.ForeignKey(
                        name: "fk_property_measurement_measurement_unit_measurement_unit_id",
                        column: x => x.measurement_unit_id,
                        principalSchema: "property",
                        principalTable: "measurement_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_measurement_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "property",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_status_history",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    reference_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_status_history", x => x.id);
                    table.CheckConstraint("ck_property_status_history_period", "effective_to IS NULL OR effective_to >= effective_from");
                    table.ForeignKey(
                        name: "fk_property_status_history_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "property",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_status_history_property_status_property_status_id",
                        column: x => x.property_status_id,
                        principalSchema: "property",
                        principalTable: "property_status",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_transfer",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    transfer_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_date = table.Column<DateOnly>(type: "date", nullable: false),
                    transfer_reference_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    share_transferred_pct = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    consideration_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    relationship = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    approved_by = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    approval_date = table.Column<DateOnly>(type: "date", nullable: true),
                    transfer_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_transfer", x => x.id);
                    table.ForeignKey(
                        name: "fk_property_transfer_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "property",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_transfer_transfer_type_transfer_type_id",
                        column: x => x.transfer_type_id,
                        principalSchema: "property",
                        principalTable: "transfer_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_owner",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    owner_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    father_husband_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cnic = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    ntn = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    registration_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    cnic_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    owner_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_owner", x => x.id);
                    table.ForeignKey(
                        name: "fk_property_owner_owner_type_owner_type_id",
                        column: x => x.owner_type_id,
                        principalSchema: "property",
                        principalTable: "owner_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_owner_property_document_cnic_document_id",
                        column: x => x.cnic_document_id,
                        principalSchema: "property",
                        principalTable: "property_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "owner_address",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    address_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    full_address = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    city_town = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    district = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    province = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "Pakistan"),
                    postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_owner_address", x => x.id);
                    table.ForeignKey(
                        name: "fk_owner_address_property_owner_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "property",
                        principalTable: "property_owner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "owner_contact",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    remarks = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_owner_contact", x => x.id);
                    table.ForeignKey(
                        name: "fk_owner_contact_contact_type_contact_type_id",
                        column: x => x.contact_type_id,
                        principalSchema: "property",
                        principalTable: "contact_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_owner_contact_property_owner_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "property",
                        principalTable: "property_owner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_ownership",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenure_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ownership_share_pct = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false, defaultValue: 100m),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    ownership_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    acquisition_transfer_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    acquired_via_transfer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    acquired_via_allotment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_ownership", x => x.id);
                    table.CheckConstraint("ck_property_ownership_period", "effective_to IS NULL OR effective_to >= effective_from");
                    table.CheckConstraint("ck_property_ownership_share", "ownership_share_pct > 0 AND ownership_share_pct <= 100");
                    table.ForeignKey(
                        name: "fk_property_ownership_property_owner_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "property",
                        principalTable: "property_owner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_ownership_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "property",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_ownership_property_transfer_acquired_via_transfer_",
                        column: x => x.acquired_via_transfer_id,
                        principalSchema: "property",
                        principalTable: "property_transfer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_ownership_tenure_type_tenure_type_id",
                        column: x => x.tenure_type_id,
                        principalSchema: "property",
                        principalTable: "tenure_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_ownership_transfer_type_acquisition_transfer_type_",
                        column: x => x.acquisition_transfer_type_id,
                        principalSchema: "property",
                        principalTable: "transfer_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_transfer_party",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_transfer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_role = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    share_pct = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_transfer_party", x => x.id);
                    table.CheckConstraint("ck_property_transfer_party_share", "share_pct > 0 AND share_pct <= 100");
                    table.ForeignKey(
                        name: "fk_property_transfer_party_property_owner_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "property",
                        principalTable: "property_owner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_transfer_party_property_transfer_property_transfer",
                        column: x => x.property_transfer_id,
                        principalSchema: "property",
                        principalTable: "property_transfer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_encumbrance",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encumbrance_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ownership_id = table.Column<Guid>(type: "uuid", nullable: true),
                    holder_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    holder_owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    release_date = table.Column<DateOnly>(type: "date", nullable: true),
                    release_reference_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_encumbrance", x => x.id);
                    table.ForeignKey(
                        name: "fk_property_encumbrance_encumbrance_type_encumbrance_type_id",
                        column: x => x.encumbrance_type_id,
                        principalSchema: "property",
                        principalTable: "encumbrance_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_encumbrance_property_owner_holder_owner_id",
                        column: x => x.holder_owner_id,
                        principalSchema: "property",
                        principalTable: "property_owner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_encumbrance_property_ownership_ownership_id",
                        column: x => x.ownership_id,
                        principalSchema: "property",
                        principalTable: "property_ownership",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_encumbrance_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "property",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agreement_type_code",
                schema: "property",
                table: "agreement_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_allotment_status_code",
                schema: "property",
                table: "allotment_status",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_allotment_type_code",
                schema: "property",
                table: "allotment_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attribute_definition_attribute_group_id",
                schema: "property",
                table: "attribute_definition",
                column: "attribute_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_attribute_definition_code",
                schema: "property",
                table: "attribute_definition",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attribute_group_code",
                schema: "property",
                table: "attribute_group",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auction_status_code",
                schema: "property",
                table: "auction_status",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auction_type_code",
                schema: "property",
                table: "auction_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_building_plan_status_code",
                schema: "property",
                table: "building_plan_status",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_building_plan_type_code",
                schema: "property",
                table: "building_plan_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_code_sequence_key",
                schema: "property",
                table: "code_sequence",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contact_type_code",
                schema: "property",
                table: "contact_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contract_status_code",
                schema: "property",
                table: "contract_status",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_type_code",
                schema: "property",
                table: "document_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_encroachment_status_code",
                schema: "property",
                table: "encroachment_status",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_encumbrance_type_code",
                schema: "property",
                table: "encumbrance_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lease_status_code",
                schema: "property",
                table: "lease_status",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lease_type_code",
                schema: "property",
                table: "lease_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_litigation_status_code",
                schema: "property",
                table: "litigation_status",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_litigation_type_code",
                schema: "property",
                table: "litigation_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_measurement_unit_code",
                schema: "property",
                table: "measurement_unit",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_measurement_unit_single_base",
                schema: "property",
                table: "measurement_unit",
                column: "is_base",
                unique: true,
                filter: "is_base");

            migrationBuilder.CreateIndex(
                name: "ix_outsourcing_type_code",
                schema: "property",
                table: "outsourcing_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_owner_address_owner_id_is_primary",
                schema: "property",
                table: "owner_address",
                columns: new[] { "owner_id", "is_primary" });

            migrationBuilder.CreateIndex(
                name: "ix_owner_contact_contact_type_id",
                schema: "property",
                table: "owner_contact",
                column: "contact_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_owner_contact_owner_id_is_primary",
                schema: "property",
                table: "owner_contact",
                columns: new[] { "owner_id", "is_primary" });

            migrationBuilder.CreateIndex(
                name: "ix_owner_type_code",
                schema: "property",
                table: "owner_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_property_property_classification_id",
                schema: "property",
                table: "property",
                column: "property_classification_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_property_code",
                schema: "property",
                table: "property",
                column: "property_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_property_property_status_id",
                schema: "property",
                table: "property",
                column: "property_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_property_type_id",
                schema: "property",
                table: "property",
                column: "property_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_town_id",
                schema: "property",
                table: "property",
                column: "town_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_area_regularization_measurement_unit_id",
                schema: "property",
                table: "property_area_regularization",
                column: "measurement_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_area_regularization_property_id",
                schema: "property",
                table: "property_area_regularization",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_attribute_value_attribute_definition_id",
                schema: "property",
                table: "property_attribute_value",
                column: "attribute_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_attribute_value_property_id_attribute_definition_id",
                schema: "property",
                table: "property_attribute_value",
                columns: new[] { "property_id", "attribute_definition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_property_classification_code",
                schema: "property",
                table: "property_classification",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_property_document_document_type_id",
                schema: "property",
                table: "property_document",
                column: "document_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_document_entity_type_entity_id",
                schema: "property",
                table: "property_document",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_property_document_property_id_document_type_id",
                schema: "property",
                table: "property_document",
                columns: new[] { "property_id", "document_type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_property_document_supersedes_document_id",
                schema: "property",
                table: "property_document",
                column: "supersedes_document_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_property_encumbrance_encumbrance_type_id",
                schema: "property",
                table: "property_encumbrance",
                column: "encumbrance_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_encumbrance_holder_owner_id",
                schema: "property",
                table: "property_encumbrance",
                column: "holder_owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_encumbrance_ownership_id",
                schema: "property",
                table: "property_encumbrance",
                column: "ownership_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_encumbrance_property_id_status",
                schema: "property",
                table: "property_encumbrance",
                columns: new[] { "property_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_property_measurement_measurement_unit_id",
                schema: "property",
                table: "property_measurement",
                column: "measurement_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_measurement_property_id_is_current",
                schema: "property",
                table: "property_measurement",
                columns: new[] { "property_id", "is_current" });

            migrationBuilder.CreateIndex(
                name: "ix_property_owner_cnic",
                schema: "property",
                table: "property_owner",
                column: "cnic");

            migrationBuilder.CreateIndex(
                name: "ix_property_owner_cnic_document_id",
                schema: "property",
                table: "property_owner",
                column: "cnic_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_owner_ntn",
                schema: "property",
                table: "property_owner",
                column: "ntn");

            migrationBuilder.CreateIndex(
                name: "ix_property_owner_owner_code",
                schema: "property",
                table: "property_owner",
                column: "owner_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_property_owner_owner_type_id",
                schema: "property",
                table: "property_owner",
                column: "owner_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_ownership_acquired_via_transfer_id",
                schema: "property",
                table: "property_ownership",
                column: "acquired_via_transfer_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_ownership_acquisition_transfer_type_id",
                schema: "property",
                table: "property_ownership",
                column: "acquisition_transfer_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_ownership_owner_id",
                schema: "property",
                table: "property_ownership",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_ownership_property_id_owner_id_effective_from",
                schema: "property",
                table: "property_ownership",
                columns: new[] { "property_id", "owner_id", "effective_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_property_ownership_property_id_ownership_status",
                schema: "property",
                table: "property_ownership",
                columns: new[] { "property_id", "ownership_status" });

            migrationBuilder.CreateIndex(
                name: "ix_property_ownership_tenure_type_id",
                schema: "property",
                table: "property_ownership",
                column: "tenure_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_status_code",
                schema: "property",
                table: "property_status",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_property_status_history_property_id_effective_from",
                schema: "property",
                table: "property_status_history",
                columns: new[] { "property_id", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "ix_property_status_history_property_status_id",
                schema: "property",
                table: "property_status_history",
                column: "property_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_transfer_property_id_transfer_date",
                schema: "property",
                table: "property_transfer",
                columns: new[] { "property_id", "transfer_date" });

            migrationBuilder.CreateIndex(
                name: "ix_property_transfer_transfer_no",
                schema: "property",
                table: "property_transfer",
                column: "transfer_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_property_transfer_transfer_type_id",
                schema: "property",
                table: "property_transfer",
                column: "transfer_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_transfer_party_owner_id",
                schema: "property",
                table: "property_transfer_party",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_transfer_party_property_transfer_id_owner_id_party",
                schema: "property",
                table: "property_transfer_party",
                columns: new[] { "property_transfer_id", "owner_id", "party_role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_property_type_code",
                schema: "property",
                table: "property_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rental_status_code",
                schema: "property",
                table: "rental_status",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rental_type_code",
                schema: "property",
                table: "rental_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenure_type_code",
                schema: "property",
                table: "tenure_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_town_code",
                schema: "property",
                table: "town",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transfer_type_code",
                schema: "property",
                table: "transfer_type",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agreement_type",
                schema: "property");

            migrationBuilder.DropTable(
                name: "allotment_status",
                schema: "property");

            migrationBuilder.DropTable(
                name: "allotment_type",
                schema: "property");

            migrationBuilder.DropTable(
                name: "auction_status",
                schema: "property");

            migrationBuilder.DropTable(
                name: "auction_type",
                schema: "property");

            migrationBuilder.DropTable(
                name: "building_plan_status",
                schema: "property");

            migrationBuilder.DropTable(
                name: "building_plan_type",
                schema: "property");

            migrationBuilder.DropTable(
                name: "code_sequence",
                schema: "property");

            migrationBuilder.DropTable(
                name: "contract_status",
                schema: "property");

            migrationBuilder.DropTable(
                name: "encroachment_status",
                schema: "property");

            migrationBuilder.DropTable(
                name: "lease_status",
                schema: "property");

            migrationBuilder.DropTable(
                name: "lease_type",
                schema: "property");

            migrationBuilder.DropTable(
                name: "litigation_status",
                schema: "property");

            migrationBuilder.DropTable(
                name: "litigation_type",
                schema: "property");

            migrationBuilder.DropTable(
                name: "outsourcing_type",
                schema: "property");

            migrationBuilder.DropTable(
                name: "owner_address",
                schema: "property");

            migrationBuilder.DropTable(
                name: "owner_contact",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_area_regularization",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_attribute_value",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_encumbrance",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_measurement",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_status_history",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_transfer_party",
                schema: "property");

            migrationBuilder.DropTable(
                name: "rental_status",
                schema: "property");

            migrationBuilder.DropTable(
                name: "rental_type",
                schema: "property");

            migrationBuilder.DropTable(
                name: "contact_type",
                schema: "property");

            migrationBuilder.DropTable(
                name: "attribute_definition",
                schema: "property");

            migrationBuilder.DropTable(
                name: "encumbrance_type",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_ownership",
                schema: "property");

            migrationBuilder.DropTable(
                name: "measurement_unit",
                schema: "property");

            migrationBuilder.DropTable(
                name: "attribute_group",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_owner",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_transfer",
                schema: "property");

            migrationBuilder.DropTable(
                name: "tenure_type",
                schema: "property");

            migrationBuilder.DropTable(
                name: "owner_type",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_document",
                schema: "property");

            migrationBuilder.DropTable(
                name: "transfer_type",
                schema: "property");

            migrationBuilder.DropTable(
                name: "document_type",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_classification",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_status",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_type",
                schema: "property");

            migrationBuilder.DropTable(
                name: "town",
                schema: "property");
        }
    }
}
