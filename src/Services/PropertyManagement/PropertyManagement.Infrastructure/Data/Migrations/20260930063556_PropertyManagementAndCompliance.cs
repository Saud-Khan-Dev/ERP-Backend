using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PropertyManagementAndCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ERD index (owner_name). property_owner.owner_name is an EF complex property, which EF cannot index.
            migrationBuilder.Sql("CREATE INDEX ix_property_owner_owner_name ON property.property_owner (owner_name);");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "transfer_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "code",
                schema: "property",
                table: "town",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "tenure_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "rental_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "rental_status",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "property_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "property_status",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "property_classification",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "owner_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "outsourcing_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "measurement_unit",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "code",
                schema: "property",
                table: "measurement_unit",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "litigation_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "litigation_status",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "lease_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "lease_status",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "encumbrance_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "encroachment_status",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "document_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "contract_status",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "contact_type",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "code",
                schema: "property",
                table: "contact_type",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "building_plan_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "building_plan_status",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "auction_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "auction_status",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "attribute_group",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "allotment_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "allotment_status",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "agreement_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.CreateTable(
                name: "building_plan",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    building_plan_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    building_plan_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    supersedes_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    applicant_owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submission_date = table.Column<DateOnly>(type: "date", nullable: true),
                    approval_date = table.Column<DateOnly>(type: "date", nullable: true),
                    approved_by = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    approval_reference_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    validity_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    covered_area = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    measurement_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    floors = table.Column<int>(type: "integer", nullable: true),
                    architect_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_building_plan", x => x.id);
                    table.ForeignKey(
                        name: "fk_building_plan_building_plan_status_building_plan_status_id",
                        column: x => x.building_plan_status_id,
                        principalSchema: "property",
                        principalTable: "building_plan_status",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_building_plan_building_plan_supersedes_plan_id",
                        column: x => x.supersedes_plan_id,
                        principalSchema: "property",
                        principalTable: "building_plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_building_plan_building_plan_type_building_plan_type_id",
                        column: x => x.building_plan_type_id,
                        principalSchema: "property",
                        principalTable: "building_plan_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_building_plan_measurement_unit_measurement_unit_id",
                        column: x => x.measurement_unit_id,
                        principalSchema: "property",
                        principalTable: "measurement_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_building_plan_property_owner_applicant_owner_id",
                        column: x => x.applicant_owner_id,
                        principalSchema: "property",
                        principalTable: "property_owner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_building_plan_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "property",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_allotment",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allottee_owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allotment_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    allotment_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allotment_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allotment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: true),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    allotment_letter_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    conditions = table.Column<string>(type: "text", nullable: true),
                    cancellation_date = table.Column<DateOnly>(type: "date", nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true),
                    cancellation_order_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    restoration_date = table.Column<DateOnly>(type: "date", nullable: true),
                    restoration_order_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_allotment", x => x.id);
                    table.ForeignKey(
                        name: "fk_property_allotment_allotment_status_allotment_status_id",
                        column: x => x.allotment_status_id,
                        principalSchema: "property",
                        principalTable: "allotment_status",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_allotment_allotment_type_allotment_type_id",
                        column: x => x.allotment_type_id,
                        principalSchema: "property",
                        principalTable: "allotment_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_allotment_property_owner_allottee_owner_id",
                        column: x => x.allottee_owner_id,
                        principalSchema: "property",
                        principalTable: "property_owner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_allotment_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "property",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_appeal",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appeal_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    appellant_owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appealed_order_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    appealed_order_date = table.Column<DateOnly>(type: "date", nullable: false),
                    order_received_date = table.Column<DateOnly>(type: "date", nullable: true),
                    order_source_table = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    order_source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    appeal_date = table.Column<DateOnly>(type: "date", nullable: false),
                    appellate_authority = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false, defaultValue: "Chief Secretary, Khyber Pakhtunkhwa"),
                    delegated_officer = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    decision_due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    decision_date = table.Column<DateOnly>(type: "date", nullable: true),
                    decision_outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    decision_details = table.Column<string>(type: "text", nullable: true),
                    appeal_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "FILED"),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_appeal", x => x.id);
                    table.ForeignKey(
                        name: "fk_property_appeal_property_owner_appellant_owner_id",
                        column: x => x.appellant_owner_id,
                        principalSchema: "property",
                        principalTable: "property_owner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_appeal_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "property",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_auction",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    auction_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    auction_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    auction_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    announcement_date = table.Column<DateOnly>(type: "date", nullable: true),
                    auction_date = table.Column<DateOnly>(type: "date", nullable: true),
                    base_reserve_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    winning_bid_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    successful_bidder_owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    award_date = table.Column<DateOnly>(type: "date", nullable: true),
                    award_reference_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    auction_committee_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    venue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    reference_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_auction", x => x.id);
                    table.ForeignKey(
                        name: "fk_property_auction_auction_status_auction_status_id",
                        column: x => x.auction_status_id,
                        principalSchema: "property",
                        principalTable: "auction_status",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_auction_auction_type_auction_type_id",
                        column: x => x.auction_type_id,
                        principalSchema: "property",
                        principalTable: "auction_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_auction_property_owner_successful_bidder_owner_id",
                        column: x => x.successful_bidder_owner_id,
                        principalSchema: "property",
                        principalTable: "property_owner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_auction_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "property",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_boundary",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    boundary_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "ORIGINAL"),
                    survey_date = table.Column<DateOnly>(type: "date", nullable: true),
                    survey_source = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    slope_percentage = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                    is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_boundary", x => x.id);
                    table.ForeignKey(
                        name: "fk_property_boundary_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "property",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_encroachment",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encroachment_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    encroachment_area = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    measurement_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encroachment_area_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    encroachment_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encroacher_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    encroacher_owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    detection_date = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: true),
                    notice_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    notice_date = table.Column<DateOnly>(type: "date", nullable: true),
                    resolution_date = table.Column<DateOnly>(type: "date", nullable: true),
                    resolution_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    resolution_reference_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_encroachment", x => x.id);
                    table.ForeignKey(
                        name: "fk_property_encroachment_encroachment_status_encroachment_stat",
                        column: x => x.encroachment_status_id,
                        principalSchema: "property",
                        principalTable: "encroachment_status",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_encroachment_measurement_unit_measurement_unit_id",
                        column: x => x.measurement_unit_id,
                        principalSchema: "property",
                        principalTable: "measurement_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_encroachment_property_owner_encroacher_owner_id",
                        column: x => x.encroacher_owner_id,
                        principalSchema: "property",
                        principalTable: "property_owner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_encroachment_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "property",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_lease",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lessee_owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lease_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    lease_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lease_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lease_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    lease_end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    lease_term_years = table.Column<int>(type: "integer", nullable: true),
                    lease_purpose = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    lease_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    amount_frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    security_deposit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    agreement_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    agreement_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_renewable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    renewed_from_lease_id = table.Column<Guid>(type: "uuid", nullable: true),
                    termination_date = table.Column<DateOnly>(type: "date", nullable: true),
                    termination_reason = table.Column<string>(type: "text", nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_lease", x => x.id);
                    table.ForeignKey(
                        name: "fk_property_lease_lease_status_lease_status_id",
                        column: x => x.lease_status_id,
                        principalSchema: "property",
                        principalTable: "lease_status",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_lease_lease_type_lease_type_id",
                        column: x => x.lease_type_id,
                        principalSchema: "property",
                        principalTable: "lease_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_lease_property_lease_renewed_from_lease_id",
                        column: x => x.renewed_from_lease_id,
                        principalSchema: "property",
                        principalTable: "property_lease",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_lease_property_owner_lessee_owner_id",
                        column: x => x.lessee_owner_id,
                        principalSchema: "property",
                        principalTable: "property_owner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_lease_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "property",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_outsourcing",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outsourced_party_owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    outsourcing_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    contract_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    purpose_service = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    contract_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    amount_frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    performance_guarantee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    reference_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    termination_date = table.Column<DateOnly>(type: "date", nullable: true),
                    termination_reason = table.Column<string>(type: "text", nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_outsourcing", x => x.id);
                    table.ForeignKey(
                        name: "fk_property_outsourcing_contract_status_contract_status_id",
                        column: x => x.contract_status_id,
                        principalSchema: "property",
                        principalTable: "contract_status",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_outsourcing_outsourcing_type_outsourcing_type_id",
                        column: x => x.outsourcing_type_id,
                        principalSchema: "property",
                        principalTable: "outsourcing_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_outsourcing_property_owner_outsourced_party_owner_",
                        column: x => x.outsourced_party_owner_id,
                        principalSchema: "property",
                        principalTable: "property_owner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_outsourcing_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "property",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_rental",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rental_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    rental_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rental_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rental_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    rental_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    rent_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    rent_frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "MONTHLY"),
                    security_deposit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    annual_increase_pct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    agreement_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    agreement_date = table.Column<DateOnly>(type: "date", nullable: true),
                    renewed_from_rental_id = table.Column<Guid>(type: "uuid", nullable: true),
                    termination_date = table.Column<DateOnly>(type: "date", nullable: true),
                    termination_reason = table.Column<string>(type: "text", nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_rental", x => x.id);
                    table.ForeignKey(
                        name: "fk_property_rental_property_owner_tenant_owner_id",
                        column: x => x.tenant_owner_id,
                        principalSchema: "property",
                        principalTable: "property_owner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_rental_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "property",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_rental_property_rental_renewed_from_rental_id",
                        column: x => x.renewed_from_rental_id,
                        principalSchema: "property",
                        principalTable: "property_rental",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_rental_rental_status_rental_status_id",
                        column: x => x.rental_status_id,
                        principalSchema: "property",
                        principalTable: "rental_status",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_rental_rental_type_rental_type_id",
                        column: x => x.rental_type_id,
                        principalSchema: "property",
                        principalTable: "rental_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "auction_bid",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    auction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bidder_owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bid_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    bid_rank = table.Column<int>(type: "integer", nullable: true),
                    earnest_money = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    is_winning = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    remarks = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auction_bid", x => x.id);
                    table.ForeignKey(
                        name: "fk_auction_bid_property_auction_auction_id",
                        column: x => x.auction_id,
                        principalSchema: "property",
                        principalTable: "property_auction",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_auction_bid_property_owner_bidder_owner_id",
                        column: x => x.bidder_owner_id,
                        principalSchema: "property",
                        principalTable: "property_owner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "boundary_point",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_boundary_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_no = table.Column<int>(type: "integer", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: false),
                    longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_boundary_point", x => x.id);
                    table.ForeignKey(
                        name: "fk_boundary_point_property_boundary_property_boundary_id",
                        column: x => x.property_boundary_id,
                        principalSchema: "property",
                        principalTable: "property_boundary",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "encroachment_boundary_point",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    encroachment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_no = table.Column<int>(type: "integer", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: false),
                    longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_encroachment_boundary_point", x => x.id);
                    table.ForeignKey(
                        name: "fk_encroachment_boundary_point_property_encroachment_encroachm",
                        column: x => x.encroachment_id,
                        principalSchema: "property",
                        principalTable: "property_encroachment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "property_litigation",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    case_title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    court_authority = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    litigation_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    litigation_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    filing_date = table.Column<DateOnly>(type: "date", nullable: true),
                    gda_role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    filed_by_officer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    related_encroachment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    related_allotment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    related_lease_id = table.Column<Guid>(type: "uuid", nullable: true),
                    next_hearing_date = table.Column<DateOnly>(type: "date", nullable: true),
                    decision_date = table.Column<DateOnly>(type: "date", nullable: true),
                    decision_outcome = table.Column<string>(type: "text", nullable: true),
                    appealed_to = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    parent_litigation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    gda_counsel = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property_litigation", x => x.id);
                    table.ForeignKey(
                        name: "fk_property_litigation_litigation_status_litigation_status_id",
                        column: x => x.litigation_status_id,
                        principalSchema: "property",
                        principalTable: "litigation_status",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_litigation_litigation_type_litigation_type_id",
                        column: x => x.litigation_type_id,
                        principalSchema: "property",
                        principalTable: "litigation_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_litigation_property_allotment_related_allotment_id",
                        column: x => x.related_allotment_id,
                        principalSchema: "property",
                        principalTable: "property_allotment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_litigation_property_encroachment_related_encroachm",
                        column: x => x.related_encroachment_id,
                        principalSchema: "property",
                        principalTable: "property_encroachment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_litigation_property_lease_related_lease_id",
                        column: x => x.related_lease_id,
                        principalSchema: "property",
                        principalTable: "property_lease",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_litigation_property_litigation_parent_litigation_id",
                        column: x => x.parent_litigation_id,
                        principalSchema: "property",
                        principalTable: "property_litigation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_property_litigation_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "property",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agreement_violation",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agreement_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lease_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rental_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transfer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    violator_owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    violation_date = table.Column<DateOnly>(type: "date", nullable: false),
                    violation_description = table.Column<string>(type: "text", nullable: false),
                    occurrence_no = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    notice_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    notice_date = table.Column<DateOnly>(type: "date", nullable: true),
                    notice_deadline = table.Column<DateOnly>(type: "date", nullable: true),
                    fine_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    fine_imposed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    fine_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    led_to_cancellation = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    cancellation_date = table.Column<DateOnly>(type: "date", nullable: true),
                    violation_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "OPEN"),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agreement_violation", x => x.id);
                    table.CheckConstraint("ck_agreement_violation_one_agreement", "num_nonnulls(lease_id, rental_id, transfer_id) = 1");
                    table.ForeignKey(
                        name: "fk_agreement_violation_agreement_type_agreement_type_id",
                        column: x => x.agreement_type_id,
                        principalSchema: "property",
                        principalTable: "agreement_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agreement_violation_property_lease_lease_id",
                        column: x => x.lease_id,
                        principalSchema: "property",
                        principalTable: "property_lease",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agreement_violation_property_owner_violator_owner_id",
                        column: x => x.violator_owner_id,
                        principalSchema: "property",
                        principalTable: "property_owner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agreement_violation_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "property",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agreement_violation_property_rental_rental_id",
                        column: x => x.rental_id,
                        principalSchema: "property",
                        principalTable: "property_rental",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agreement_violation_property_transfer_transfer_id",
                        column: x => x.transfer_id,
                        principalSchema: "property",
                        principalTable: "property_transfer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "litigation_hearing",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    litigation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hearing_date = table.Column<DateOnly>(type: "date", nullable: false),
                    proceedings = table.Column<string>(type: "text", nullable: true),
                    order_passed = table.Column<string>(type: "text", nullable: true),
                    next_hearing_date = table.Column<DateOnly>(type: "date", nullable: true),
                    attended_by = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_litigation_hearing", x => x.id);
                    table.ForeignKey(
                        name: "fk_litigation_hearing_property_litigation_litigation_id",
                        column: x => x.litigation_id,
                        principalSchema: "property",
                        principalTable: "property_litigation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "litigation_party",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    litigation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    party_owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    party_role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    counsel_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    remarks = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_litigation_party", x => x.id);
                    table.ForeignKey(
                        name: "fk_litigation_party_property_litigation_litigation_id",
                        column: x => x.litigation_id,
                        principalSchema: "property",
                        principalTable: "property_litigation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_litigation_party_property_owner_party_owner_id",
                        column: x => x.party_owner_id,
                        principalSchema: "property",
                        principalTable: "property_owner",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_property_ownership_acquired_via_allotment_id",
                schema: "property",
                table: "property_ownership",
                column: "acquired_via_allotment_id");

            migrationBuilder.CreateIndex(
                name: "ix_agreement_violation_agreement_type_id",
                schema: "property",
                table: "agreement_violation",
                column: "agreement_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_agreement_violation_lease_id",
                schema: "property",
                table: "agreement_violation",
                column: "lease_id");

            migrationBuilder.CreateIndex(
                name: "ix_agreement_violation_property_id_violation_date",
                schema: "property",
                table: "agreement_violation",
                columns: new[] { "property_id", "violation_date" });

            migrationBuilder.CreateIndex(
                name: "ix_agreement_violation_rental_id",
                schema: "property",
                table: "agreement_violation",
                column: "rental_id");

            migrationBuilder.CreateIndex(
                name: "ix_agreement_violation_transfer_id",
                schema: "property",
                table: "agreement_violation",
                column: "transfer_id");

            migrationBuilder.CreateIndex(
                name: "ix_agreement_violation_violator_owner_id",
                schema: "property",
                table: "agreement_violation",
                column: "violator_owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_auction_bid_auction_id",
                schema: "property",
                table: "auction_bid",
                column: "auction_id");

            migrationBuilder.CreateIndex(
                name: "ix_auction_bid_bidder_owner_id",
                schema: "property",
                table: "auction_bid",
                column: "bidder_owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_boundary_point_property_boundary_id_sequence_no",
                schema: "property",
                table: "boundary_point",
                columns: new[] { "property_boundary_id", "sequence_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_building_plan_applicant_owner_id",
                schema: "property",
                table: "building_plan",
                column: "applicant_owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_building_plan_building_plan_status_id",
                schema: "property",
                table: "building_plan",
                column: "building_plan_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_building_plan_building_plan_type_id",
                schema: "property",
                table: "building_plan",
                column: "building_plan_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_building_plan_measurement_unit_id",
                schema: "property",
                table: "building_plan",
                column: "measurement_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_building_plan_plan_no_revision_no",
                schema: "property",
                table: "building_plan",
                columns: new[] { "plan_no", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_building_plan_property_id_building_plan_status_id",
                schema: "property",
                table: "building_plan",
                columns: new[] { "property_id", "building_plan_status_id" });

            migrationBuilder.CreateIndex(
                name: "ix_building_plan_supersedes_plan_id",
                schema: "property",
                table: "building_plan",
                column: "supersedes_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_encroachment_boundary_point_encroachment_id_sequence_no",
                schema: "property",
                table: "encroachment_boundary_point",
                columns: new[] { "encroachment_id", "sequence_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_litigation_hearing_litigation_id",
                schema: "property",
                table: "litigation_hearing",
                column: "litigation_id");

            migrationBuilder.CreateIndex(
                name: "ix_litigation_party_litigation_id",
                schema: "property",
                table: "litigation_party",
                column: "litigation_id");

            migrationBuilder.CreateIndex(
                name: "ix_litigation_party_party_owner_id",
                schema: "property",
                table: "litigation_party",
                column: "party_owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_allotment_allotment_no",
                schema: "property",
                table: "property_allotment",
                column: "allotment_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_property_allotment_allotment_status_id",
                schema: "property",
                table: "property_allotment",
                column: "allotment_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_allotment_allotment_type_id",
                schema: "property",
                table: "property_allotment",
                column: "allotment_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_allotment_allottee_owner_id",
                schema: "property",
                table: "property_allotment",
                column: "allottee_owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_allotment_property_id_allotment_status_id",
                schema: "property",
                table: "property_allotment",
                columns: new[] { "property_id", "allotment_status_id" });

            migrationBuilder.CreateIndex(
                name: "ix_property_appeal_appeal_no",
                schema: "property",
                table: "property_appeal",
                column: "appeal_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_property_appeal_appellant_owner_id",
                schema: "property",
                table: "property_appeal",
                column: "appellant_owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_appeal_property_id",
                schema: "property",
                table: "property_appeal",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_auction_auction_no",
                schema: "property",
                table: "property_auction",
                column: "auction_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_property_auction_auction_status_id",
                schema: "property",
                table: "property_auction",
                column: "auction_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_auction_auction_type_id",
                schema: "property",
                table: "property_auction",
                column: "auction_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_auction_property_id_auction_status_id",
                schema: "property",
                table: "property_auction",
                columns: new[] { "property_id", "auction_status_id" });

            migrationBuilder.CreateIndex(
                name: "ix_property_auction_successful_bidder_owner_id",
                schema: "property",
                table: "property_auction",
                column: "successful_bidder_owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_boundary_property_id",
                schema: "property",
                table: "property_boundary",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_encroachment_encroacher_owner_id",
                schema: "property",
                table: "property_encroachment",
                column: "encroacher_owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_encroachment_encroachment_no",
                schema: "property",
                table: "property_encroachment",
                column: "encroachment_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_property_encroachment_encroachment_status_id",
                schema: "property",
                table: "property_encroachment",
                column: "encroachment_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_encroachment_measurement_unit_id",
                schema: "property",
                table: "property_encroachment",
                column: "measurement_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_encroachment_property_id_encroachment_status_id",
                schema: "property",
                table: "property_encroachment",
                columns: new[] { "property_id", "encroachment_status_id" });

            migrationBuilder.CreateIndex(
                name: "ix_property_lease_lease_end_date",
                schema: "property",
                table: "property_lease",
                column: "lease_end_date");

            migrationBuilder.CreateIndex(
                name: "ix_property_lease_lease_no",
                schema: "property",
                table: "property_lease",
                column: "lease_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_property_lease_lease_status_id",
                schema: "property",
                table: "property_lease",
                column: "lease_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_lease_lease_type_id",
                schema: "property",
                table: "property_lease",
                column: "lease_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_lease_lessee_owner_id",
                schema: "property",
                table: "property_lease",
                column: "lessee_owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_lease_property_id_lease_status_id",
                schema: "property",
                table: "property_lease",
                columns: new[] { "property_id", "lease_status_id" });

            migrationBuilder.CreateIndex(
                name: "ix_property_lease_renewed_from_lease_id",
                schema: "property",
                table: "property_lease",
                column: "renewed_from_lease_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_litigation_case_no_court_authority",
                schema: "property",
                table: "property_litigation",
                columns: new[] { "case_no", "court_authority" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_property_litigation_litigation_status_id",
                schema: "property",
                table: "property_litigation",
                column: "litigation_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_litigation_litigation_type_id",
                schema: "property",
                table: "property_litigation",
                column: "litigation_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_litigation_next_hearing_date",
                schema: "property",
                table: "property_litigation",
                column: "next_hearing_date");

            migrationBuilder.CreateIndex(
                name: "ix_property_litigation_parent_litigation_id",
                schema: "property",
                table: "property_litigation",
                column: "parent_litigation_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_litigation_property_id_litigation_status_id",
                schema: "property",
                table: "property_litigation",
                columns: new[] { "property_id", "litigation_status_id" });

            migrationBuilder.CreateIndex(
                name: "ix_property_litigation_related_allotment_id",
                schema: "property",
                table: "property_litigation",
                column: "related_allotment_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_litigation_related_encroachment_id",
                schema: "property",
                table: "property_litigation",
                column: "related_encroachment_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_litigation_related_lease_id",
                schema: "property",
                table: "property_litigation",
                column: "related_lease_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_outsourcing_contract_no",
                schema: "property",
                table: "property_outsourcing",
                column: "contract_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_property_outsourcing_contract_status_id",
                schema: "property",
                table: "property_outsourcing",
                column: "contract_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_outsourcing_outsourced_party_owner_id",
                schema: "property",
                table: "property_outsourcing",
                column: "outsourced_party_owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_outsourcing_outsourcing_type_id",
                schema: "property",
                table: "property_outsourcing",
                column: "outsourcing_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_outsourcing_property_id_contract_status_id",
                schema: "property",
                table: "property_outsourcing",
                columns: new[] { "property_id", "contract_status_id" });

            migrationBuilder.CreateIndex(
                name: "ix_property_rental_property_id_rental_status_id",
                schema: "property",
                table: "property_rental",
                columns: new[] { "property_id", "rental_status_id" });

            migrationBuilder.CreateIndex(
                name: "ix_property_rental_renewed_from_rental_id",
                schema: "property",
                table: "property_rental",
                column: "renewed_from_rental_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_rental_rental_no",
                schema: "property",
                table: "property_rental",
                column: "rental_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_property_rental_rental_status_id",
                schema: "property",
                table: "property_rental",
                column: "rental_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_rental_rental_type_id",
                schema: "property",
                table: "property_rental",
                column: "rental_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_property_rental_tenant_owner_id",
                schema: "property",
                table: "property_rental",
                column: "tenant_owner_id");

            migrationBuilder.AddForeignKey(
                name: "fk_property_ownership_property_allotment_acquired_via_allotmen",
                schema: "property",
                table: "property_ownership",
                column: "acquired_via_allotment_id",
                principalSchema: "property",
                principalTable: "property_allotment",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS property.ix_property_owner_owner_name;");

            migrationBuilder.DropForeignKey(
                name: "fk_property_ownership_property_allotment_acquired_via_allotmen",
                schema: "property",
                table: "property_ownership");

            migrationBuilder.DropTable(
                name: "agreement_violation",
                schema: "property");

            migrationBuilder.DropTable(
                name: "auction_bid",
                schema: "property");

            migrationBuilder.DropTable(
                name: "boundary_point",
                schema: "property");

            migrationBuilder.DropTable(
                name: "building_plan",
                schema: "property");

            migrationBuilder.DropTable(
                name: "encroachment_boundary_point",
                schema: "property");

            migrationBuilder.DropTable(
                name: "litigation_hearing",
                schema: "property");

            migrationBuilder.DropTable(
                name: "litigation_party",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_appeal",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_outsourcing",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_rental",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_auction",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_boundary",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_litigation",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_allotment",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_encroachment",
                schema: "property");

            migrationBuilder.DropTable(
                name: "property_lease",
                schema: "property");

            migrationBuilder.DropIndex(
                name: "ix_property_ownership_acquired_via_allotment_id",
                schema: "property",
                table: "property_ownership");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "transfer_type",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "code",
                schema: "property",
                table: "town",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "tenure_type",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "rental_type",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "rental_status",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "property_type",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "property_status",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "property_classification",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "owner_type",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "outsourcing_type",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "measurement_unit",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "code",
                schema: "property",
                table: "measurement_unit",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "litigation_type",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "litigation_status",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "lease_type",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "lease_status",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "encumbrance_type",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "encroachment_status",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "document_type",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "contract_status",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "contact_type",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "code",
                schema: "property",
                table: "contact_type",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "building_plan_type",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "building_plan_status",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "auction_type",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "auction_status",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "attribute_group",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "allotment_type",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "allotment_status",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "property",
                table: "agreement_type",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);
        }
    }
}
