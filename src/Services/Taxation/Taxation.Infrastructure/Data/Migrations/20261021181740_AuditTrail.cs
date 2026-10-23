using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taxation.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "tax_file_id",
                schema: "taxation",
                table: "penalty",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tax_file_id",
                schema: "taxation",
                table: "notice",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tax_file_id",
                schema: "taxation",
                table: "agreement_step_revision",
                type: "uuid",
                nullable: true);

            // the file of every notice, surcharge and step revision, from its demand or agreement
            migrationBuilder.Sql("""
                UPDATE taxation.notice n SET tax_file_id = d.tax_file_id FROM taxation.demand d WHERE d.id = n.demand_id;
                UPDATE taxation.penalty p SET tax_file_id = d.tax_file_id FROM taxation.demand d WHERE d.id = p.demand_id;
                UPDATE taxation.agreement_step_revision r SET tax_file_id = a.tax_file_id FROM taxation.agreement_schedule a WHERE a.id = r.schedule_id;
                ALTER TABLE taxation.notice ALTER COLUMN tax_file_id SET NOT NULL;
                ALTER TABLE taxation.penalty ALTER COLUMN tax_file_id SET NOT NULL;
                ALTER TABLE taxation.agreement_step_revision ALTER COLUMN tax_file_id SET NOT NULL;
                """);

            migrationBuilder.CreateTable(
                name: "audit_entry",
                schema: "taxation",
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
                    table.PrimaryKey("audit_entry_pkey", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_penalty_tax_file_id",
                schema: "taxation",
                table: "penalty",
                column: "tax_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_notice_tax_file_id",
                schema: "taxation",
                table: "notice",
                column: "tax_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_agreement_step_revision_tax_file_id",
                schema: "taxation",
                table: "agreement_step_revision",
                column: "tax_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_actor_id_at",
                schema: "taxation",
                table: "audit_entry",
                columns: new[] { "actor_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_at",
                schema: "taxation",
                table: "audit_entry",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_parent_id_at",
                schema: "taxation",
                table: "audit_entry",
                columns: new[] { "parent_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_record_id_at",
                schema: "taxation",
                table: "audit_entry",
                columns: new[] { "record_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_record_kind_at",
                schema: "taxation",
                table: "audit_entry",
                columns: new[] { "record_kind", "at" });

            migrationBuilder.AddForeignKey(
                name: "agreement_step_revision_tax_file_id_fkey",
                schema: "taxation",
                table: "agreement_step_revision",
                column: "tax_file_id",
                principalSchema: "taxation",
                principalTable: "tax_file",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "notice_tax_file_id_fkey",
                schema: "taxation",
                table: "notice",
                column: "tax_file_id",
                principalSchema: "taxation",
                principalTable: "tax_file",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "penalty_tax_file_id_fkey",
                schema: "taxation",
                table: "penalty",
                column: "tax_file_id",
                principalSchema: "taxation",
                principalTable: "tax_file",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // the change history kept until now goes into the trail, so a record's history is one list
            migrationBuilder.Sql(CopyAuditLog);

            // the old history stays as it was: read-only from now on
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION taxation.audit_log_read_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  RAISE EXCEPTION 'taxation.audit_log is kept read-only; the audit trail is taxation.audit_entry.';
                END $$;
                CREATE TRIGGER audit_log_read_only BEFORE INSERT OR UPDATE OR DELETE ON taxation.audit_log
                  FOR EACH ROW EXECUTE FUNCTION taxation.audit_log_read_only();
                CREATE TRIGGER audit_log_no_truncate BEFORE TRUNCATE ON taxation.audit_log
                  FOR EACH STATEMENT EXECUTE FUNCTION taxation.audit_log_read_only();
                """);

            migrationBuilder.Sql(AuditTrailSql.Protect("taxation"));
        }

        /// <summary>One line per fact, as the trail writes them: an added or removed record with its facts, a change per
        /// field with the values before and after. Kinds and fields in words; the file's number as the parent.</summary>
        private const string CopyAuditLog = """
            WITH kinds(entity, kind) AS (VALUES
              ('tax_file', 'Tax file'), ('tax_file_override', 'Override'), ('litigation_hold', 'Litigation hold'), ('tax_file_head_value', 'Head value'),
              ('agreement_schedule', 'Agreement'), ('agreement_step_revision', 'Agreement step revision'), ('arrears', 'Arrears'), ('fee', 'Fee'),
              ('clearance', 'Clearance'), ('payment', 'Payment'), ('party', 'Party'), ('demand_run', 'Demand run'), ('penalty_run', 'Surcharge run'),
              ('tax_circle', 'Circle'), ('tax_circle_town', 'Town in circle'), ('tax_circle_staff', 'Circle post'), ('tax_head', 'Tax head'),
              ('category_map', 'Category mapping'), ('rate_set', 'Rate set'), ('tax_setting', 'Setting'), ('message_template', 'SMS text'),
              ('special_policy', 'Special policy'), ('penalty_rule', 'Surcharge rule'), ('notice_template', 'Notice template'),
              ('notice_letterhead', 'Letterhead'), ('notice_table_layout', 'Notice table')),
            log AS (
              SELECT l.*, coalesce(k.kind, initcap(replace(l.entity, '_', ' '))) AS kind, f.file_no,
                coalesce(l.new_values, '{}'::jsonb) || coalesce(l.old_values, '{}'::jsonb) AS facts,
                CASE l.entity
                  WHEN 'tax_file' THEN (SELECT x.file_no FROM taxation.tax_file x WHERE x.id = l.entity_id)
                  WHEN 'clearance' THEN (SELECT x.clearance_no FROM taxation.clearance x WHERE x.id = l.entity_id)
                  WHEN 'fee' THEN (SELECT x.fee_no FROM taxation.fee x WHERE x.id = l.entity_id)
                  WHEN 'payment' THEN (SELECT coalesce(x.receipt_no, x.transaction_ref) FROM taxation.payment x WHERE x.id = l.entity_id)
                  WHEN 'party' THEN (SELECT x.name FROM taxation.party x WHERE x.id = l.entity_id)
                  WHEN 'rate_set' THEN (SELECT x.name FROM taxation.rate_set x WHERE x.id = l.entity_id)
                  WHEN 'tax_head' THEN (SELECT x.code FROM taxation.tax_head x WHERE x.id = l.entity_id)
                END AS known_code
              FROM taxation.audit_log l
              LEFT JOIN kinds k ON k.entity = l.entity
              LEFT JOIN taxation.tax_file f ON f.id = l.tax_file_id)
            INSERT INTO taxation.audit_entry (id, at, module, actor_id, actor_name, action, record_kind, record_id, record_code, record_label, parent_id, parent_code,
              field, old_value, new_value, detail)
            SELECT gen_random_uuid(), log.at, 'Taxation', log.user_id, coalesce(nullif(log.user_name, ''), 'System'),
              CASE log.action WHEN 'ADDED' THEN 'Added' ELSE 'Removed' END, log.kind, log.entity_id,
              left(coalesce(log.known_code, log.facts->>'fileNo', log.facts->>'feeNo', log.facts->>'clearanceNo', log.facts->>'receiptNo', log.facts->>'code', log.facts->>'key'), 100),
              left(coalesce(log.facts->>'name', log.facts->>'payerName'), 300),
              coalesce(log.tax_file_id, log.entity_id), log.file_no, NULL, NULL, NULL,
              (SELECT string_agg(upper(left(w.words, 1)) || substr(w.words, 2) || ': ' || e.value, ' · ' ORDER BY e.key)
                 FROM jsonb_each_text(CASE log.action WHEN 'ADDED' THEN log.new_values ELSE log.old_values END) e
                 CROSS JOIN LATERAL (SELECT lower(regexp_replace(e.key, '([a-z0-9])([A-Z])', '\1 \2', 'g')) AS words) w
                 WHERE e.value IS NOT NULL)
            FROM log WHERE log.action IN ('ADDED', 'DELETED')
            UNION ALL
            SELECT gen_random_uuid(), log.at, 'Taxation', log.user_id, coalesce(nullif(log.user_name, ''), 'System'), 'Changed', log.kind, log.entity_id,
              left(log.known_code, 100), NULL, coalesce(log.tax_file_id, log.entity_id), log.file_no,
              left(upper(left(w.words, 1)) || substr(w.words, 2), 150), left(log.old_values->>k.key, 4000), left(log.new_values->>k.key, 4000), NULL
            FROM log
            CROSS JOIN LATERAL jsonb_object_keys(log.facts) AS k(key)
            CROSS JOIN LATERAL (SELECT lower(regexp_replace(k.key, '([a-z0-9])([A-Z])', '\1 \2', 'g')) AS words) w
            WHERE log.action = 'MODIFIED';
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AuditTrailSql.Unprotect("taxation"));
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS audit_log_read_only ON taxation.audit_log;
                DROP TRIGGER IF EXISTS audit_log_no_truncate ON taxation.audit_log;
                DROP FUNCTION IF EXISTS taxation.audit_log_read_only();
                """);

            migrationBuilder.DropForeignKey(
                name: "agreement_step_revision_tax_file_id_fkey",
                schema: "taxation",
                table: "agreement_step_revision");

            migrationBuilder.DropForeignKey(
                name: "notice_tax_file_id_fkey",
                schema: "taxation",
                table: "notice");

            migrationBuilder.DropForeignKey(
                name: "penalty_tax_file_id_fkey",
                schema: "taxation",
                table: "penalty");

            migrationBuilder.DropTable(
                name: "audit_entry",
                schema: "taxation");

            migrationBuilder.DropIndex(
                name: "ix_penalty_tax_file_id",
                schema: "taxation",
                table: "penalty");

            migrationBuilder.DropIndex(
                name: "ix_notice_tax_file_id",
                schema: "taxation",
                table: "notice");

            migrationBuilder.DropIndex(
                name: "ix_agreement_step_revision_tax_file_id",
                schema: "taxation",
                table: "agreement_step_revision");

            migrationBuilder.DropColumn(
                name: "tax_file_id",
                schema: "taxation",
                table: "penalty");

            migrationBuilder.DropColumn(
                name: "tax_file_id",
                schema: "taxation",
                table: "notice");

            migrationBuilder.DropColumn(
                name: "tax_file_id",
                schema: "taxation",
                table: "agreement_step_revision");
        }
    }
}
