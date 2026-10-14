using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taxation.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialTaxationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "taxation");

            migrationBuilder.CreateSequence(
                name: "clearance_no_seq",
                schema: "taxation");

            migrationBuilder.CreateSequence(
                name: "demand_no_seq",
                schema: "taxation");

            migrationBuilder.CreateSequence(
                name: "fee_no_seq",
                schema: "taxation");

            migrationBuilder.CreateSequence(
                name: "notice_ref_seq",
                schema: "taxation");

            migrationBuilder.CreateSequence(
                name: "psid_seq",
                schema: "taxation");

            migrationBuilder.CreateSequence(
                name: "receipt_no_seq",
                schema: "taxation");

            migrationBuilder.CreateTable(
                name: "category_map",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    property_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_type_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tax_category = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("category_map_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "message_template",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    channel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    language = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, defaultValue: "en"),
                    body = table.Column<string>(type: "text", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("message_template_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notice_letterhead",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    government_line = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    authority_line = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    motto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    notes = table.Column<string>(type: "jsonb", nullable: false),
                    cc_lines = table.Column<string>(type: "jsonb", nullable: false),
                    qr_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    qr_bank_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    logo_left_ref = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    logo_right_ref = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    watermark = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    board_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("notice_letterhead_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notice_table_layout",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    other_label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "Other charges / earlier dues"),
                    hide_zero_rows = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    board_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("notice_table_layout_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notice_template",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    stage = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    paragraph_text = table.Column<string>(type: "text", nullable: false),
                    source_document = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    version_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("notice_template_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "party",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    party_type = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cnic_ntn = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    mobile = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    address = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("party_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rate_set",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    board_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    board_decision_date = table.Column<DateOnly>(type: "date", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false, defaultValueSql: "'DRAFT'"),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("rate_set_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tax_circle",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    org_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    officer_post_id = table.Column<Guid>(type: "uuid", nullable: true),
                    office_address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    office_fax = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    office_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tax_circle_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tax_head",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    head_kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_refundable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    calc_method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_builtin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    applies_to = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, defaultValueSql: "'BOTH'"),
                    charged_from = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false, defaultValueSql: "'ALWAYS'"),
                    facility_label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    formula_com = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    formula_res = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: true),
                    board_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    applies_annual_increase = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_conditional = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    override_allowed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    accounts_head_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tax_head_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notice_table_column",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    layout_id = table.Column<Guid>(type: "uuid", nullable: false),
                    column_key = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    heading = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    printed_by_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("notice_table_column_pkey", x => x.id);
                    table.ForeignKey(
                        name: "notice_table_column_layout_id_fkey",
                        column: x => x.layout_id,
                        principalSchema: "taxation",
                        principalTable: "notice_table_layout",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notice_table_row",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    layout_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("notice_table_row_pkey", x => x.id);
                    table.ForeignKey(
                        name: "notice_table_row_layout_id_fkey",
                        column: x => x.layout_id,
                        principalSchema: "taxation",
                        principalTable: "notice_table_layout",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ltf_factor",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    rate_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    town_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_category = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    component = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    band_from = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    band_to = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    factor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ltf_factor_pkey", x => x.id);
                    table.ForeignKey(
                        name: "ltf_factor_rate_set_id_fkey",
                        column: x => x.rate_set_id,
                        principalSchema: "taxation",
                        principalTable: "rate_set",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "penalty_rule",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    trigger = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    period_value = table.Column<int>(type: "integer", nullable: true),
                    period_unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    period_anchor = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    rate_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    @base = table.Column<string>(name: "base", type: "character varying(15)", maxLength: 15, nullable: false, defaultValueSql: "'OUTSTANDING'"),
                    target_set = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false, defaultValueSql: "'ALL_UNPAID'"),
                    includes_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    stacks_with_year_end = table.Column<bool>(type: "boolean", nullable: true),
                    rate_set_id = table.Column<Guid>(type: "uuid", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("penalty_rule_pkey", x => x.id);
                    table.ForeignKey(
                        name: "penalty_rule_rate_set_id_fkey",
                        column: x => x.rate_set_id,
                        principalSchema: "taxation",
                        principalTable: "rate_set",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tax_setting",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    rate_set_id = table.Column<Guid>(type: "uuid", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tax_setting_pkey", x => x.id);
                    table.ForeignKey(
                        name: "tax_setting_rate_set_id_fkey",
                        column: x => x.rate_set_id,
                        principalSchema: "taxation",
                        principalTable: "rate_set",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "demand_run",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    circle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rate_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    basis_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    property_count = table.Column<int>(type: "integer", nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    exception_count = table.Column<int>(type: "integer", nullable: true),
                    computed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    computed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("demand_run_pkey", x => x.id);
                    table.ForeignKey(
                        name: "demand_run_circle_id_fkey",
                        column: x => x.circle_id,
                        principalSchema: "taxation",
                        principalTable: "tax_circle",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "demand_run_rate_set_id_fkey",
                        column: x => x.rate_set_id,
                        principalSchema: "taxation",
                        principalTable: "rate_set",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tax_circle_staff",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    circle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tax_circle_staff_pkey", x => x.id);
                    table.ForeignKey(
                        name: "tax_circle_staff_circle_id_fkey",
                        column: x => x.circle_id,
                        principalSchema: "taxation",
                        principalTable: "tax_circle",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tax_circle_town",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    circle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    town_id = table.Column<Guid>(type: "uuid", nullable: false),
                    town_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    short_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tax_circle_town_pkey", x => x.id);
                    table.ForeignKey(
                        name: "tax_circle_town_circle_id_fkey",
                        column: x => x.circle_id,
                        principalSchema: "taxation",
                        principalTable: "tax_circle",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tax_file",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    file_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    circle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    town_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_category = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    holding_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rooms = table.Column<int>(type: "integer", nullable: true),
                    psid = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    registered_on = table.Column<DateOnly>(type: "date", nullable: true),
                    water_facility_from = table.Column<DateOnly>(type: "date", nullable: true),
                    uplift_rate_basis = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    uplift_pct = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    old_reference_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    plot_no = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    tax_status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false, defaultValueSql: "'ACTIVE'"),
                    exemption_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    opened_on = table.Column<DateOnly>(type: "date", nullable: false),
                    closed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tax_file_pkey", x => x.id);
                    table.ForeignKey(
                        name: "tax_file_circle_id_fkey",
                        column: x => x.circle_id,
                        principalSchema: "taxation",
                        principalTable: "tax_circle",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rate_band",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    rate_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    head_id = table.Column<Guid>(type: "uuid", nullable: false),
                    town_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tax_category = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    basis = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unit_label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    round_up = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    block_size = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    band_from = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    band_to = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    rate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    rate_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    pro_rata = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    above_top_rule = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("rate_band_pkey", x => x.id);
                    table.ForeignKey(
                        name: "rate_band_head_id_fkey",
                        column: x => x.head_id,
                        principalSchema: "taxation",
                        principalTable: "tax_head",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "rate_band_rate_set_id_fkey",
                        column: x => x.rate_set_id,
                        principalSchema: "taxation",
                        principalTable: "rate_set",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "special_policy",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    head_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    applies_to = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, defaultValueSql: "'BOTH'"),
                    annual_increase = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    board_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("special_policy_pkey", x => x.id);
                    table.ForeignKey(
                        name: "special_policy_head_id_fkey",
                        column: x => x.head_id,
                        principalSchema: "taxation",
                        principalTable: "tax_head",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notice_table_row_source",
                schema: "taxation",
                columns: table => new
                {
                    row_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("notice_table_row_source_pkey", x => new { x.row_id, x.source_code });
                    table.ForeignKey(
                        name: "notice_table_row_source_row_id_fkey",
                        column: x => x.row_id,
                        principalSchema: "taxation",
                        principalTable: "notice_table_row",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "penalty_run",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    circle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    run_on = table.Column<DateOnly>(type: "date", nullable: false),
                    target_set = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    rate_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    includes_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    property_count = table.Column<int>(type: "integer", nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("penalty_run_pkey", x => x.id);
                    table.ForeignKey(
                        name: "penalty_run_circle_id_fkey",
                        column: x => x.circle_id,
                        principalSchema: "taxation",
                        principalTable: "tax_circle",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "penalty_run_rule_id_fkey",
                        column: x => x.rule_id,
                        principalSchema: "taxation",
                        principalTable: "penalty_rule",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agreement_schedule",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tax_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    base_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    frequency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    term_years = table.Column<int>(type: "integer", nullable: true),
                    step_pct = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    step_interval_years = table.Column<int>(type: "integer", nullable: true),
                    is_direct_99_year = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    next_renewal_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false, defaultValueSql: "'ACTIVE'"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("agreement_schedule_pkey", x => x.id);
                    table.ForeignKey(
                        name: "agreement_schedule_tax_file_id_fkey",
                        column: x => x.tax_file_id,
                        principalSchema: "taxation",
                        principalTable: "tax_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "litigation_hold",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tax_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_litigation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    started_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ended_on = table.Column<DateOnly>(type: "date", nullable: true),
                    suspends_demand = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    excludes_completion_clock = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("litigation_hold_pkey", x => x.id);
                    table.ForeignKey(
                        name: "litigation_hold_tax_file_id_fkey",
                        column: x => x.tax_file_id,
                        principalSchema: "taxation",
                        principalTable: "tax_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tax_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    psid = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    challan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    channel = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    receipt_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    received_by = table.Column<Guid>(type: "uuid", nullable: true),
                    bank_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    transaction_ref = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    posted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    accounts_posted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("payment_pkey", x => x.id);
                    table.ForeignKey(
                        name: "payment_tax_file_id_fkey",
                        column: x => x.tax_file_id,
                        principalSchema: "taxation",
                        principalTable: "tax_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tax_file_basis",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tax_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    land_area_sqyd = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    land_area_entered = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    land_area_unit_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    land_area_kanal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    covered_area_sqft = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    covered_area_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    regularized_area_sqyd = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    source_measurement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_building_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bpa_date = table.Column<DateOnly>(type: "date", nullable: true),
                    completion_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completion_date = table.Column<DateOnly>(type: "date", nullable: true),
                    property_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    property_status_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    classification_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tax_file_basis_pkey", x => x.id);
                    table.ForeignKey(
                        name: "tax_file_basis_tax_file_id_fkey",
                        column: x => x.tax_file_id,
                        principalSchema: "taxation",
                        principalTable: "tax_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tax_file_head_value",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tax_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    head_id = table.Column<Guid>(type: "uuid", nullable: false),
                    facility_from = table.Column<DateOnly>(type: "date", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tax_file_head_value_pkey", x => x.id);
                    table.ForeignKey(
                        name: "tax_file_head_value_head_id_fkey",
                        column: x => x.head_id,
                        principalSchema: "taxation",
                        principalTable: "tax_head",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "tax_file_head_value_tax_file_id_fkey",
                        column: x => x.tax_file_id,
                        principalSchema: "taxation",
                        principalTable: "tax_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tax_file_payer",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tax_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payer_role = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    source_type = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    share_pct = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: true),
                    is_addressee = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    father_husband_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cnic_ntn = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    mobile = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    postal_address = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tax_file_payer_pkey", x => x.id);
                    table.ForeignKey(
                        name: "tax_file_payer_tax_file_id_fkey",
                        column: x => x.tax_file_id,
                        principalSchema: "taxation",
                        principalTable: "tax_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fee",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    fee_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    head_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    party_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    basis_qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    basis_unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    rate_band_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    exemption_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("fee_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fee_head_id_fkey",
                        column: x => x.head_id,
                        principalSchema: "taxation",
                        principalTable: "tax_head",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fee_party_id_fkey",
                        column: x => x.party_id,
                        principalSchema: "taxation",
                        principalTable: "party",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fee_rate_band_id_fkey",
                        column: x => x.rate_band_id,
                        principalSchema: "taxation",
                        principalTable: "rate_band",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fee_tax_file_id_fkey",
                        column: x => x.tax_file_id,
                        principalSchema: "taxation",
                        principalTable: "tax_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tax_file_override",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tax_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    head_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    follows_increase = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tax_file_override_pkey", x => x.id);
                    table.ForeignKey(
                        name: "tax_file_override_head_id_fkey",
                        column: x => x.head_id,
                        principalSchema: "taxation",
                        principalTable: "tax_head",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "tax_file_override_policy_id_fkey",
                        column: x => x.policy_id,
                        principalSchema: "taxation",
                        principalTable: "special_policy",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "tax_file_override_tax_file_id_fkey",
                        column: x => x.tax_file_id,
                        principalSchema: "taxation",
                        principalTable: "tax_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agreement_step_revision",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    step_pct = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    step_interval_years = table.Column<int>(type: "integer", nullable: false),
                    board_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("agreement_step_revision_pkey", x => x.id);
                    table.ForeignKey(
                        name: "agreement_step_revision_schedule_id_fkey",
                        column: x => x.schedule_id,
                        principalSchema: "taxation",
                        principalTable: "agreement_schedule",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "demand",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    demand_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tax_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    basis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    payer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payer_cnic = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    payer_mobile = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    payer_address = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    current_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    arrears_bf = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    penalty_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    stage = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    issued_on = table.Column<DateOnly>(type: "date", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: true),
                    carried_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    carried_to_demand_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("demand_pkey", x => x.id);
                    table.ForeignKey(
                        name: "demand_basis_id_fkey",
                        column: x => x.basis_id,
                        principalSchema: "taxation",
                        principalTable: "tax_file_basis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "demand_carried_to_demand_id_fkey",
                        column: x => x.carried_to_demand_id,
                        principalSchema: "taxation",
                        principalTable: "demand",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "demand_payer_id_fkey",
                        column: x => x.payer_id,
                        principalSchema: "taxation",
                        principalTable: "tax_file_payer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "demand_run_id_fkey",
                        column: x => x.run_id,
                        principalSchema: "taxation",
                        principalTable: "demand_run",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "demand_tax_file_id_fkey",
                        column: x => x.tax_file_id,
                        principalSchema: "taxation",
                        principalTable: "tax_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "clearance",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    clearance_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tax_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    property_ref_id = table.Column<Guid>(type: "uuid", nullable: true),
                    property_ref_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    outstanding_checked = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    issued_on = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: false),
                    issued_by = table.Column<Guid>(type: "uuid", nullable: true),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("clearance_pkey", x => x.id);
                    table.ForeignKey(
                        name: "clearance_fee_id_fkey",
                        column: x => x.fee_id,
                        principalSchema: "taxation",
                        principalTable: "fee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "clearance_tax_file_id_fkey",
                        column: x => x.tax_file_id,
                        principalSchema: "taxation",
                        principalTable: "tax_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "arrears",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tax_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    head_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: true),
                    original_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    penalty_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    outstanding = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    source_demand_id = table.Column<Guid>(type: "uuid", nullable: true),
                    import_row_id = table.Column<Guid>(type: "uuid", nullable: true),
                    added_to_demand_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    entered_by = table.Column<Guid>(type: "uuid", nullable: true),
                    entered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("arrears_pkey", x => x.id);
                    table.ForeignKey(
                        name: "arrears_added_to_demand_id_fkey",
                        column: x => x.added_to_demand_id,
                        principalSchema: "taxation",
                        principalTable: "demand",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "arrears_head_id_fkey",
                        column: x => x.head_id,
                        principalSchema: "taxation",
                        principalTable: "tax_head",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "arrears_source_demand_id_fkey",
                        column: x => x.source_demand_id,
                        principalSchema: "taxation",
                        principalTable: "demand",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "arrears_tax_file_id_fkey",
                        column: x => x.tax_file_id,
                        principalSchema: "taxation",
                        principalTable: "tax_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "demand_line",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    demand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    head_id = table.Column<Guid>(type: "uuid", nullable: false),
                    basis_qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    basis_unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ltf_factor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rate_band_id = table.Column<Guid>(type: "uuid", nullable: true),
                    schedule_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    base_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    increase_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    uplift_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    override_id = table.Column<Guid>(type: "uuid", nullable: true),
                    line_mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValueSql: "'NORMAL'"),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: true),
                    normal_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    override_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    explanation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("demand_line_pkey", x => x.id);
                    table.ForeignKey(
                        name: "demand_line_demand_id_fkey",
                        column: x => x.demand_id,
                        principalSchema: "taxation",
                        principalTable: "demand",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "demand_line_head_id_fkey",
                        column: x => x.head_id,
                        principalSchema: "taxation",
                        principalTable: "tax_head",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "demand_line_ltf_factor_id_fkey",
                        column: x => x.ltf_factor_id,
                        principalSchema: "taxation",
                        principalTable: "ltf_factor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "demand_line_override_id_fkey",
                        column: x => x.override_id,
                        principalSchema: "taxation",
                        principalTable: "tax_file_override",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "demand_line_policy_id_fkey",
                        column: x => x.policy_id,
                        principalSchema: "taxation",
                        principalTable: "special_policy",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "demand_line_rate_band_id_fkey",
                        column: x => x.rate_band_id,
                        principalSchema: "taxation",
                        principalTable: "rate_band",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notice",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    notice_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    endst_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    letterhead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shows_paragraph = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    table_layout_id = table.Column<Guid>(type: "uuid", nullable: true),
                    columns = table.Column<string>(type: "jsonb", nullable: true),
                    shows_table = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    particulars = table.Column<string>(type: "jsonb", nullable: false),
                    demand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    origin = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValueSql: "'CYCLE'"),
                    pay_via = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValueSql: "'BOTH'"),
                    qr_payload = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    sms_sent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_version = table.Column<int>(type: "integer", nullable: false),
                    penalty_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    variables = table.Column<string>(type: "jsonb", nullable: false),
                    edited_fields = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValueSql: "'PENDING'"),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    psid = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    amount_within_due = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    amount_after_due = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    sms_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pdf_document_ref = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reply_by = table.Column<DateOnly>(type: "date", nullable: true),
                    balance_at_issue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    signing_post_id = table.Column<Guid>(type: "uuid", nullable: true),
                    signing_employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    signing_officer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    served_by_employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    served_on = table.Column<DateOnly>(type: "date", nullable: true),
                    service_mode = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("notice_pkey", x => x.id);
                    table.ForeignKey(
                        name: "notice_demand_id_fkey",
                        column: x => x.demand_id,
                        principalSchema: "taxation",
                        principalTable: "demand",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "notice_letterhead_id_fkey",
                        column: x => x.letterhead_id,
                        principalSchema: "taxation",
                        principalTable: "notice_letterhead",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "notice_penalty_run_id_fkey",
                        column: x => x.penalty_run_id,
                        principalSchema: "taxation",
                        principalTable: "penalty_run",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "notice_table_layout_id_fkey",
                        column: x => x.table_layout_id,
                        principalSchema: "taxation",
                        principalTable: "notice_table_layout",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "notice_template_id_fkey",
                        column: x => x.template_id,
                        principalSchema: "taxation",
                        principalTable: "notice_template",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "penalty",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    demand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    base_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    pct = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    applied_on = table.Column<DateOnly>(type: "date", nullable: false),
                    rule_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    violation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("penalty_pkey", x => x.id);
                    table.ForeignKey(
                        name: "penalty_demand_id_fkey",
                        column: x => x.demand_id,
                        principalSchema: "taxation",
                        principalTable: "demand",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "penalty_rule_id_fkey",
                        column: x => x.rule_id,
                        principalSchema: "taxation",
                        principalTable: "penalty_rule",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "penalty_run_id_fkey",
                        column: x => x.run_id,
                        principalSchema: "taxation",
                        principalTable: "penalty_run",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sms_message",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tax_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    demand_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_mobile = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    queued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    provider_ref = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    delivery_status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("sms_message_pkey", x => x.id);
                    table.ForeignKey(
                        name: "sms_message_demand_id_fkey",
                        column: x => x.demand_id,
                        principalSchema: "taxation",
                        principalTable: "demand",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "sms_message_notice_id_fkey",
                        column: x => x.notice_id,
                        principalSchema: "taxation",
                        principalTable: "notice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "sms_message_tax_file_id_fkey",
                        column: x => x.tax_file_id,
                        principalSchema: "taxation",
                        principalTable: "tax_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "sms_message_template_id_fkey",
                        column: x => x.template_id,
                        principalSchema: "taxation",
                        principalTable: "message_template",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_allocation",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    head_id = table.Column<Guid>(type: "uuid", nullable: false),
                    demand_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    arrears_id = table.Column<Guid>(type: "uuid", nullable: true),
                    penalty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("payment_allocation_pkey", x => x.id);
                    table.ForeignKey(
                        name: "payment_allocation_arrears_id_fkey",
                        column: x => x.arrears_id,
                        principalSchema: "taxation",
                        principalTable: "arrears",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "payment_allocation_demand_line_id_fkey",
                        column: x => x.demand_line_id,
                        principalSchema: "taxation",
                        principalTable: "demand_line",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "payment_allocation_fee_id_fkey",
                        column: x => x.fee_id,
                        principalSchema: "taxation",
                        principalTable: "fee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "payment_allocation_head_id_fkey",
                        column: x => x.head_id,
                        principalSchema: "taxation",
                        principalTable: "tax_head",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "payment_allocation_payment_id_fkey",
                        column: x => x.payment_id,
                        principalSchema: "taxation",
                        principalTable: "payment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "payment_allocation_penalty_id_fkey",
                        column: x => x.penalty_id,
                        principalSchema: "taxation",
                        principalTable: "penalty",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "psid_sync",
                schema: "taxation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tax_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    psid = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    sync_no = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    demand_id = table.Column<Guid>(type: "uuid", nullable: true),
                    penalty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount_before = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    amount_after = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    onelink_ack = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("psid_sync_pkey", x => x.id);
                    table.ForeignKey(
                        name: "psid_sync_demand_id_fkey",
                        column: x => x.demand_id,
                        principalSchema: "taxation",
                        principalTable: "demand",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "psid_sync_penalty_id_fkey",
                        column: x => x.penalty_id,
                        principalSchema: "taxation",
                        principalTable: "penalty",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "psid_sync_tax_file_id_fkey",
                        column: x => x.tax_file_id,
                        principalSchema: "taxation",
                        principalTable: "tax_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agreement_schedule_tax_file_id",
                schema: "taxation",
                table: "agreement_schedule",
                column: "tax_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_agreement_step_revision_schedule_id",
                schema: "taxation",
                table: "agreement_step_revision",
                column: "schedule_id");

            migrationBuilder.CreateIndex(
                name: "ix_arrears_added_to_demand_id",
                schema: "taxation",
                table: "arrears",
                column: "added_to_demand_id");

            migrationBuilder.CreateIndex(
                name: "ix_arrears_head_id",
                schema: "taxation",
                table: "arrears",
                column: "head_id");

            migrationBuilder.CreateIndex(
                name: "ix_arrears_source_demand_id",
                schema: "taxation",
                table: "arrears",
                column: "source_demand_id");

            migrationBuilder.CreateIndex(
                name: "ix_arrears_tax_file_id",
                schema: "taxation",
                table: "arrears",
                column: "tax_file_id");

            migrationBuilder.CreateIndex(
                name: "category_map_property_type_id_key",
                schema: "taxation",
                table: "category_map",
                column: "property_type_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "clearance_clearance_no_key",
                schema: "taxation",
                table: "clearance",
                column: "clearance_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clearance_fee_id",
                schema: "taxation",
                table: "clearance",
                column: "fee_id");

            migrationBuilder.CreateIndex(
                name: "ix_clearance_tax_file_id",
                schema: "taxation",
                table: "clearance",
                column: "tax_file_id");

            migrationBuilder.CreateIndex(
                name: "demand_demand_no_key",
                schema: "taxation",
                table: "demand",
                column: "demand_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "demand_tax_file_id_fiscal_year_key",
                schema: "taxation",
                table: "demand",
                columns: new[] { "tax_file_id", "fiscal_year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_demand_basis_id",
                schema: "taxation",
                table: "demand",
                column: "basis_id");

            migrationBuilder.CreateIndex(
                name: "ix_demand_carried_to_demand_id",
                schema: "taxation",
                table: "demand",
                column: "carried_to_demand_id");

            migrationBuilder.CreateIndex(
                name: "ix_demand_payer_id",
                schema: "taxation",
                table: "demand",
                column: "payer_id");

            migrationBuilder.CreateIndex(
                name: "ix_demand_run_id",
                schema: "taxation",
                table: "demand",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "ix_demand_line_demand_id",
                schema: "taxation",
                table: "demand_line",
                column: "demand_id");

            migrationBuilder.CreateIndex(
                name: "ix_demand_line_head_id",
                schema: "taxation",
                table: "demand_line",
                column: "head_id");

            migrationBuilder.CreateIndex(
                name: "ix_demand_line_ltf_factor_id",
                schema: "taxation",
                table: "demand_line",
                column: "ltf_factor_id");

            migrationBuilder.CreateIndex(
                name: "ix_demand_line_override_id",
                schema: "taxation",
                table: "demand_line",
                column: "override_id");

            migrationBuilder.CreateIndex(
                name: "ix_demand_line_policy_id",
                schema: "taxation",
                table: "demand_line",
                column: "policy_id");

            migrationBuilder.CreateIndex(
                name: "ix_demand_line_rate_band_id",
                schema: "taxation",
                table: "demand_line",
                column: "rate_band_id");

            migrationBuilder.CreateIndex(
                name: "ix_demand_run_circle_id",
                schema: "taxation",
                table: "demand_run",
                column: "circle_id");

            migrationBuilder.CreateIndex(
                name: "ix_demand_run_rate_set_id",
                schema: "taxation",
                table: "demand_run",
                column: "rate_set_id");

            migrationBuilder.CreateIndex(
                name: "fee_fee_no_key",
                schema: "taxation",
                table: "fee",
                column: "fee_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fee_head_id",
                schema: "taxation",
                table: "fee",
                column: "head_id");

            migrationBuilder.CreateIndex(
                name: "ix_fee_party_id",
                schema: "taxation",
                table: "fee",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "ix_fee_rate_band_id",
                schema: "taxation",
                table: "fee",
                column: "rate_band_id");

            migrationBuilder.CreateIndex(
                name: "ix_fee_tax_file_id",
                schema: "taxation",
                table: "fee",
                column: "tax_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_litigation_hold_tax_file_id",
                schema: "taxation",
                table: "litigation_hold",
                column: "tax_file_id");

            migrationBuilder.CreateIndex(
                name: "ltf_factor_rate_set_id_town_id_tax_category_component_band_key",
                schema: "taxation",
                table: "ltf_factor",
                columns: new[] { "rate_set_id", "town_id", "tax_category", "component", "band_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notice_demand_id",
                schema: "taxation",
                table: "notice",
                column: "demand_id");

            migrationBuilder.CreateIndex(
                name: "ix_notice_letterhead_id",
                schema: "taxation",
                table: "notice",
                column: "letterhead_id");

            migrationBuilder.CreateIndex(
                name: "ix_notice_penalty_run_id",
                schema: "taxation",
                table: "notice",
                column: "penalty_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_notice_table_layout_id",
                schema: "taxation",
                table: "notice",
                column: "table_layout_id");

            migrationBuilder.CreateIndex(
                name: "ix_notice_template_id",
                schema: "taxation",
                table: "notice",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "notice_endst_no_key",
                schema: "taxation",
                table: "notice",
                column: "endst_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "notice_notice_no_key",
                schema: "taxation",
                table: "notice",
                column: "notice_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "notice_table_column_layout_id_column_key_key",
                schema: "taxation",
                table: "notice_table_column",
                columns: new[] { "layout_id", "column_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notice_table_row_layout_id",
                schema: "taxation",
                table: "notice_table_row",
                column: "layout_id");

            migrationBuilder.CreateIndex(
                name: "notice_template_stage_key",
                schema: "taxation",
                table: "notice_template",
                column: "stage",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_tax_file_id",
                schema: "taxation",
                table: "payment",
                column: "tax_file_id");

            migrationBuilder.CreateIndex(
                name: "payment_transaction_ref_key",
                schema: "taxation",
                table: "payment",
                column: "transaction_ref",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocation_arrears_id",
                schema: "taxation",
                table: "payment_allocation",
                column: "arrears_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocation_demand_line_id",
                schema: "taxation",
                table: "payment_allocation",
                column: "demand_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocation_fee_id",
                schema: "taxation",
                table: "payment_allocation",
                column: "fee_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocation_head_id",
                schema: "taxation",
                table: "payment_allocation",
                column: "head_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocation_payment_id",
                schema: "taxation",
                table: "payment_allocation",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocation_penalty_id",
                schema: "taxation",
                table: "payment_allocation",
                column: "penalty_id");

            migrationBuilder.CreateIndex(
                name: "ix_penalty_demand_id",
                schema: "taxation",
                table: "penalty",
                column: "demand_id");

            migrationBuilder.CreateIndex(
                name: "ix_penalty_rule_id",
                schema: "taxation",
                table: "penalty",
                column: "rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_penalty_run_id",
                schema: "taxation",
                table: "penalty",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "ix_penalty_rule_rate_set_id",
                schema: "taxation",
                table: "penalty_rule",
                column: "rate_set_id");

            migrationBuilder.CreateIndex(
                name: "penalty_rule_code_key",
                schema: "taxation",
                table: "penalty_rule",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_penalty_run_circle_id",
                schema: "taxation",
                table: "penalty_run",
                column: "circle_id");

            migrationBuilder.CreateIndex(
                name: "ix_penalty_run_rule_id",
                schema: "taxation",
                table: "penalty_run",
                column: "rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_psid_sync_demand_id",
                schema: "taxation",
                table: "psid_sync",
                column: "demand_id");

            migrationBuilder.CreateIndex(
                name: "ix_psid_sync_penalty_id",
                schema: "taxation",
                table: "psid_sync",
                column: "penalty_id");

            migrationBuilder.CreateIndex(
                name: "ix_psid_sync_tax_file_id",
                schema: "taxation",
                table: "psid_sync",
                column: "tax_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_rate_band_head_id",
                schema: "taxation",
                table: "rate_band",
                column: "head_id");

            migrationBuilder.CreateIndex(
                name: "ix_rate_band_rate_set_id",
                schema: "taxation",
                table: "rate_band",
                column: "rate_set_id");

            migrationBuilder.CreateIndex(
                name: "ix_sms_message_demand_id",
                schema: "taxation",
                table: "sms_message",
                column: "demand_id");

            migrationBuilder.CreateIndex(
                name: "ix_sms_message_notice_id",
                schema: "taxation",
                table: "sms_message",
                column: "notice_id");

            migrationBuilder.CreateIndex(
                name: "ix_sms_message_tax_file_id",
                schema: "taxation",
                table: "sms_message",
                column: "tax_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_sms_message_template_id",
                schema: "taxation",
                table: "sms_message",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_special_policy_head_id",
                schema: "taxation",
                table: "special_policy",
                column: "head_id");

            migrationBuilder.CreateIndex(
                name: "special_policy_code_key",
                schema: "taxation",
                table: "special_policy",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "tax_circle_code_key",
                schema: "taxation",
                table: "tax_circle",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tax_circle_staff_circle_id",
                schema: "taxation",
                table: "tax_circle_staff",
                column: "circle_id");

            migrationBuilder.CreateIndex(
                name: "ix_tax_circle_town_circle_id",
                schema: "taxation",
                table: "tax_circle_town",
                column: "circle_id");

            migrationBuilder.CreateIndex(
                name: "tax_circle_town_town_id_effective_from_key",
                schema: "taxation",
                table: "tax_circle_town",
                columns: new[] { "town_id", "effective_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tax_file_circle_id_town_id",
                schema: "taxation",
                table: "tax_file",
                columns: new[] { "circle_id", "town_id" });

            migrationBuilder.CreateIndex(
                name: "tax_file_file_no_key",
                schema: "taxation",
                table: "tax_file",
                column: "file_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "tax_file_property_id_key",
                schema: "taxation",
                table: "tax_file",
                column: "property_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "tax_file_psid_key",
                schema: "taxation",
                table: "tax_file",
                column: "psid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tax_file_basis_tax_file_id",
                schema: "taxation",
                table: "tax_file_basis",
                column: "tax_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_tax_file_head_value_head_id",
                schema: "taxation",
                table: "tax_file_head_value",
                column: "head_id");

            migrationBuilder.CreateIndex(
                name: "tax_file_head_value_tax_file_id_head_id_key",
                schema: "taxation",
                table: "tax_file_head_value",
                columns: new[] { "tax_file_id", "head_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tax_file_override_head_id",
                schema: "taxation",
                table: "tax_file_override",
                column: "head_id");

            migrationBuilder.CreateIndex(
                name: "ix_tax_file_override_policy_id",
                schema: "taxation",
                table: "tax_file_override",
                column: "policy_id");

            migrationBuilder.CreateIndex(
                name: "tax_file_override_tax_file_id_head_id_effective_from_key",
                schema: "taxation",
                table: "tax_file_override",
                columns: new[] { "tax_file_id", "head_id", "effective_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tax_file_payer_tax_file_id",
                schema: "taxation",
                table: "tax_file_payer",
                column: "tax_file_id");

            migrationBuilder.CreateIndex(
                name: "tax_head_code_key",
                schema: "taxation",
                table: "tax_head",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tax_setting_rate_set_id",
                schema: "taxation",
                table: "tax_setting",
                column: "rate_set_id");

            migrationBuilder.CreateIndex(
                name: "tax_setting_key_effective_from_key",
                schema: "taxation",
                table: "tax_setting",
                columns: new[] { "key", "effective_from" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agreement_step_revision",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "category_map",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "clearance",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "litigation_hold",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "notice_table_column",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "notice_table_row_source",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "payment_allocation",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "psid_sync",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "sms_message",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "tax_circle_staff",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "tax_circle_town",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "tax_file_head_value",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "tax_setting",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "agreement_schedule",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "notice_table_row",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "arrears",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "demand_line",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "fee",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "payment",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "penalty",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "notice",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "message_template",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "ltf_factor",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "tax_file_override",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "party",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "rate_band",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "demand",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "notice_letterhead",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "penalty_run",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "notice_table_layout",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "notice_template",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "special_policy",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "tax_file_basis",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "tax_file_payer",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "demand_run",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "penalty_rule",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "tax_head",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "tax_file",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "rate_set",
                schema: "taxation");

            migrationBuilder.DropTable(
                name: "tax_circle",
                schema: "taxation");

            migrationBuilder.DropSequence(
                name: "clearance_no_seq",
                schema: "taxation");

            migrationBuilder.DropSequence(
                name: "demand_no_seq",
                schema: "taxation");

            migrationBuilder.DropSequence(
                name: "fee_no_seq",
                schema: "taxation");

            migrationBuilder.DropSequence(
                name: "notice_ref_seq",
                schema: "taxation");

            migrationBuilder.DropSequence(
                name: "psid_seq",
                schema: "taxation");

            migrationBuilder.DropSequence(
                name: "receipt_no_seq",
                schema: "taxation");
        }
    }
}
