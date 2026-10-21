using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetsManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ActorsCustodyFinanceRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "approved_at",
                schema: "assets",
                table: "asset_assignment");

            migrationBuilder.DropColumn(
                name: "supplier_id",
                schema: "assets",
                table: "asset_acquisition");

            migrationBuilder.RenameColumn(
                name: "valued_by",
                schema: "assets",
                table: "asset_valuation_history",
                newName: "recorded_by");

            migrationBuilder.RenameColumn(
                name: "approved_by",
                schema: "assets",
                table: "asset_assignment",
                newName: "recorded_by");

            migrationBuilder.CreateSequence(
                name: "asset_tag_seq",
                schema: "assets");

            migrationBuilder.AddColumn<bool>(
                name: "is_system",
                schema: "assets",
                table: "lifecycle_event_type",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "purpose",
                schema: "assets",
                table: "lifecycle_event_type",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "allows_value",
                schema: "assets",
                table: "disposal_method",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_system",
                schema: "assets",
                table: "depreciation_method",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                schema: "assets",
                table: "depreciation_method",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_hidden",
                schema: "assets",
                table: "attribute_assignment",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "recorded_by_name",
                schema: "assets",
                table: "asset_valuation_history",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "valued_by_name",
                schema: "assets",
                table: "asset_valuation_history",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "performed_by_name",
                schema: "assets",
                table: "asset_lifecycle_event",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "approved_by_name",
                schema: "assets",
                table: "asset_disposal",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "opening_book_value",
                schema: "assets",
                table: "asset_depreciation_schedule",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "posted_by_name",
                schema: "assets",
                table: "asset_depreciation_entry",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reversed_by_name",
                schema: "assets",
                table: "asset_depreciation_entry",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "changed_by_name",
                schema: "assets",
                table: "asset_attribute_history",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "from_custodian_name",
                schema: "assets",
                table: "asset_assignment",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "from_custodian_number",
                schema: "assets",
                table: "asset_assignment",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "from_department_name",
                schema: "assets",
                table: "asset_assignment",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recorded_by_name",
                schema: "assets",
                table: "asset_assignment",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "to_custodian_name",
                schema: "assets",
                table: "asset_assignment",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "to_custodian_number",
                schema: "assets",
                table: "asset_assignment",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "to_department_name",
                schema: "assets",
                table: "asset_assignment",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "supplier_name",
                schema: "assets",
                table: "asset_acquisition",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "custodian_name",
                schema: "assets",
                table: "asset",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "custodian_number",
                schema: "assets",
                table: "asset",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deletion_reason",
                schema: "assets",
                table: "asset",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "department_name",
                schema: "assets",
                table: "asset",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_lifecycle_event_type_purpose",
                schema: "assets",
                table: "lifecycle_event_type",
                column: "purpose",
                unique: true,
                filter: "purpose IS NOT NULL");

            // --- data -----------------------------------------------------------------------------------------

            // tags: the sequence continues after the highest AST-###### tag already issued
            migrationBuilder.Sql(@"
SELECT setval('assets.asset_tag_seq',
  GREATEST(1, COALESCE((SELECT MAX(CAST(SUBSTRING(asset_code FROM 5) AS bigint)) FROM assets.asset WHERE asset_code ~ '^AST-[0-9]+$'), 0)),
  COALESCE((SELECT MAX(CAST(SUBSTRING(asset_code FROM 5) AS bigint)) FROM assets.asset WHERE asset_code ~ '^AST-[0-9]+$'), 0) > 0);");

            // a schedule starts from the value it spreads plus what is left at the end
            migrationBuilder.Sql("UPDATE assets.asset_depreciation_schedule SET opening_book_value = depreciable_base + salvage_value;");

            // class and type fields always reached every node below; the switch is now honoured for every level
            migrationBuilder.Sql("UPDATE assets.attribute_assignment SET inherit_to_children = TRUE WHERE scope IN ('AssetClass', 'AssetType');");

            // depreciation methods: what each one calculates; the three the engine knows become system rows
            migrationBuilder.Sql(@"
UPDATE assets.depreciation_method SET kind = 'StraightLine', is_system = TRUE WHERE code = 'STRAIGHT_LINE';
UPDATE assets.depreciation_method SET kind = 'DecliningBalance', is_system = TRUE WHERE code = 'DECLINING_BALANCE';
UPDATE assets.depreciation_method SET kind = 'UnitsOfProduction', is_system = TRUE WHERE code = 'UNITS_OF_PRODUCTION';
UPDATE assets.depreciation_method SET kind = 'StraightLine' WHERE kind = '';
INSERT INTO assets.depreciation_method (id, code, name, description, kind, is_system, is_active, created_at, created_by)
SELECT gen_random_uuid(), v.code, v.name, v.description, v.kind, TRUE, TRUE, now(), 'system'
FROM (VALUES
  ('STRAIGHT_LINE', 'Straight line', 'The same amount every month over the useful life.', 'StraightLine'),
  ('DECLINING_BALANCE', 'Declining balance', 'A fixed yearly share of the value left; switches to straight line once that gives more.', 'DecliningBalance'),
  ('UNITS_OF_PRODUCTION', 'Units of production', 'By use: the months are added by hand.', 'UnitsOfProduction')) AS v(code, name, description, kind)
WHERE NOT EXISTS (SELECT 1 FROM assets.depreciation_method m WHERE m.kind = v.kind AND m.is_system)
  AND NOT EXISTS (SELECT 1 FROM assets.depreciation_method m WHERE m.code = v.code);");

            // timeline types: the ones the acts write get their purpose and become system rows; the missing ones are added
            migrationBuilder.Sql(@"
UPDATE assets.lifecycle_event_type SET purpose = 'Registered', is_system = TRUE, name = 'Registered', stage = 'ACQUISITION' WHERE code = 'PURCHASED';
UPDATE assets.lifecycle_event_type SET purpose = 'Transfer', is_system = TRUE, stage = 'ASSIGNMENT' WHERE code = 'TRANSFERRED';
UPDATE assets.lifecycle_event_type SET purpose = 'Loan', is_system = TRUE, stage = 'ASSIGNMENT' WHERE code = 'ISSUED';
UPDATE assets.lifecycle_event_type SET purpose = 'Return', is_system = TRUE, stage = 'ASSIGNMENT' WHERE code = 'RETURNED';
UPDATE assets.lifecycle_event_type SET purpose = 'StatusChange', is_system = TRUE, stage = 'RECORD' WHERE code = 'STATUS_CHANGED';
UPDATE assets.lifecycle_event_type SET purpose = 'Disposal', is_system = TRUE, stage = 'DISPOSAL' WHERE code = 'DISPOSED';
INSERT INTO assets.lifecycle_event_type (id, stage, code, name, description, purpose, is_system, is_active, created_at, created_by)
SELECT gen_random_uuid(), v.stage, v.code, v.name, v.description, v.purpose, TRUE, TRUE, now(), 'system'
FROM (VALUES
  ('ACQUISITION', 'REGISTERED', 'Registered', 'The asset was entered in the register.', 'Registered'),
  ('ASSIGNMENT', 'TRANSFERRED', 'Transferred', 'Moved to another place, person or unit.', 'Transfer'),
  ('ASSIGNMENT', 'ISSUED', 'Issued on loan', 'Issued for a time, to come back by a date.', 'Loan'),
  ('ASSIGNMENT', 'RETURNED', 'Returned', 'Came back from a loan.', 'Return'),
  ('RECORD', 'STATUS_CHANGED', 'Status changed', 'Moved to another status.', 'StatusChange'),
  ('DISPOSAL', 'DISPOSED', 'Disposed', 'Left GDA: sold, scrapped or written off.', 'Disposal'),
  ('DISPOSAL', 'DISPOSAL_CANCELLED', 'Disposal cancelled', 'A disposal recorded by mistake was taken off.', 'DisposalCancelled'),
  ('VALUATION', 'VALUED', 'Valued', 'A valuation was recorded.', 'Valuation'),
  ('RECORD', 'DETAILS_CHANGED', 'Details changed', 'The record''s details were changed.', 'DetailsChanged'),
  ('DEPRECIATION', 'DEPRECIATION_REVISED', 'Useful life revised', 'The depreciation was revised from the book value left.', 'DepreciationRevised'),
  ('RECORD', 'DOCUMENT_REMOVED', 'Document removed', 'A document was taken off the record.', 'DocumentRemoved')) AS v(stage, code, name, description, purpose)
WHERE NOT EXISTS (SELECT 1 FROM assets.lifecycle_event_type t WHERE t.purpose = v.purpose)
  AND NOT EXISTS (SELECT 1 FROM assets.lifecycle_event_type t WHERE t.code = v.code);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_lifecycle_event_type_purpose",
                schema: "assets",
                table: "lifecycle_event_type");

            migrationBuilder.DropColumn(
                name: "is_system",
                schema: "assets",
                table: "lifecycle_event_type");

            migrationBuilder.DropColumn(
                name: "purpose",
                schema: "assets",
                table: "lifecycle_event_type");

            migrationBuilder.DropColumn(
                name: "allows_value",
                schema: "assets",
                table: "disposal_method");

            migrationBuilder.DropColumn(
                name: "is_system",
                schema: "assets",
                table: "depreciation_method");

            migrationBuilder.DropColumn(
                name: "kind",
                schema: "assets",
                table: "depreciation_method");

            migrationBuilder.DropColumn(
                name: "is_hidden",
                schema: "assets",
                table: "attribute_assignment");

            migrationBuilder.DropColumn(
                name: "recorded_by_name",
                schema: "assets",
                table: "asset_valuation_history");

            migrationBuilder.DropColumn(
                name: "valued_by_name",
                schema: "assets",
                table: "asset_valuation_history");

            migrationBuilder.DropColumn(
                name: "performed_by_name",
                schema: "assets",
                table: "asset_lifecycle_event");

            migrationBuilder.DropColumn(
                name: "approved_by_name",
                schema: "assets",
                table: "asset_disposal");

            migrationBuilder.DropColumn(
                name: "opening_book_value",
                schema: "assets",
                table: "asset_depreciation_schedule");

            migrationBuilder.DropColumn(
                name: "posted_by_name",
                schema: "assets",
                table: "asset_depreciation_entry");

            migrationBuilder.DropColumn(
                name: "reversed_by_name",
                schema: "assets",
                table: "asset_depreciation_entry");

            migrationBuilder.DropColumn(
                name: "changed_by_name",
                schema: "assets",
                table: "asset_attribute_history");

            migrationBuilder.DropColumn(
                name: "from_custodian_name",
                schema: "assets",
                table: "asset_assignment");

            migrationBuilder.DropColumn(
                name: "from_custodian_number",
                schema: "assets",
                table: "asset_assignment");

            migrationBuilder.DropColumn(
                name: "from_department_name",
                schema: "assets",
                table: "asset_assignment");

            migrationBuilder.DropColumn(
                name: "recorded_by_name",
                schema: "assets",
                table: "asset_assignment");

            migrationBuilder.DropColumn(
                name: "to_custodian_name",
                schema: "assets",
                table: "asset_assignment");

            migrationBuilder.DropColumn(
                name: "to_custodian_number",
                schema: "assets",
                table: "asset_assignment");

            migrationBuilder.DropColumn(
                name: "to_department_name",
                schema: "assets",
                table: "asset_assignment");

            migrationBuilder.DropColumn(
                name: "supplier_name",
                schema: "assets",
                table: "asset_acquisition");

            migrationBuilder.DropColumn(
                name: "custodian_name",
                schema: "assets",
                table: "asset");

            migrationBuilder.DropColumn(
                name: "custodian_number",
                schema: "assets",
                table: "asset");

            migrationBuilder.DropColumn(
                name: "deletion_reason",
                schema: "assets",
                table: "asset");

            migrationBuilder.DropColumn(
                name: "department_name",
                schema: "assets",
                table: "asset");

            migrationBuilder.DropSequence(
                name: "asset_tag_seq",
                schema: "assets");

            migrationBuilder.RenameColumn(
                name: "recorded_by",
                schema: "assets",
                table: "asset_valuation_history",
                newName: "valued_by");

            migrationBuilder.RenameColumn(
                name: "recorded_by",
                schema: "assets",
                table: "asset_assignment",
                newName: "approved_by");

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                schema: "assets",
                table: "asset_assignment",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "supplier_id",
                schema: "assets",
                table: "asset_acquisition",
                type: "uuid",
                nullable: true);
        }
    }
}
