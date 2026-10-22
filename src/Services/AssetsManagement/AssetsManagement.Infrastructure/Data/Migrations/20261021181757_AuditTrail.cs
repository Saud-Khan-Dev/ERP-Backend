using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetsManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_asset_acquisition_asset_asset_id",
                schema: "assets",
                table: "asset_acquisition");

            migrationBuilder.DropForeignKey(
                name: "fk_asset_assignment_asset_asset_id",
                schema: "assets",
                table: "asset_assignment");

            migrationBuilder.DropForeignKey(
                name: "fk_asset_attachment_asset_asset_id",
                schema: "assets",
                table: "asset_attachment");

            migrationBuilder.DropForeignKey(
                name: "fk_asset_attribute_history_asset_asset_id",
                schema: "assets",
                table: "asset_attribute_history");

            migrationBuilder.DropForeignKey(
                name: "fk_asset_attribute_history_attribute_definition_attribute_defi",
                schema: "assets",
                table: "asset_attribute_history");

            migrationBuilder.DropForeignKey(
                name: "fk_asset_depreciation_schedule_asset_asset_id",
                schema: "assets",
                table: "asset_depreciation_schedule");

            migrationBuilder.DropForeignKey(
                name: "fk_asset_disposal_asset_asset_id",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.DropForeignKey(
                name: "fk_asset_lifecycle_event_asset_asset_id",
                schema: "assets",
                table: "asset_lifecycle_event");

            migrationBuilder.DropForeignKey(
                name: "fk_asset_valuation_history_asset_asset_id",
                schema: "assets",
                table: "asset_valuation_history");

            migrationBuilder.DropIndex(
                name: "ix_asset_disposal_asset_id",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                schema: "assets",
                table: "asset_disposal",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at",
                schema: "assets",
                table: "asset_disposal",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cancelled_by",
                schema: "assets",
                table: "asset_disposal",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancelled_by_name",
                schema: "assets",
                table: "asset_disposal",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "decided_at",
                schema: "assets",
                table: "asset_disposal",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "decided_by",
                schema: "assets",
                table: "asset_disposal",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "decided_by_name",
                schema: "assets",
                table: "asset_disposal",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "decision_note",
                schema: "assets",
                table: "asset_disposal",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "proposed_at",
                schema: "assets",
                table: "asset_disposal",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "proposed_by",
                schema: "assets",
                table: "asset_disposal",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "proposed_by_name",
                schema: "assets",
                table: "asset_disposal",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // every disposal recorded before the two steps was approved when it was recorded
            migrationBuilder.AddColumn<string>(
                name: "state",
                schema: "assets",
                table: "asset_disposal",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Approved");

            migrationBuilder.AddColumn<Guid>(
                name: "to_status_id",
                schema: "assets",
                table: "asset_disposal",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reversal_reason",
                schema: "assets",
                table: "asset_depreciation_entry",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audit_entry",
                schema: "assets",
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
                name: "ix_asset_disposal_asset_id_state",
                schema: "assets",
                table: "asset_disposal",
                columns: new[] { "asset_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_asset_disposal_to_status_id",
                schema: "assets",
                table: "asset_disposal",
                column: "to_status_id");

            migrationBuilder.CreateIndex(
                name: "ux_asset_disposal_live",
                schema: "assets",
                table: "asset_disposal",
                column: "asset_id",
                unique: true,
                filter: "state IN ('Proposed', 'Approved')");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_actor_id_at",
                schema: "assets",
                table: "audit_entry",
                columns: new[] { "actor_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_at",
                schema: "assets",
                table: "audit_entry",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_parent_id_at",
                schema: "assets",
                table: "audit_entry",
                columns: new[] { "parent_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_record_id_at",
                schema: "assets",
                table: "audit_entry",
                columns: new[] { "record_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_record_kind_at",
                schema: "assets",
                table: "audit_entry",
                columns: new[] { "record_kind", "at" });

            migrationBuilder.AddForeignKey(
                name: "fk_asset_acquisition_asset_asset_id",
                schema: "assets",
                table: "asset_acquisition",
                column: "asset_id",
                principalSchema: "assets",
                principalTable: "asset",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_asset_assignment_asset_asset_id",
                schema: "assets",
                table: "asset_assignment",
                column: "asset_id",
                principalSchema: "assets",
                principalTable: "asset",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_asset_attachment_asset_asset_id",
                schema: "assets",
                table: "asset_attachment",
                column: "asset_id",
                principalSchema: "assets",
                principalTable: "asset",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_asset_attribute_history_asset_asset_id",
                schema: "assets",
                table: "asset_attribute_history",
                column: "asset_id",
                principalSchema: "assets",
                principalTable: "asset",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_asset_depreciation_schedule_asset_asset_id",
                schema: "assets",
                table: "asset_depreciation_schedule",
                column: "asset_id",
                principalSchema: "assets",
                principalTable: "asset",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_asset_disposal_asset_asset_id",
                schema: "assets",
                table: "asset_disposal",
                column: "asset_id",
                principalSchema: "assets",
                principalTable: "asset",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_asset_disposal_asset_status_to_status_id",
                schema: "assets",
                table: "asset_disposal",
                column: "to_status_id",
                principalSchema: "assets",
                principalTable: "asset_status",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_asset_lifecycle_event_asset_asset_id",
                schema: "assets",
                table: "asset_lifecycle_event",
                column: "asset_id",
                principalSchema: "assets",
                principalTable: "asset",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_asset_valuation_history_asset_asset_id",
                schema: "assets",
                table: "asset_valuation_history",
                column: "asset_id",
                principalSchema: "assets",
                principalTable: "asset",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // disposals recorded in one step: the recorder proposed and approved them; the final status is the asset's
            migrationBuilder.Sql("""
                UPDATE assets.asset_disposal d
                   SET proposed_by = d.approved_by, proposed_by_name = d.approved_by_name, proposed_at = d.approved_at,
                       to_status_id = a.status_id
                  FROM assets.asset a
                 WHERE a.id = d.asset_id AND d.proposed_at IS NULL;
                """);

            // the history kept so far moves into the trail, so an asset's History is one list (the old tables stay, read-only)
            migrationBuilder.Sql(CopyHistorySql);

            // append-only from here on: no UPDATE, DELETE or TRUNCATE of a line
            migrationBuilder.Sql(AuditTrailSql.Protect("assets"));
        }

        /// <summary>Copies the timeline, the field-change archive, deletions and posted / reversed months into the trail.</summary>
        private const string CopyHistorySql = """
            CREATE FUNCTION assets.tmp_audit_value(v jsonb, set_id uuid) RETURNS text LANGUAGE sql STABLE AS $f$
              SELECT CASE
                WHEN v IS NULL OR jsonb_typeof(v) = 'null' THEN NULL
                WHEN jsonb_typeof(v) = 'string' THEN COALESCE(
                  (SELECT o.label FROM assets.option_set_value o WHERE o.option_set_id = set_id AND o.value = v #>> '{}' LIMIT 1), v #>> '{}')
                WHEN jsonb_typeof(v) = 'boolean' THEN CASE WHEN v::text = 'true' THEN 'Yes' ELSE 'No' END
                WHEN jsonb_typeof(v) = 'array' THEN (SELECT string_agg(COALESCE(
                  (SELECT o.label FROM assets.option_set_value o WHERE o.option_set_id = set_id AND o.value = x LIMIT 1), x), ', ')
                  FROM jsonb_array_elements_text(v) x)
                ELSE v #>> '{}'
              END
            $f$;

            -- the asset timeline (events the acts wrote, and events recorded by hand)
            INSERT INTO assets.audit_entry (id, at, module, actor_id, actor_name, action, record_kind, record_id, record_code, record_label,
                                            parent_id, parent_code, field, old_value, new_value, detail, request_id)
            SELECT gen_random_uuid(), COALESCE(e.created_at, e.event_date), 'Assets', e.performed_by,
                   COALESCE(NULLIF(e.performed_by_name, ''), NULLIF(e.created_by, ''), 'System'),
                   CASE WHEN t.purpose IS NULL THEN 'Recorded' ELSE CASE t.purpose
                     WHEN 'Registered' THEN 'Added' WHEN 'Transfer' THEN 'Moved' WHEN 'Loan' THEN 'Issued on loan' WHEN 'Return' THEN 'Returned'
                     WHEN 'StatusChange' THEN 'Changed' WHEN 'Disposal' THEN 'Disposed' WHEN 'DisposalCancelled' THEN 'Cancelled'
                     WHEN 'Valuation' THEN 'Added' WHEN 'DepreciationRevised' THEN 'Revised' WHEN 'DocumentRemoved' THEN 'Removed'
                     WHEN 'DetailsChanged' THEN CASE WHEN e.notes = 'Purchase recorded' THEN 'Added' ELSE 'Changed' END
                     ELSE 'Recorded' END END,
                   k.kind,
                   CASE WHEN k.kind = 'Asset' THEN e.asset_id ELSE e.id END,
                   CASE WHEN k.kind = 'Asset' THEN a.asset_code END,
                   CASE WHEN k.kind = 'Asset' THEN a.name ELSE t.name END,
                   e.asset_id, a.asset_code || ' · ' || a.name,
                   CASE WHEN t.purpose = 'StatusChange' THEN 'Status' END,
                   CASE WHEN t.purpose = 'StatusChange' THEN fs.name END,
                   CASE WHEN t.purpose = 'StatusChange' THEN ts.name END,
                   NULLIF(concat_ws(' · ',
                     NULLIF(e.notes, ''),
                     CASE WHEN jsonb_typeof(e.details) = 'object'
                       THEN (SELECT string_agg(j.key || ': ' || j.value, ' · ') FROM jsonb_each_text(e.details) j) END,
                     CASE WHEN t.purpose IS DISTINCT FROM 'StatusChange' AND (fs.name IS NOT NULL OR ts.name IS NOT NULL)
                       THEN 'Status: ' || COALESCE(fs.name, '—') || ' → ' || COALESCE(ts.name, '—') END,
                     CASE WHEN e.created_at IS NOT NULL AND abs(extract(epoch FROM (e.event_date - e.created_at))) > 86400
                       THEN 'Dated ' || to_char(e.event_date AT TIME ZONE 'Asia/Karachi', 'FMDD Mon YYYY') END), ''),
                   'copied:asset_lifecycle_event'
              FROM assets.asset_lifecycle_event e
              JOIN assets.lifecycle_event_type t ON t.id = e.event_type_id
              JOIN assets.asset a ON a.id = e.asset_id
              LEFT JOIN assets.asset_status fs ON fs.id = e.from_status_id
              LEFT JOIN assets.asset_status ts ON ts.id = e.to_status_id
              CROSS JOIN LATERAL (SELECT CASE
                WHEN t.purpose IS NULL THEN 'Event'
                WHEN t.purpose IN ('Disposal', 'DisposalCancelled') THEN 'Disposal'
                WHEN t.purpose = 'Valuation' THEN 'Valuation'
                WHEN t.purpose IN ('Transfer', 'Loan', 'Return') THEN 'Move'
                WHEN t.purpose = 'DepreciationRevised' THEN 'Depreciation schedule'
                WHEN t.purpose = 'DocumentRemoved' THEN 'Document'
                WHEN t.purpose = 'DetailsChanged' AND e.notes IN ('Purchase recorded', 'Purchase details changed') THEN 'Purchase'
                ELSE 'Asset' END AS kind) k;

            -- the field-change archive: one line per field, values as a person reads them (choices by their labels)
            INSERT INTO assets.audit_entry (id, at, module, actor_id, actor_name, action, record_kind, record_id, record_code, record_label,
                                            parent_id, parent_code, field, old_value, new_value, detail, request_id)
            SELECT gen_random_uuid(), h.changed_at, 'Assets', h.changed_by, COALESCE(NULLIF(h.changed_by_name, ''), NULLIF(h.created_by, ''), 'System'),
                   'Changed', 'Asset', h.asset_id, a.asset_code, a.name, h.asset_id, a.asset_code || ' · ' || a.name,
                   COALESCE(d.name, h.attribute_code),
                   left(assets.tmp_audit_value(h.old_value, d.option_set_id), 4000), left(assets.tmp_audit_value(h.new_value, d.option_set_id), 4000),
                   NULLIF(NULLIF(h.change_reason, ''), 'Asset created'),
                   'copied:asset_attribute_history'
              FROM assets.asset_attribute_history h
              JOIN assets.asset a ON a.id = h.asset_id
              LEFT JOIN assets.attribute_definition d ON d.id = h.attribute_definition_id;

            -- assets taken out of the register (the row kept who and why, nobody could see it)
            INSERT INTO assets.audit_entry (id, at, module, actor_id, actor_name, action, record_kind, record_id, record_code, record_label,
                                            parent_id, parent_code, detail, request_id)
            SELECT gen_random_uuid(), a.deleted_at, 'Assets', NULL, COALESCE(NULLIF(a.deleted_by, ''), 'System'), 'Removed', 'Asset', a.id,
                   a.asset_code, a.name, a.id, a.asset_code || ' · ' || a.name,
                   CASE WHEN a.deletion_reason IS NOT NULL THEN 'Reason: ' || a.deletion_reason END, 'copied:asset'
              FROM assets.asset a
             WHERE a.deleted_at IS NOT NULL;

            -- posted months: one line per posting (a run or a disposal posts many at once)
            INSERT INTO assets.audit_entry (id, at, module, actor_id, actor_name, action, record_kind, record_id, record_code, record_label,
                                            parent_id, parent_code, detail, request_id)
            SELECT gen_random_uuid(), min(e.posted_at), 'Assets', e.posted_by, COALESCE(NULLIF(e.posted_by_name, ''), 'System'), 'Posted',
                   'Depreciation month', CASE WHEN count(*) = 1 THEN min(e.id::text)::uuid ELSE s.id END,
                   CASE WHEN count(*) = 1 THEN to_char(min(e.period_start), 'Mon YYYY') ELSE count(*) || ' months' END,
                   NULL, s.asset_id, a.asset_code || ' · ' || a.name,
                   to_char(min(e.period_start), 'FMDD Mon YYYY') || ' - ' || to_char(max(e.period_end) - 1, 'FMDD Mon YYYY')
                     || ' · Amount ' || to_char(sum(e.depreciation_amount), 'FM999,999,999,990.00'),
                   'copied:asset_depreciation_entry'
              FROM assets.asset_depreciation_entry e
              JOIN assets.asset_depreciation_schedule s ON s.id = e.schedule_id
              JOIN assets.asset a ON a.id = s.asset_id
             WHERE e.posted AND e.posted_at IS NOT NULL
             GROUP BY s.id, s.asset_id, a.asset_code, a.name, e.posted_by, e.posted_by_name, date_trunc('second', e.posted_at);

            -- reversed months, one by one
            INSERT INTO assets.audit_entry (id, at, module, actor_id, actor_name, action, record_kind, record_id, record_code, record_label,
                                            parent_id, parent_code, detail, request_id)
            SELECT gen_random_uuid(), e.reversed_at, 'Assets', e.reversed_by, COALESCE(NULLIF(e.reversed_by_name, ''), 'System'), 'Reversed',
                   'Depreciation month', e.id, to_char(e.period_start, 'Mon YYYY'), NULL, s.asset_id, a.asset_code || ' · ' || a.name,
                   to_char(e.period_start, 'FMDD Mon YYYY') || ' - ' || to_char(e.period_end - 1, 'FMDD Mon YYYY')
                     || ' · Amount ' || to_char(e.depreciation_amount, 'FM999,999,999,990.00')
                     || CASE WHEN e.posted_by_name IS NOT NULL THEN ' · Posted by ' || e.posted_by_name ELSE '' END,
                   'copied:asset_depreciation_entry'
              FROM assets.asset_depreciation_entry e
              JOIN assets.asset_depreciation_schedule s ON s.id = e.schedule_id
              JOIN assets.asset a ON a.id = s.asset_id
             WHERE e.reversed_at IS NOT NULL;

            DROP FUNCTION assets.tmp_audit_value(jsonb, uuid);
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AuditTrailSql.Unprotect("assets"));

            migrationBuilder.DropForeignKey(
                name: "fk_asset_acquisition_asset_asset_id",
                schema: "assets",
                table: "asset_acquisition");

            migrationBuilder.DropForeignKey(
                name: "fk_asset_assignment_asset_asset_id",
                schema: "assets",
                table: "asset_assignment");

            migrationBuilder.DropForeignKey(
                name: "fk_asset_attachment_asset_asset_id",
                schema: "assets",
                table: "asset_attachment");

            migrationBuilder.DropForeignKey(
                name: "fk_asset_attribute_history_asset_asset_id",
                schema: "assets",
                table: "asset_attribute_history");

            migrationBuilder.DropForeignKey(
                name: "fk_asset_depreciation_schedule_asset_asset_id",
                schema: "assets",
                table: "asset_depreciation_schedule");

            migrationBuilder.DropForeignKey(
                name: "fk_asset_disposal_asset_asset_id",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.DropForeignKey(
                name: "fk_asset_disposal_asset_status_to_status_id",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.DropForeignKey(
                name: "fk_asset_lifecycle_event_asset_asset_id",
                schema: "assets",
                table: "asset_lifecycle_event");

            migrationBuilder.DropForeignKey(
                name: "fk_asset_valuation_history_asset_asset_id",
                schema: "assets",
                table: "asset_valuation_history");

            migrationBuilder.DropTable(
                name: "audit_entry",
                schema: "assets");

            migrationBuilder.DropIndex(
                name: "ix_asset_disposal_asset_id_state",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.DropIndex(
                name: "ix_asset_disposal_to_status_id",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.DropIndex(
                name: "ux_asset_disposal_live",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.DropColumn(
                name: "cancelled_by",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.DropColumn(
                name: "cancelled_by_name",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.DropColumn(
                name: "decided_at",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.DropColumn(
                name: "decided_by",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.DropColumn(
                name: "decided_by_name",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.DropColumn(
                name: "decision_note",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.DropColumn(
                name: "proposed_at",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.DropColumn(
                name: "proposed_by",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.DropColumn(
                name: "proposed_by_name",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.DropColumn(
                name: "state",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.DropColumn(
                name: "to_status_id",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.DropColumn(
                name: "reversal_reason",
                schema: "assets",
                table: "asset_depreciation_entry");

            migrationBuilder.CreateIndex(
                name: "ix_asset_disposal_asset_id",
                schema: "assets",
                table: "asset_disposal",
                column: "asset_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_asset_acquisition_asset_asset_id",
                schema: "assets",
                table: "asset_acquisition",
                column: "asset_id",
                principalSchema: "assets",
                principalTable: "asset",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_asset_assignment_asset_asset_id",
                schema: "assets",
                table: "asset_assignment",
                column: "asset_id",
                principalSchema: "assets",
                principalTable: "asset",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_asset_attachment_asset_asset_id",
                schema: "assets",
                table: "asset_attachment",
                column: "asset_id",
                principalSchema: "assets",
                principalTable: "asset",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_asset_attribute_history_asset_asset_id",
                schema: "assets",
                table: "asset_attribute_history",
                column: "asset_id",
                principalSchema: "assets",
                principalTable: "asset",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_asset_attribute_history_attribute_definition_attribute_defi",
                schema: "assets",
                table: "asset_attribute_history",
                column: "attribute_definition_id",
                principalSchema: "assets",
                principalTable: "attribute_definition",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_asset_depreciation_schedule_asset_asset_id",
                schema: "assets",
                table: "asset_depreciation_schedule",
                column: "asset_id",
                principalSchema: "assets",
                principalTable: "asset",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_asset_disposal_asset_asset_id",
                schema: "assets",
                table: "asset_disposal",
                column: "asset_id",
                principalSchema: "assets",
                principalTable: "asset",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_asset_lifecycle_event_asset_asset_id",
                schema: "assets",
                table: "asset_lifecycle_event",
                column: "asset_id",
                principalSchema: "assets",
                principalTable: "asset",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_asset_valuation_history_asset_asset_id",
                schema: "assets",
                table: "asset_valuation_history",
                column: "asset_id",
                principalSchema: "assets",
                principalTable: "asset",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
