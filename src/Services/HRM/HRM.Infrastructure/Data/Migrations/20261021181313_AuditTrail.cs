using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_entry",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    module = table.Column<string>(type: "varchar", maxLength: 60, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_name = table.Column<string>(type: "varchar", maxLength: 200, nullable: false),
                    action = table.Column<string>(type: "varchar", maxLength: 40, nullable: false),
                    record_kind = table.Column<string>(type: "varchar", maxLength: 100, nullable: false),
                    record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    record_code = table.Column<string>(type: "varchar", maxLength: 100, nullable: true),
                    record_label = table.Column<string>(type: "varchar", maxLength: 300, nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_code = table.Column<string>(type: "varchar", maxLength: 100, nullable: true),
                    field = table.Column<string>(type: "varchar", maxLength: 150, nullable: true),
                    old_value = table.Column<string>(type: "varchar", maxLength: 4000, nullable: true),
                    new_value = table.Column<string>(type: "varchar", maxLength: 4000, nullable: true),
                    detail = table.Column<string>(type: "varchar", nullable: true),
                    ip_address = table.Column<string>(type: "varchar", maxLength: 64, nullable: true),
                    device = table.Column<string>(type: "varchar", maxLength: 300, nullable: true),
                    request_id = table.Column<string>(type: "varchar", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("audit_entry_pkey", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_actor_id_at",
                schema: "hrms",
                table: "audit_entry",
                columns: new[] { "actor_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_at",
                schema: "hrms",
                table: "audit_entry",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_parent_id_at",
                schema: "hrms",
                table: "audit_entry",
                columns: new[] { "parent_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_record_id_at",
                schema: "hrms",
                table: "audit_entry",
                columns: new[] { "record_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_record_kind_at",
                schema: "hrms",
                table: "audit_entry",
                columns: new[] { "record_kind", "at" });

            // history is one list from the start: the employee change log's rows move into the trail
            migrationBuilder.Sql(AuditTrailMigrationSql.CopyChangeLog);

            // append-only: no UPDATE, DELETE or TRUNCATE on the trail
            migrationBuilder.Sql(AuditTrailSql.Protect("hrms"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AuditTrailSql.Unprotect("hrms"));

            migrationBuilder.DropTable(
                name: "audit_entry",
                schema: "hrms");
        }
    }
}
