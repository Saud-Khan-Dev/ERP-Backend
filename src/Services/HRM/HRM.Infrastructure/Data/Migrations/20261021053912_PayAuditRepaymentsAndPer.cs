using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PayAuditRepaymentsAndPer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {


            migrationBuilder.AddColumn<DateTime>(
                name: "acknowledged_at",
                schema: "hrms",
                table: "performance_review",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "countersign_remarks",
                schema: "hrms",
                table: "performance_review",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "countersigned_at",
                schema: "hrms",
                table: "performance_review",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "countersigning_officer_id",
                schema: "hrms",
                table: "performance_review",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "final_score_reason",
                schema: "hrms",
                table: "performance_review",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "finalized_at",
                schema: "hrms",
                table: "performance_review",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "finalized_by",
                schema: "hrms",
                table: "performance_review",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "submitted_at",
                schema: "hrms",
                table: "performance_review",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "calculated_at",
                schema: "hrms",
                table: "payroll_run",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "finalized_by",
                schema: "hrms",
                table: "payroll_run",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "paid_by",
                schema: "hrms",
                table: "payroll_run",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reversal_reason",
                schema: "hrms",
                table: "payroll_run",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reversed_at",
                schema: "hrms",
                table: "payroll_run",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reversed_by",
                schema: "hrms",
                table: "payroll_run",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "bank_account_id",
                schema: "hrms",
                table: "payroll_payment",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "waive_reason",
                schema: "hrms",
                table: "loan_installment_schedule",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "waived_at",
                schema: "hrms",
                table: "loan_installment_schedule",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "waived_by",
                schema: "hrms",
                table: "loan_installment_schedule",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "closed_at",
                schema: "hrms",
                table: "employee_loan",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "closed_by",
                schema: "hrms",
                table: "employee_loan",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "closure_reason",
                schema: "hrms",
                table: "employee_loan",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "employee_tax_opening",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    taxable_income = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    tax_withheld = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_tax_opening_pkey", x => x.id);
                    table.CheckConstraint("ck_eto_amounts", "taxable_income >= 0 AND tax_withheld >= 0 AND tax_withheld <= taxable_income");
                    table.ForeignKey(
                        name: "employee_tax_opening_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_tax_opening_tax_year_id_fkey",
                        column: x => x.tax_year_id,
                        principalSchema: "hrms",
                        principalTable: "tax_year",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "loan_repayment",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_loan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    receipt_number = table.Column<string>(type: "varchar", nullable: false),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("loan_repayment_pkey", x => x.id);
                    table.CheckConstraint("ck_lr_amount", "amount > 0");
                    table.ForeignKey(
                        name: "loan_repayment_employee_loan_id_fkey",
                        column: x => x.employee_loan_id,
                        principalSchema: "hrms",
                        principalTable: "employee_loan",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "idx_pr_countersigning_officer",
                schema: "hrms",
                table: "performance_review",
                column: "countersigning_officer_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pr_cso",
                schema: "hrms",
                table: "performance_review",
                sql: "countersigning_officer_id IS NULL OR (countersigning_officer_id <> employee_id AND countersigning_officer_id <> evaluator_id)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pp_bank_transfer",
                schema: "hrms",
                table: "payroll_payment",
                sql: "payment_method <> 'bank_transfer' OR bank_account_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_employee_tax_opening_tax_year_id",
                schema: "hrms",
                table: "employee_tax_opening",
                column: "tax_year_id");

            migrationBuilder.CreateIndex(
                name: "uq_eto_employee_year",
                schema: "hrms",
                table: "employee_tax_opening",
                columns: new[] { "employee_id", "tax_year_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_loan_repayment_employee_loan_id",
                schema: "hrms",
                table: "loan_repayment",
                column: "employee_loan_id");

            migrationBuilder.AddForeignKey(
                name: "performance_review_countersigning_officer_id_fkey",
                schema: "hrms",
                table: "performance_review",
                column: "countersigning_officer_id",
                principalSchema: "hrms",
                principalTable: "employee",
                principalColumn: "id");

            // the review status "countersigned" (appended) and the repayment ledger's guard
            PayAuditSql.Up(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "performance_review_countersigning_officer_id_fkey",
                schema: "hrms",
                table: "performance_review");

            migrationBuilder.DropTable(
                name: "employee_tax_opening",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "loan_repayment",
                schema: "hrms");

            migrationBuilder.DropIndex(
                name: "idx_pr_countersigning_officer",
                schema: "hrms",
                table: "performance_review");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pr_cso",
                schema: "hrms",
                table: "performance_review");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pp_bank_transfer",
                schema: "hrms",
                table: "payroll_payment");

            migrationBuilder.DropColumn(
                name: "acknowledged_at",
                schema: "hrms",
                table: "performance_review");

            migrationBuilder.DropColumn(
                name: "countersign_remarks",
                schema: "hrms",
                table: "performance_review");

            migrationBuilder.DropColumn(
                name: "countersigned_at",
                schema: "hrms",
                table: "performance_review");

            migrationBuilder.DropColumn(
                name: "countersigning_officer_id",
                schema: "hrms",
                table: "performance_review");

            migrationBuilder.DropColumn(
                name: "final_score_reason",
                schema: "hrms",
                table: "performance_review");

            migrationBuilder.DropColumn(
                name: "finalized_at",
                schema: "hrms",
                table: "performance_review");

            migrationBuilder.DropColumn(
                name: "finalized_by",
                schema: "hrms",
                table: "performance_review");

            migrationBuilder.DropColumn(
                name: "submitted_at",
                schema: "hrms",
                table: "performance_review");

            migrationBuilder.DropColumn(
                name: "calculated_at",
                schema: "hrms",
                table: "payroll_run");

            migrationBuilder.DropColumn(
                name: "finalized_by",
                schema: "hrms",
                table: "payroll_run");

            migrationBuilder.DropColumn(
                name: "paid_by",
                schema: "hrms",
                table: "payroll_run");

            migrationBuilder.DropColumn(
                name: "reversal_reason",
                schema: "hrms",
                table: "payroll_run");

            migrationBuilder.DropColumn(
                name: "reversed_at",
                schema: "hrms",
                table: "payroll_run");

            migrationBuilder.DropColumn(
                name: "reversed_by",
                schema: "hrms",
                table: "payroll_run");

            migrationBuilder.DropColumn(
                name: "waive_reason",
                schema: "hrms",
                table: "loan_installment_schedule");

            migrationBuilder.DropColumn(
                name: "waived_at",
                schema: "hrms",
                table: "loan_installment_schedule");

            migrationBuilder.DropColumn(
                name: "waived_by",
                schema: "hrms",
                table: "loan_installment_schedule");

            migrationBuilder.DropColumn(
                name: "closed_at",
                schema: "hrms",
                table: "employee_loan");

            migrationBuilder.DropColumn(
                name: "closed_by",
                schema: "hrms",
                table: "employee_loan");

            migrationBuilder.DropColumn(
                name: "closure_reason",
                schema: "hrms",
                table: "employee_loan");

            PayAuditSql.Down(migrationBuilder);

            migrationBuilder.AlterColumn<Guid>(
                name: "bank_account_id",
                schema: "hrms",
                table: "payroll_payment",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
