using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class OwnerIdentityAndFirstOwners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "date_of_death",
                schema: "property",
                table: "property_owner",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "old_id_no",
                schema: "property",
                table: "property_owner",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "old_id_type",
                schema: "property",
                table: "property_owner",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // No "Other" catch-all in any list (the user's rule): the OTHER values an earlier seed added are switched off.
            // Records that use one keep its label; the value is only no longer offered.
            migrationBuilder.Sql(@"
DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY['property_type', 'property_status', 'property_classification', 'transfer_type', 'owner_type',
    'contact_type', 'tenure_type', 'document_type', 'encumbrance_type', 'building_plan_type', 'auction_type',
    'litigation_status', 'litigation_type', 'agreement_type'] LOOP
    IF to_regclass('property.' || t) IS NOT NULL THEN
      EXECUTE format('UPDATE property.%I SET is_active = false WHERE code = ''OTHER'' AND is_active', t);
    END IF;
  END LOOP;
END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "date_of_death",
                schema: "property",
                table: "property_owner");

            migrationBuilder.DropColumn(
                name: "old_id_no",
                schema: "property",
                table: "property_owner");

            migrationBuilder.DropColumn(
                name: "old_id_type",
                schema: "property",
                table: "property_owner");
        }
    }
}
