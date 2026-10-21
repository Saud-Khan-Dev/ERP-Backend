using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PropertyAuditFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_agreement_violation_one_agreement",
                schema: "property",
                table: "agreement_violation");

            migrationBuilder.AddColumn<Guid>(
                name: "approved_by_employee_id",
                schema: "property",
                table: "property_transfer",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "approved_by_user_id",
                schema: "property",
                table: "property_transfer",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "no_dues_certificate_no",
                schema: "property",
                table: "property_transfer",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "property",
                table: "property_transfer",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<string>(
                name: "end_reference_no",
                schema: "property",
                table: "property_ownership",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "money_direction",
                schema: "property",
                table: "property_outsourcing",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "renewed_from_contract_id",
                schema: "property",
                table: "property_outsourcing",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "decision_result",
                schema: "property",
                table: "property_litigation",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "gda_counsel_employee_id",
                schema: "property",
                table: "property_litigation",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "approved_by_employee_id",
                schema: "property",
                table: "property_area_regularization",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "delegated_officer_employee_id",
                schema: "property",
                table: "property_appeal",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "property",
                table: "property_allotment",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<DateOnly>(
                name: "deactivated_on",
                schema: "property",
                table: "property",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deactivation_reason",
                schema: "property",
                table: "property",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "property",
                table: "property",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<Guid>(
                name: "attended_by_employee_id",
                schema: "property",
                table: "litigation_hearing",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "approved_by_employee_id",
                schema: "property",
                table: "building_plan",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "covered_area_base",
                schema: "property",
                table: "building_plan",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "no_dues_certificate_no",
                schema: "property",
                table: "building_plan",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "allotment_id",
                schema: "property",
                table: "agreement_violation",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fine_imposed_on",
                schema: "property",
                table: "agreement_violation",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fine_paid_on",
                schema: "property",
                table: "agreement_violation",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fine_receipt_no",
                schema: "property",
                table: "agreement_violation",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fine_status_changed_by",
                schema: "property",
                table: "agreement_violation",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fine_status_changed_on",
                schema: "property",
                table: "agreement_violation",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fine_status_reason",
                schema: "property",
                table: "agreement_violation",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_property_outsourcing_renewed_from_contract_id",
                schema: "property",
                table: "property_outsourcing",
                column: "renewed_from_contract_id");

            migrationBuilder.CreateIndex(
                name: "ix_agreement_violation_allotment_id",
                schema: "property",
                table: "agreement_violation",
                column: "allotment_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_agreement_violation_one_agreement",
                schema: "property",
                table: "agreement_violation",
                sql: "num_nonnulls(lease_id, rental_id, transfer_id, allotment_id) = 1");

            migrationBuilder.AddForeignKey(
                name: "fk_agreement_violation_property_allotment_allotment_id",
                schema: "property",
                table: "agreement_violation",
                column: "allotment_id",
                principalSchema: "property",
                principalTable: "property_allotment",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_property_outsourcing_property_outsourcing_renewed_from_cont",
                schema: "property",
                table: "property_outsourcing",
                column: "renewed_from_contract_id",
                principalSchema: "property",
                principalTable: "property_outsourcing",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_agreement_violation_property_allotment_allotment_id",
                schema: "property",
                table: "agreement_violation");

            migrationBuilder.DropForeignKey(
                name: "fk_property_outsourcing_property_outsourcing_renewed_from_cont",
                schema: "property",
                table: "property_outsourcing");

            migrationBuilder.DropIndex(
                name: "ix_property_outsourcing_renewed_from_contract_id",
                schema: "property",
                table: "property_outsourcing");

            migrationBuilder.DropIndex(
                name: "ix_agreement_violation_allotment_id",
                schema: "property",
                table: "agreement_violation");

            migrationBuilder.DropCheckConstraint(
                name: "ck_agreement_violation_one_agreement",
                schema: "property",
                table: "agreement_violation");

            migrationBuilder.DropColumn(
                name: "approved_by_employee_id",
                schema: "property",
                table: "property_transfer");

            migrationBuilder.DropColumn(
                name: "approved_by_user_id",
                schema: "property",
                table: "property_transfer");

            migrationBuilder.DropColumn(
                name: "no_dues_certificate_no",
                schema: "property",
                table: "property_transfer");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "property",
                table: "property_transfer");

            migrationBuilder.DropColumn(
                name: "end_reference_no",
                schema: "property",
                table: "property_ownership");

            migrationBuilder.DropColumn(
                name: "money_direction",
                schema: "property",
                table: "property_outsourcing");

            migrationBuilder.DropColumn(
                name: "renewed_from_contract_id",
                schema: "property",
                table: "property_outsourcing");

            migrationBuilder.DropColumn(
                name: "decision_result",
                schema: "property",
                table: "property_litigation");

            migrationBuilder.DropColumn(
                name: "gda_counsel_employee_id",
                schema: "property",
                table: "property_litigation");

            migrationBuilder.DropColumn(
                name: "approved_by_employee_id",
                schema: "property",
                table: "property_area_regularization");

            migrationBuilder.DropColumn(
                name: "delegated_officer_employee_id",
                schema: "property",
                table: "property_appeal");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "property",
                table: "property_allotment");

            migrationBuilder.DropColumn(
                name: "deactivated_on",
                schema: "property",
                table: "property");

            migrationBuilder.DropColumn(
                name: "deactivation_reason",
                schema: "property",
                table: "property");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "property",
                table: "property");

            migrationBuilder.DropColumn(
                name: "attended_by_employee_id",
                schema: "property",
                table: "litigation_hearing");

            migrationBuilder.DropColumn(
                name: "approved_by_employee_id",
                schema: "property",
                table: "building_plan");

            migrationBuilder.DropColumn(
                name: "covered_area_base",
                schema: "property",
                table: "building_plan");

            migrationBuilder.DropColumn(
                name: "no_dues_certificate_no",
                schema: "property",
                table: "building_plan");

            migrationBuilder.DropColumn(
                name: "allotment_id",
                schema: "property",
                table: "agreement_violation");

            migrationBuilder.DropColumn(
                name: "fine_imposed_on",
                schema: "property",
                table: "agreement_violation");

            migrationBuilder.DropColumn(
                name: "fine_paid_on",
                schema: "property",
                table: "agreement_violation");

            migrationBuilder.DropColumn(
                name: "fine_receipt_no",
                schema: "property",
                table: "agreement_violation");

            migrationBuilder.DropColumn(
                name: "fine_status_changed_by",
                schema: "property",
                table: "agreement_violation");

            migrationBuilder.DropColumn(
                name: "fine_status_changed_on",
                schema: "property",
                table: "agreement_violation");

            migrationBuilder.DropColumn(
                name: "fine_status_reason",
                schema: "property",
                table: "agreement_violation");

            migrationBuilder.AddCheckConstraint(
                name: "ck_agreement_violation_one_agreement",
                schema: "property",
                table: "agreement_violation",
                sql: "num_nonnulls(lease_id, rental_id, transfer_id) = 1");
        }
    }
}
