using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class OwnershipHistoryFromOldRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "from_history_entry_id",
                schema: "property",
                table: "property_transfer",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transfer_date_precision",
                schema: "property",
                table: "property_transfer",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValueSql: "'EXACT'");

            migrationBuilder.AddColumn<Guid>(
                name: "closed_by_transfer_id",
                schema: "property",
                table: "property_ownership",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "effective_from_precision",
                schema: "property",
                table: "property_ownership",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValueSql: "'EXACT'");

            migrationBuilder.AddColumn<Guid>(
                name: "opened_by_transfer_id",
                schema: "property",
                table: "property_ownership",
                type: "uuid",
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
                    last_edited_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ownership_history_entry",
                schema: "property");

            migrationBuilder.DropColumn(
                name: "from_history_entry_id",
                schema: "property",
                table: "property_transfer");

            migrationBuilder.DropColumn(
                name: "transfer_date_precision",
                schema: "property",
                table: "property_transfer");

            migrationBuilder.DropColumn(
                name: "closed_by_transfer_id",
                schema: "property",
                table: "property_ownership");

            migrationBuilder.DropColumn(
                name: "effective_from_precision",
                schema: "property",
                table: "property_ownership");

            migrationBuilder.DropColumn(
                name: "opened_by_transfer_id",
                schema: "property",
                table: "property_ownership");
        }
    }
}
