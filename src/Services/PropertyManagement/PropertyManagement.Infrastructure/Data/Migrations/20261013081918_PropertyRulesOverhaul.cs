using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PropertyRulesOverhaul : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "registration_no",
                schema: "property",
                table: "property_owner");

            migrationBuilder.AddColumn<string>(
                name: "approval_order_ref",
                schema: "property",
                table: "property_transfer",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "closed_by",
                schema: "property",
                table: "property_transfer",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "closed_on",
                schema: "property",
                table: "property_transfer",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "closing_reason",
                schema: "property",
                table: "property_transfer",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "completed_by",
                schema: "property",
                table: "property_transfer",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "completed_on",
                schema: "property",
                table: "property_transfer",
                type: "date",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "property_id",
                schema: "property",
                table: "property_document",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<decimal>(
                name: "slope_degrees",
                schema: "property",
                table: "property_boundary",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "previous_auction_id",
                schema: "property",
                table: "property_auction",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "surrender_date",
                schema: "property",
                table: "property_allotment",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "applies_to",
                schema: "property",
                table: "document_type",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PROPERTY");

            migrationBuilder.AddColumn<string>(
                name: "pre_appeal_status",
                schema: "property",
                table: "agreement_violation",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Slope is now recorded in degrees (BCR 2026 states its limit as 45 degrees): convert the stored
            // percentages (rise / run x 100) before the old column goes.
            migrationBuilder.Sql(@"UPDATE property.property_boundary
                SET slope_degrees = round(degrees(atan(slope_percentage / 100.0))::numeric, 2)
                WHERE slope_percentage IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "slope_percentage",
                schema: "property",
                table: "property_boundary");

            // An owner's papers are filed on the owner, not under a property; CNIC copies are owner documents.
            migrationBuilder.Sql(@"UPDATE property.property_document SET property_id = NULL WHERE entity_type = 'OWNER';");
            migrationBuilder.Sql(@"UPDATE property.document_type SET applies_to = 'OWNER' WHERE code = 'CNIC_COPY';");

            // Transfers completed before completion was recorded: completed when approved (or on the transfer date).
            migrationBuilder.Sql(@"UPDATE property.property_transfer
                SET completed_on = COALESCE(approval_date, transfer_date), completed_by = approved_by
                WHERE transfer_status = 'COMPLETED' AND completed_on IS NULL;");

            migrationBuilder.CreateIndex(
                name: "ix_property_auction_previous_auction_id",
                schema: "property",
                table: "property_auction",
                column: "previous_auction_id");

            migrationBuilder.AddForeignKey(
                name: "fk_property_auction_property_auction_previous_auction_id",
                schema: "property",
                table: "property_auction",
                column: "previous_auction_id",
                principalSchema: "property",
                principalTable: "property_auction",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_property_auction_property_auction_previous_auction_id",
                schema: "property",
                table: "property_auction");

            migrationBuilder.DropIndex(
                name: "ix_property_auction_previous_auction_id",
                schema: "property",
                table: "property_auction");

            migrationBuilder.DropColumn(
                name: "approval_order_ref",
                schema: "property",
                table: "property_transfer");

            migrationBuilder.DropColumn(
                name: "closed_by",
                schema: "property",
                table: "property_transfer");

            migrationBuilder.DropColumn(
                name: "closed_on",
                schema: "property",
                table: "property_transfer");

            migrationBuilder.DropColumn(
                name: "closing_reason",
                schema: "property",
                table: "property_transfer");

            migrationBuilder.DropColumn(
                name: "completed_by",
                schema: "property",
                table: "property_transfer");

            migrationBuilder.DropColumn(
                name: "completed_on",
                schema: "property",
                table: "property_transfer");

            migrationBuilder.DropColumn(
                name: "slope_degrees",
                schema: "property",
                table: "property_boundary");

            migrationBuilder.DropColumn(
                name: "previous_auction_id",
                schema: "property",
                table: "property_auction");

            migrationBuilder.DropColumn(
                name: "surrender_date",
                schema: "property",
                table: "property_allotment");

            migrationBuilder.DropColumn(
                name: "applies_to",
                schema: "property",
                table: "document_type");

            migrationBuilder.DropColumn(
                name: "pre_appeal_status",
                schema: "property",
                table: "agreement_violation");

            migrationBuilder.AddColumn<string>(
                name: "registration_no",
                schema: "property",
                table: "property_owner",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "property_id",
                schema: "property",
                table: "property_document",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "slope_percentage",
                schema: "property",
                table: "property_boundary",
                type: "numeric(8,4)",
                precision: 8,
                scale: 4,
                nullable: true);
        }
    }
}
