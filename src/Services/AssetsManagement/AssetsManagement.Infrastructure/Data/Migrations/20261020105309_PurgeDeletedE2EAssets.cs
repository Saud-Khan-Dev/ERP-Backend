using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetsManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PurgeDeletedE2EAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Four assets the end-to-end suites registered and then deleted (AST-000001, -000002, -000004, -000028,
            // all "E2E Meter … (lab)"). A deleted asset can no longer be removed through the API, and these kept their
            // test categories and types from being cleaned up. Each row is matched by its id AND its test name AND its
            // deleted mark, so on any other database this does nothing.
            migrationBuilder.Sql(@"
CREATE TEMP TABLE e2e_purge ON COMMIT DROP AS
SELECT id FROM assets.asset
WHERE deleted_at IS NOT NULL
  AND name LIKE 'E2E Meter % (lab)'
  AND (id, asset_code) IN (
    ('f95151e4-e6fa-4188-bf02-a6d575e8c256'::uuid, 'AST-000001'),
    ('0dab3b24-54c1-4191-a05e-ac8140a46752'::uuid, 'AST-000002'),
    ('35680096-6377-410b-b0a4-585c4eda414e'::uuid, 'AST-000004'),
    ('b31d73f0-9644-48c2-bccd-af2dfe852df8'::uuid, 'AST-000028'));

DELETE FROM assets.asset_depreciation_entry WHERE schedule_id IN (SELECT id FROM assets.asset_depreciation_schedule WHERE asset_id IN (SELECT id FROM e2e_purge));
DELETE FROM assets.asset_depreciation_schedule WHERE asset_id IN (SELECT id FROM e2e_purge);
DELETE FROM assets.asset_disposal WHERE asset_id IN (SELECT id FROM e2e_purge);
DELETE FROM assets.asset_valuation_history WHERE asset_id IN (SELECT id FROM e2e_purge);
DELETE FROM assets.asset_lifecycle_event WHERE asset_id IN (SELECT id FROM e2e_purge);
DELETE FROM assets.asset_attribute_history WHERE asset_id IN (SELECT id FROM e2e_purge);
DELETE FROM assets.asset_attribute_value WHERE asset_id IN (SELECT id FROM e2e_purge);
DELETE FROM assets.asset_attachment WHERE asset_id IN (SELECT id FROM e2e_purge);
DELETE FROM assets.asset_assignment WHERE asset_id IN (SELECT id FROM e2e_purge);
DELETE FROM assets.asset_acquisition WHERE asset_id IN (SELECT id FROM e2e_purge);
DELETE FROM assets.asset WHERE id IN (SELECT id FROM e2e_purge);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
