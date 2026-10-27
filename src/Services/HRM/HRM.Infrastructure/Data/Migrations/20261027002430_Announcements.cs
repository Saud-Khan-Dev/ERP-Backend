using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Announcements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "announcement",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    title = table.Column<string>(type: "varchar", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    org_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    show_from = table.Column<DateOnly>(type: "date", nullable: false),
                    show_until = table.Column<DateOnly>(type: "date", nullable: true),
                    is_pinned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    file_path = table.Column<string>(type: "varchar", nullable: true),
                    file_name = table.Column<string>(type: "varchar", nullable: true),
                    is_withdrawn = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    withdrawn_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    withdrawn_by = table.Column<Guid>(type: "uuid", nullable: true),
                    withdrawn_reason = table.Column<string>(type: "varchar", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("announcement_pkey", x => x.id);
                    table.CheckConstraint("ck_ann_dates", "show_until IS NULL OR show_until >= show_from");
                    table.CheckConstraint("ck_ann_file", "(file_path IS NULL) = (file_name IS NULL)");
                    table.CheckConstraint("ck_ann_withdrawn", "NOT is_withdrawn OR (withdrawn_at IS NOT NULL AND withdrawn_by IS NOT NULL AND withdrawn_reason IS NOT NULL)");
                    table.ForeignKey(
                        name: "announcement_org_unit_id_fkey",
                        column: x => x.org_unit_id,
                        principalSchema: "hrms",
                        principalTable: "organization_unit",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "announcement_read",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    announcement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("announcement_read_pkey", x => x.id);
                    table.ForeignKey(
                        name: "announcement_read_announcement_id_fkey",
                        column: x => x.announcement_id,
                        principalSchema: "hrms",
                        principalTable: "announcement",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "announcement_read_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "idx_ann_org_unit",
                schema: "hrms",
                table: "announcement",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "idx_ann_showing",
                schema: "hrms",
                table: "announcement",
                columns: new[] { "show_from", "show_until" });

            migrationBuilder.CreateIndex(
                name: "idx_ann_read_employee",
                schema: "hrms",
                table: "announcement_read",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ux_ann_read_once",
                schema: "hrms",
                table: "announcement_read",
                columns: new[] { "announcement_id", "employee_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcement_read",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "announcement",
                schema: "hrms");
        }
    }
}
