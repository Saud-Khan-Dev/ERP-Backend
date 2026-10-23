using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_entry",
                schema: "property",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    module = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    record_kind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    record_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    record_label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    field = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    old_value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    new_value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    detail = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    device = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_entry", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_actor_id_at",
                schema: "property",
                table: "audit_entry",
                columns: new[] { "actor_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_at",
                schema: "property",
                table: "audit_entry",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_parent_id_at",
                schema: "property",
                table: "audit_entry",
                columns: new[] { "parent_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_record_id_at",
                schema: "property",
                table: "audit_entry",
                columns: new[] { "record_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_record_kind_at",
                schema: "property",
                table: "audit_entry",
                columns: new[] { "record_kind", "at" });

            // the history written so far by the old change log (property_change, kept read-only) moves into the trail, so a
            // record's History is one list: kinds in the trail's words, fields without the " id" suffix, the parent's
            // code, the actor's display name where Identity knows the account (else the account name as it stood)
            migrationBuilder.Sql("""
                DO $$
                DECLARE has_users boolean := to_regclass('auth.users') IS NOT NULL;
                BEGIN
                  EXECUTE format($sql$
                    INSERT INTO property.audit_entry (id, at, module, actor_id, actor_name, action, record_kind, record_id, record_code,
                      record_label, parent_id, parent_code, field, old_value, new_value, detail)
                    SELECT gen_random_uuid(), c.changed_at, 'Property', c.changed_by,
                      left(coalesce(%s, c.changed_by_name, 'System'), 200),
                      c.action,
                      CASE c.entity
                        WHEN 'Litigation' THEN 'Court case'
                        WHEN 'Outsourcing' THEN 'Outsourcing contract'
                        WHEN 'Agreement violation' THEN 'Violation'
                        WHEN 'Boundary' THEN 'Boundary survey'
                        WHEN 'Status history' THEN 'Status'
                        WHEN 'Attribute value' THEN 'Custom field value'
                        ELSE c.entity END,
                      c.entity_id, c.entity_code,
                      CASE WHEN c.entity = 'Property' THEN p.property_name WHEN c.entity = 'Owner' THEN o.owner_name END,
                      coalesce(c.property_id, c.owner_id),
                      coalesce(p.property_code, o.owner_code),
                      CASE WHEN c.field IS NULL THEN NULL ELSE regexp_replace(c.field, ' id$', '') END,
                      c.old_value, c.new_value,
                      'Moved from the earlier change log'
                    FROM property.property_change c
                    LEFT JOIN property.property p ON p.id = c.property_id
                    LEFT JOIN property.property_owner o ON o.id = c.owner_id
                  $sql$, CASE WHEN has_users THEN '(SELECT nullif(u.display_name, '''') FROM auth.users u WHERE u.id = c.changed_by)' ELSE 'NULL' END);
                END $$;
                """);

            // append-only from here on: UPDATE, DELETE and TRUNCATE are refused by triggers
            migrationBuilder.Sql(AuditTrailSql.Protect(ApplicationDbContext.Schema));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AuditTrailSql.Unprotect(ApplicationDbContext.Schema));

            migrationBuilder.DropTable(
                name: "audit_entry",
                schema: "property");
        }
    }
}
