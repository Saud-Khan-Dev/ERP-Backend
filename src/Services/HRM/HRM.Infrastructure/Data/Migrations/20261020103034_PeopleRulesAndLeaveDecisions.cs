using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PeopleRulesAndLeaveDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "hrms",
                table: "leave_type",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_placeholder",
                schema: "hrms",
                table: "leave_entitlement",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "decided_at",
                schema: "hrms",
                table: "leave_application",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "decided_by",
                schema: "hrms",
                table: "leave_application",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "decision_reason",
                schema: "hrms",
                table: "leave_application",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "hr_action_id",
                schema: "hrms",
                table: "leave_application",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "applied_at",
                schema: "hrms",
                table: "hr_action_request",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "applied_by",
                schema: "hrms",
                table: "hr_action_request",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "basic_pay",
                schema: "hrms",
                table: "hr_action_request",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "decided_at",
                schema: "hrms",
                table: "hr_action_request",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "decided_by",
                schema: "hrms",
                table: "hr_action_request",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "decision_remarks",
                schema: "hrms",
                table: "hr_action_request",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<EmploymentMethod>(
                name: "employment_method",
                schema: "hrms",
                table: "hr_action_request",
                type: "hrms.employment_method_enum",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "end_date",
                schema: "hrms",
                table: "hr_action_request",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "event_type_id",
                schema: "hrms",
                table: "hr_action_request",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_reference_org",
                schema: "hrms",
                table: "hr_action_request",
                type: "varchar",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "leave_application_id",
                schema: "hrms",
                table: "hr_action_request",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "notice_period_days",
                schema: "hrms",
                table: "hr_action_request",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "recruitment_method_id",
                schema: "hrms",
                table: "hr_action_request",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "stage_number",
                schema: "hrms",
                table: "hr_action_request",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "submitted_at",
                schema: "hrms",
                table: "hr_action_request",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "submitted_by",
                schema: "hrms",
                table: "hr_action_request",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "increment_deferred_days",
                schema: "hrms",
                table: "employee_pay_record",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "lending_department",
                schema: "hrms",
                table: "employee",
                type: "varchar",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "term_from",
                schema: "hrms",
                table: "employee",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "term_to",
                schema: "hrms",
                table: "employee",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "employee_change_log",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field = table.Column<string>(type: "varchar", nullable: false),
                    old_value = table.Column<string>(type: "varchar", nullable: true),
                    new_value = table.Column<string>(type: "varchar", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_by_name = table.Column<string>(type: "varchar", nullable: true),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_change_log_pkey", x => x.id);
                    table.ForeignKey(
                        name: "employee_change_log_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "idx_leave_application_hr_action_id",
                schema: "hrms",
                table: "leave_application",
                column: "hr_action_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_la_rejection",
                schema: "hrms",
                table: "leave_application",
                sql: "status <> 'rejected' OR decision_reason IS NOT NULL OR decided_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_hr_action_request_event_type_id",
                schema: "hrms",
                table: "hr_action_request",
                column: "event_type_id");

            migrationBuilder.CreateIndex(
                name: "idx_hr_action_request_leave_application_id",
                schema: "hrms",
                table: "hr_action_request",
                column: "leave_application_id");

            migrationBuilder.CreateIndex(
                name: "idx_hr_action_request_recruitment_method_id",
                schema: "hrms",
                table: "hr_action_request",
                column: "recruitment_method_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_har_end",
                schema: "hrms",
                table: "hr_action_request",
                sql: "end_date IS NULL OR end_date >= effective_date");

            migrationBuilder.AddCheckConstraint(
                name: "ck_har_pay",
                schema: "hrms",
                table: "hr_action_request",
                sql: "(stage_number IS NULL OR stage_number >= 0) AND (basic_pay IS NULL OR basic_pay >= 0) AND (notice_period_days IS NULL OR notice_period_days >= 0)");

            migrationBuilder.CreateIndex(
                name: "idx_employee_term_to",
                schema: "hrms",
                table: "employee",
                column: "term_to");

            migrationBuilder.AddCheckConstraint(
                name: "ck_emp_term",
                schema: "hrms",
                table: "employee",
                sql: "term_to IS NULL OR term_from IS NULL OR term_to >= term_from");

            migrationBuilder.CreateIndex(
                name: "idx_ecl_employee_at",
                schema: "hrms",
                table: "employee_change_log",
                columns: new[] { "employee_id", "changed_at" });

            migrationBuilder.AddForeignKey(
                name: "hr_action_request_event_type_id_fkey",
                schema: "hrms",
                table: "hr_action_request",
                column: "event_type_id",
                principalSchema: "hrms",
                principalTable: "service_event_type",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "hr_action_request_leave_application_id_fkey",
                schema: "hrms",
                table: "hr_action_request",
                column: "leave_application_id",
                principalSchema: "hrms",
                principalTable: "leave_application",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "hr_action_request_recruitment_method_id_fkey",
                schema: "hrms",
                table: "hr_action_request",
                column: "recruitment_method_id",
                principalSchema: "hrms",
                principalTable: "recruitment_method",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_la_hr_action",
                schema: "hrms",
                table: "leave_application",
                column: "hr_action_id",
                principalSchema: "hrms",
                principalTable: "hr_action_request",
                principalColumn: "id");

            // the enum labels (in the C# declaration order), the new seat check and the change log's guard
            PeopleRulesSql.Up(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // enum labels cannot be dropped from Postgres types; they stay
            PeopleRulesSql.Down(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "hr_action_request_event_type_id_fkey",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropForeignKey(
                name: "hr_action_request_leave_application_id_fkey",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropForeignKey(
                name: "hr_action_request_recruitment_method_id_fkey",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropForeignKey(
                name: "fk_la_hr_action",
                schema: "hrms",
                table: "leave_application");

            migrationBuilder.DropTable(
                name: "employee_change_log",
                schema: "hrms");

            migrationBuilder.DropIndex(
                name: "idx_leave_application_hr_action_id",
                schema: "hrms",
                table: "leave_application");

            migrationBuilder.DropCheckConstraint(
                name: "ck_la_rejection",
                schema: "hrms",
                table: "leave_application");

            migrationBuilder.DropIndex(
                name: "idx_hr_action_request_event_type_id",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropIndex(
                name: "idx_hr_action_request_leave_application_id",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropIndex(
                name: "idx_hr_action_request_recruitment_method_id",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropCheckConstraint(
                name: "ck_har_end",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropCheckConstraint(
                name: "ck_har_pay",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropIndex(
                name: "idx_employee_term_to",
                schema: "hrms",
                table: "employee");

            migrationBuilder.DropCheckConstraint(
                name: "ck_emp_term",
                schema: "hrms",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "hrms",
                table: "leave_type");

            migrationBuilder.DropColumn(
                name: "is_placeholder",
                schema: "hrms",
                table: "leave_entitlement");

            migrationBuilder.DropColumn(
                name: "decided_at",
                schema: "hrms",
                table: "leave_application");

            migrationBuilder.DropColumn(
                name: "decided_by",
                schema: "hrms",
                table: "leave_application");

            migrationBuilder.DropColumn(
                name: "decision_reason",
                schema: "hrms",
                table: "leave_application");

            migrationBuilder.DropColumn(
                name: "hr_action_id",
                schema: "hrms",
                table: "leave_application");

            migrationBuilder.DropColumn(
                name: "applied_at",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropColumn(
                name: "applied_by",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropColumn(
                name: "basic_pay",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropColumn(
                name: "decided_at",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropColumn(
                name: "decided_by",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropColumn(
                name: "decision_remarks",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropColumn(
                name: "employment_method",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropColumn(
                name: "end_date",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropColumn(
                name: "event_type_id",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropColumn(
                name: "external_reference_org",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropColumn(
                name: "leave_application_id",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropColumn(
                name: "notice_period_days",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropColumn(
                name: "recruitment_method_id",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropColumn(
                name: "stage_number",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropColumn(
                name: "submitted_at",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropColumn(
                name: "submitted_by",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropColumn(
                name: "increment_deferred_days",
                schema: "hrms",
                table: "employee_pay_record");

            migrationBuilder.DropColumn(
                name: "lending_department",
                schema: "hrms",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "term_from",
                schema: "hrms",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "term_to",
                schema: "hrms",
                table: "employee");

        }
    }
}
