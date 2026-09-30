using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignWithErd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // property_document.property_id becomes NOT NULL (ERD: "always set, even when attached to a sub-record").
            // Stop with a clear message if an owner's paper was filed without a property under the previous rule.
            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM property.property_document WHERE property_id IS NULL) THEN
    RAISE EXCEPTION 'property_document has rows without property_id (owner papers filed without a property). Set their property_id to the property whose file they belong to, then apply this migration again.';
  END IF;
END $$;");

            migrationBuilder.DropCheckConstraint(
                name: "ck_property_document_property",
                schema: "property",
                table: "property_document");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "transfer_type");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "transfer_type");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "transfer_type");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "transfer_type");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "town");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "town");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "tenure_type");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "tenure_type");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "tenure_type");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "tenure_type");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "rental_type");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "rental_type");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "rental_type");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "rental_type");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "rental_status");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "rental_status");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "rental_status");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "rental_status");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "property_type");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "property_type");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "property_type");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "property_type");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "property_transfer_party");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "property_transfer_party");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "property_transfer_party");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "property_transfer_party");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "property_status_history");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "property_status_history");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "property_status");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "property_status");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "property_status");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "property_status");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "property_document");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "property_document");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "property_document");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "property_document");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "property_classification");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "property_classification");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "property_classification");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "property_classification");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "owner_type");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "owner_type");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "owner_type");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "owner_type");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "owner_contact");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "owner_contact");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "owner_contact");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "owner_address");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "owner_address");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "owner_address");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "outsourcing_type");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "outsourcing_type");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "outsourcing_type");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "outsourcing_type");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "measurement_unit");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "measurement_unit");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "measurement_unit");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "measurement_unit");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "litigation_type");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "litigation_type");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "litigation_type");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "litigation_type");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "litigation_status");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "litigation_status");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "litigation_status");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "litigation_status");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "lease_type");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "lease_type");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "lease_type");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "lease_type");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "lease_status");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "lease_status");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "lease_status");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "lease_status");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "encumbrance_type");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "encumbrance_type");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "encumbrance_type");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "encumbrance_type");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "encroachment_status");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "encroachment_status");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "encroachment_status");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "encroachment_status");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "document_type");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "document_type");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "document_type");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "document_type");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "contract_status");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "contract_status");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "contract_status");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "contract_status");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "contact_type");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "contact_type");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "contact_type");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "contact_type");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "building_plan_type");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "building_plan_type");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "building_plan_type");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "building_plan_type");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "building_plan_status");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "building_plan_status");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "building_plan_status");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "building_plan_status");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "auction_type");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "auction_type");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "auction_type");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "auction_type");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "auction_status");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "auction_status");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "auction_status");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "auction_status");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "attribute_group");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "attribute_group");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "attribute_group");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "attribute_group");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "allotment_type");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "allotment_type");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "allotment_type");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "allotment_type");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "allotment_status");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "allotment_status");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "allotment_status");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "allotment_status");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "property",
                table: "agreement_type");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "property",
                table: "agreement_type");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "property",
                table: "agreement_type");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "property",
                table: "agreement_type");

            migrationBuilder.AlterColumn<Guid>(
                name: "property_id",
                schema: "property",
                table: "property_document",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "transfer_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "transfer_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "transfer_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "transfer_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "town",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "town",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "tenure_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "tenure_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "tenure_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "tenure_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "rental_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "rental_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "rental_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "rental_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "rental_status",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "rental_status",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "rental_status",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "rental_status",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "property_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "property_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "property_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "property_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "property_transfer_party",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "property_transfer_party",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "property_transfer_party",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "property_transfer_party",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "property_status_history",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "property_status_history",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "property_status",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "property_status",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "property_status",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "property_status",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "property_id",
                schema: "property",
                table: "property_document",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "property_document",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "property_document",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "property_document",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "property_document",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "property_classification",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "property_classification",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "property_classification",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "property_classification",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "owner_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "owner_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "owner_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "owner_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "owner_contact",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "owner_contact",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "owner_contact",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "owner_address",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "owner_address",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "owner_address",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "outsourcing_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "outsourcing_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "outsourcing_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "outsourcing_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "measurement_unit",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "measurement_unit",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "measurement_unit",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "measurement_unit",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "litigation_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "litigation_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "litigation_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "litigation_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "litigation_status",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "litigation_status",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "litigation_status",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "litigation_status",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "lease_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "lease_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "lease_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "lease_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "lease_status",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "lease_status",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "lease_status",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "lease_status",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "encumbrance_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "encumbrance_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "encumbrance_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "encumbrance_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "encroachment_status",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "encroachment_status",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "encroachment_status",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "encroachment_status",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "document_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "document_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "document_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "document_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "contract_status",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "contract_status",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "contract_status",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "contract_status",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "contact_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "contact_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "contact_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "contact_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "building_plan_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "building_plan_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "building_plan_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "building_plan_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "building_plan_status",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "building_plan_status",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "building_plan_status",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "building_plan_status",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "auction_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "auction_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "auction_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "auction_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "auction_status",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "auction_status",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "auction_status",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "auction_status",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "attribute_group",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "attribute_group",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "attribute_group",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "attribute_group",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "allotment_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "allotment_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "allotment_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "allotment_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "allotment_status",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "allotment_status",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "allotment_status",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "allotment_status",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "property",
                table: "agreement_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "property",
                table: "agreement_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "property",
                table: "agreement_type",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "property",
                table: "agreement_type",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_property_document_property",
                schema: "property",
                table: "property_document",
                sql: "entity_type = 'OWNER' OR property_id IS NOT NULL");
        }
    }
}
