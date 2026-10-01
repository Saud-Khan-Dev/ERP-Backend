using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetsManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class StrictTaxonomyRealUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_asset_asset_parent_asset_id",
                schema: "assets",
                table: "asset");

            migrationBuilder.DropIndex(
                name: "ix_asset_category_id_serial_number",
                schema: "assets",
                table: "asset");

            migrationBuilder.DropIndex(
                name: "ix_asset_parent_asset_id",
                schema: "assets",
                table: "asset");

            migrationBuilder.DropIndex(
                name: "ix_asset_rfid_tag",
                schema: "assets",
                table: "asset");

            migrationBuilder.DropColumn(
                name: "parent_asset_id",
                schema: "assets",
                table: "asset");

            migrationBuilder.DropColumn(
                name: "rfid_tag",
                schema: "assets",
                table: "asset");

            migrationBuilder.DropColumn(
                name: "serial_number",
                schema: "assets",
                table: "asset");

            // ---- every category belongs to exactly one asset type (strict Class > Type > Category tree) ----
            // 1. a class with untyped categories but no type at all gets a General type to hold them
            migrationBuilder.Sql(@"
INSERT INTO assets.asset_type (id, asset_class_id, code, name, is_depreciable, requires_location, requires_custodian, is_active)
SELECT gen_random_uuid(), c.id, 'GENERAL', 'General', true, false, false, true
FROM assets.asset_class c
WHERE EXISTS (SELECT 1 FROM assets.asset_category k WHERE k.asset_class_id = c.id AND k.asset_type_id IS NULL)
  AND NOT EXISTS (SELECT 1 FROM assets.asset_type t WHERE t.asset_class_id = c.id);");

            // 2. an untyped top-level category takes the type most used below it (pinned sub-categories first,
            //    then its assets), else the first type of its class
            migrationBuilder.Sql(@"
UPDATE assets.asset_category c SET asset_type_id = COALESCE(
  (SELECT d.asset_type_id FROM assets.asset_category d
    WHERE d.path <@ c.path AND d.asset_type_id IS NOT NULL
    GROUP BY d.asset_type_id ORDER BY count(*) DESC LIMIT 1),
  (SELECT a.asset_type_id FROM assets.asset a JOIN assets.asset_category d ON d.id = a.category_id
    WHERE d.path <@ c.path
    GROUP BY a.asset_type_id ORDER BY count(*) DESC LIMIT 1),
  (SELECT t.id FROM assets.asset_type t WHERE t.asset_class_id = c.asset_class_id
    ORDER BY t.display_order NULLS LAST, t.code LIMIT 1))
WHERE c.parent_category_id IS NULL AND c.asset_type_id IS NULL;");

            // 3. a sub-category always belongs to its parent's type, level by level from the top
            migrationBuilder.Sql(@"
DO $$
DECLARE level int;
BEGIN
  FOR level IN 1..(SELECT COALESCE(max(depth), 0) FROM assets.asset_category) LOOP
    UPDATE assets.asset_category c SET asset_type_id = p.asset_type_id
    FROM assets.asset_category p
    WHERE c.parent_category_id = p.id AND c.depth = level AND c.asset_type_id IS DISTINCT FROM p.asset_type_id;
  END LOOP;
END $$;");

            // 4. an asset takes its category's type (the type of the branch it sits in)
            migrationBuilder.Sql(@"
UPDATE assets.asset a SET asset_type_id = c.asset_type_id
FROM assets.asset_category c
WHERE a.category_id = c.id AND a.asset_type_id <> c.asset_type_id;");

            migrationBuilder.AlterColumn<Guid>(
                name: "asset_type_id",
                schema: "assets",
                table: "asset_category",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title",
                schema: "assets",
                table: "asset_attachment",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "title",
                schema: "assets",
                table: "asset_attachment");

            migrationBuilder.AlterColumn<Guid>(
                name: "asset_type_id",
                schema: "assets",
                table: "asset_category",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "parent_asset_id",
                schema: "assets",
                table: "asset",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rfid_tag",
                schema: "assets",
                table: "asset",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "serial_number",
                schema: "assets",
                table: "asset",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_category_id_serial_number",
                schema: "assets",
                table: "asset",
                columns: new[] { "category_id", "serial_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_parent_asset_id",
                schema: "assets",
                table: "asset",
                column: "parent_asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_rfid_tag",
                schema: "assets",
                table: "asset",
                column: "rfid_tag",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_asset_asset_parent_asset_id",
                schema: "assets",
                table: "asset",
                column: "parent_asset_id",
                principalSchema: "assets",
                principalTable: "asset",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
