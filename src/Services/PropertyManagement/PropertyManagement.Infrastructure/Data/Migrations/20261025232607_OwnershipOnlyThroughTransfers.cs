using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class OwnershipOnlyThroughTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // An owner holds at most one current (ACTIVE or DISPUTED) share of a property: a change of share is a transfer,
            // which closes the old row before it opens the new one. Stop with directions if the data breaks that already.
            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM property.property_ownership
    WHERE ownership_status IN ('ACTIVE', 'DISPUTED')
    GROUP BY property_id, owner_id
    HAVING count(*) > 1) THEN
    RAISE EXCEPTION 'OwnershipOnlyThroughTransfers: an owner holds more than one current share of the same property. List them with: SELECT property_id, owner_id, count(*) FROM property.property_ownership WHERE ownership_status IN (''ACTIVE'', ''DISPUTED'') GROUP BY 1, 2 HAVING count(*) > 1; then keep one row per owner and property (its share the sum, the other rows ENDED) and start the service again.';
  END IF;
END $$;");

            migrationBuilder.CreateIndex(
                name: "ux_property_ownership_current_holding",
                schema: "property",
                table: "property_ownership",
                columns: new[] { "property_id", "owner_id" },
                unique: true,
                filter: "ownership_status IN ('ACTIVE', 'DISPUTED')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_property_ownership_current_holding",
                schema: "property",
                table: "property_ownership");
        }
    }
}
