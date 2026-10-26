using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class OwnershipHistoryRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "transfer_date_precision",
                schema: "property",
                table: "property_transfer",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValueSql: "'EXACT'");

            migrationBuilder.AddColumn<string>(
                name: "effective_from_precision",
                schema: "property",
                table: "property_ownership",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValueSql: "'EXACT'");

            migrationBuilder.AddColumn<DateOnly>(
                name: "date_of_death",
                schema: "property",
                table: "property_owner",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deceased",
                schema: "property",
                table: "property_owner",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "old_id_no",
                schema: "property",
                table: "property_owner",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "old_id_type",
                schema: "property",
                table: "property_owner",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "registered_on",
                schema: "property",
                table: "property",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "registered_on_precision",
                schema: "property",
                table: "property",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValueSql: "'EXACT'");

            migrationBuilder.AddColumn<string>(
                name: "registration_reference",
                schema: "property",
                table: "property",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ownership_history_entry",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    steps = table.Column<string>(type: "jsonb", nullable: false),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    recorded_by = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    recorded_on = table.Column<DateOnly>(type: "date", nullable: false),
                    decided_by = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_on = table.Column<DateOnly>(type: "date", nullable: true),
                    decision_reason = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ownership_history_entry", x => x.id);
                    table.ForeignKey(
                        name: "fk_ownership_history_entry_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "property",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ownership_history_entry_entry_no",
                schema: "property",
                table: "ownership_history_entry",
                column: "entry_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ownership_history_entry_property_id_status",
                schema: "property",
                table: "ownership_history_entry",
                columns: new[] { "property_id", "status" });

            // No "Other" catch-all in any list (the user's rule): the OTHER values an earlier seed added are switched off.
            // Records that use one keep its label; the value is just no longer offered.
            migrationBuilder.Sql(@"
DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY['property_type', 'property_status', 'property_classification', 'transfer_type', 'owner_type',
    'contact_type', 'tenure_type', 'document_type', 'encumbrance_type', 'building_plan_type', 'auction_type',
    'litigation_status', 'litigation_type', 'agreement_type'] LOOP
    IF to_regclass('property.' || t) IS NOT NULL THEN
      EXECUTE format('UPDATE property.%I SET is_active = false WHERE code = ''OTHER'' AND is_active', t);
    END IF;
  END LOOP;
END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ownership_history_entry",
                schema: "property");

            migrationBuilder.DropColumn(
                name: "transfer_date_precision",
                schema: "property",
                table: "property_transfer");

            migrationBuilder.DropColumn(
                name: "effective_from_precision",
                schema: "property",
                table: "property_ownership");

            migrationBuilder.DropColumn(
                name: "date_of_death",
                schema: "property",
                table: "property_owner");

            migrationBuilder.DropColumn(
                name: "is_deceased",
                schema: "property",
                table: "property_owner");

            migrationBuilder.DropColumn(
                name: "old_id_no",
                schema: "property",
                table: "property_owner");

            migrationBuilder.DropColumn(
                name: "old_id_type",
                schema: "property",
                table: "property_owner");

            migrationBuilder.DropColumn(
                name: "registered_on",
                schema: "property",
                table: "property");

            migrationBuilder.DropColumn(
                name: "registered_on_precision",
                schema: "property",
                table: "property");

            migrationBuilder.DropColumn(
                name: "registration_reference",
                schema: "property",
                table: "property");
        }
    }
}
