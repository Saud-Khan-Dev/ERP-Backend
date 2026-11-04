using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovePerformanceSimplerEducation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "performance_competency",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "performance_goal",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "performance_kpi",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "performance_review",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "performance_period",
                schema: "hrms");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:hrms.address_type_enum", "current,mailing,other,permanent")
                .Annotation("Npgsql:Enum:hrms.adjustment_direction_enum", "either,lowers,raises")
                .Annotation("Npgsql:Enum:hrms.assignment_type_enum", "acting,additional_charge,look_after,regular")
                .Annotation("Npgsql:Enum:hrms.attendance_status_enum", "absent,half_day,holiday,late,official_duty,on_leave,present,weekend")
                .Annotation("Npgsql:Enum:hrms.calculation_method_enum", "fixed,formula,percentage,tiered")
                .Annotation("Npgsql:Enum:hrms.component_source_enum", "adjustment,gpf,loan,manual,override,rule,tax")
                .Annotation("Npgsql:Enum:hrms.component_type_enum", "deduction,earning")
                .Annotation("Npgsql:Enum:hrms.contact_type_enum", "email,mobile,other,phone,whatsapp")
                .Annotation("Npgsql:Enum:hrms.employee_request_status_enum", "approved,cancelled,pending,rejected")
                .Annotation("Npgsql:Enum:hrms.employment_basis_enum", "deputation,fixed_term,project,regular")
                .Annotation("Npgsql:Enum:hrms.employment_status_enum", "active,contract_ended,deceased,deputed_out,dismissed,on_leave,removed,repatriated,resigned,retired,suspended,terminated")
                .Annotation("Npgsql:Enum:hrms.gender_enum", "female,male,other,transgender")
                .Annotation("Npgsql:Enum:hrms.gpf_txn_type_enum", "adjustment,advance,advance_recovery,final_payment,interest,opening,subscription,withdrawal")
                .Annotation("Npgsql:Enum:hrms.holiday_type_enum", "optional,provincial,public,religious")
                .Annotation("Npgsql:Enum:hrms.hr_action_status_enum", "applied,approved,cancelled,draft,pending,rejected")
                .Annotation("Npgsql:Enum:hrms.hr_action_type_enum", "appointment,compulsory_retirement,death,demotion,deputation_end,deputation_in,deputation_out,dismissal,end_of_contract,joining,lwop,other,premature_retirement,promotion,regularization,reinstatement,removal_from_service,resignation,retirement,suspension,termination,transfer")
                .Annotation("Npgsql:Enum:hrms.installment_status_enum", "paid,partial,pending,waived")
                .Annotation("Npgsql:Enum:hrms.leave_status_enum", "approved,cancelled,pending,rejected")
                .Annotation("Npgsql:Enum:hrms.leave_txn_type_enum", "accrual,adjustment,carry_forward,encashment,lapse,opening,usage,usage_reversal")
                .Annotation("Npgsql:Enum:hrms.loan_status_enum", "active,cancelled,completed,pending,written_off")
                .Annotation("Npgsql:Enum:hrms.marital_status_enum", "divorced,married,single,widowed")
                .Annotation("Npgsql:Enum:hrms.payment_method_enum", "bank_transfer,cash,cheque")
                .Annotation("Npgsql:Enum:hrms.payment_status_enum", "failed,pending,processed,returned")
                .Annotation("Npgsql:Enum:hrms.payroll_period_status_enum", "closed,locked,open")
                .Annotation("Npgsql:Enum:hrms.payroll_run_status_enum", "approved,calculated,draft,finalized,paid,reversed,reviewed")
                .Annotation("Npgsql:Enum:hrms.payroll_run_type_enum", "arrears,bonus,final_settlement,regular,supplementary")
                .Annotation("Npgsql:Enum:hrms.payroll_txn_status_enum", "calculated,held,released")
                .Annotation("Npgsql:Enum:hrms.position_status_enum", "abolished,filled,frozen,partially_filled,sanctioned,vacant")
                .Annotation("Npgsql:Enum:hrms.post_lifecycle_enum", "abolished,frozen,sanctioned")
                .Annotation("Npgsql:Enum:hrms.record_status_enum", "active,inactive")
                .Annotation("Npgsql:Enum:hrms.separation_type_enum", "compulsory_retirement,death,deputation_end,dismissal,end_of_contract,other,premature_retirement,removal_from_service,resignation,retirement,termination")
                .Annotation("Npgsql:Enum:hrms.task_priority_enum", "high,low,medium,urgent")
                .Annotation("Npgsql:Enum:hrms.task_status_enum", "cancelled,completed,in_progress,on_hold,pending")
                .Annotation("Npgsql:Enum:hrms.verification_status_enum", "pending,rejected,unverified,verified")
                .Annotation("Npgsql:Enum:hrms.wage_basis_enum", "daily,monthly")
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .OldAnnotation("Npgsql:Enum:hrms.address_type_enum", "current,mailing,other,permanent")
                .OldAnnotation("Npgsql:Enum:hrms.adjustment_direction_enum", "either,lowers,raises")
                .OldAnnotation("Npgsql:Enum:hrms.assignment_type_enum", "acting,additional_charge,look_after,regular")
                .OldAnnotation("Npgsql:Enum:hrms.attendance_status_enum", "absent,half_day,holiday,late,official_duty,on_leave,present,weekend")
                .OldAnnotation("Npgsql:Enum:hrms.calculation_method_enum", "fixed,formula,percentage,tiered")
                .OldAnnotation("Npgsql:Enum:hrms.component_source_enum", "adjustment,gpf,loan,manual,override,rule,tax")
                .OldAnnotation("Npgsql:Enum:hrms.component_type_enum", "deduction,earning")
                .OldAnnotation("Npgsql:Enum:hrms.contact_type_enum", "email,mobile,other,phone,whatsapp")
                .OldAnnotation("Npgsql:Enum:hrms.employee_request_status_enum", "approved,cancelled,pending,rejected")
                .OldAnnotation("Npgsql:Enum:hrms.employment_basis_enum", "deputation,fixed_term,project,regular")
                .OldAnnotation("Npgsql:Enum:hrms.employment_status_enum", "active,contract_ended,deceased,deputed_out,dismissed,on_leave,removed,repatriated,resigned,retired,suspended,terminated")
                .OldAnnotation("Npgsql:Enum:hrms.gender_enum", "female,male,other,transgender")
                .OldAnnotation("Npgsql:Enum:hrms.gpf_txn_type_enum", "adjustment,advance,advance_recovery,final_payment,interest,opening,subscription,withdrawal")
                .OldAnnotation("Npgsql:Enum:hrms.holiday_type_enum", "optional,provincial,public,religious")
                .OldAnnotation("Npgsql:Enum:hrms.hr_action_status_enum", "applied,approved,cancelled,draft,pending,rejected")
                .OldAnnotation("Npgsql:Enum:hrms.hr_action_type_enum", "appointment,compulsory_retirement,death,demotion,deputation_end,deputation_in,deputation_out,dismissal,end_of_contract,joining,lwop,other,premature_retirement,promotion,regularization,reinstatement,removal_from_service,resignation,retirement,suspension,termination,transfer")
                .OldAnnotation("Npgsql:Enum:hrms.installment_status_enum", "paid,partial,pending,waived")
                .OldAnnotation("Npgsql:Enum:hrms.leave_status_enum", "approved,cancelled,pending,rejected")
                .OldAnnotation("Npgsql:Enum:hrms.leave_txn_type_enum", "accrual,adjustment,carry_forward,encashment,lapse,opening,usage,usage_reversal")
                .OldAnnotation("Npgsql:Enum:hrms.loan_status_enum", "active,cancelled,completed,pending,written_off")
                .OldAnnotation("Npgsql:Enum:hrms.marital_status_enum", "divorced,married,single,widowed")
                .OldAnnotation("Npgsql:Enum:hrms.payment_method_enum", "bank_transfer,cash,cheque")
                .OldAnnotation("Npgsql:Enum:hrms.payment_status_enum", "failed,pending,processed,returned")
                .OldAnnotation("Npgsql:Enum:hrms.payroll_period_status_enum", "closed,locked,open")
                .OldAnnotation("Npgsql:Enum:hrms.payroll_run_status_enum", "approved,calculated,draft,finalized,paid,reversed,reviewed")
                .OldAnnotation("Npgsql:Enum:hrms.payroll_run_type_enum", "arrears,bonus,final_settlement,regular,supplementary")
                .OldAnnotation("Npgsql:Enum:hrms.payroll_txn_status_enum", "calculated,held,released")
                .OldAnnotation("Npgsql:Enum:hrms.position_status_enum", "abolished,filled,frozen,partially_filled,sanctioned,vacant")
                .OldAnnotation("Npgsql:Enum:hrms.post_lifecycle_enum", "abolished,frozen,sanctioned")
                .OldAnnotation("Npgsql:Enum:hrms.record_status_enum", "active,inactive")
                .OldAnnotation("Npgsql:Enum:hrms.review_status_enum", "acknowledged,countersigned,draft,finalized,submitted")
                .OldAnnotation("Npgsql:Enum:hrms.separation_type_enum", "compulsory_retirement,death,deputation_end,dismissal,end_of_contract,other,premature_retirement,removal_from_service,resignation,retirement,termination")
                .OldAnnotation("Npgsql:Enum:hrms.task_priority_enum", "high,low,medium,urgent")
                .OldAnnotation("Npgsql:Enum:hrms.task_status_enum", "cancelled,completed,in_progress,on_hold,pending")
                .OldAnnotation("Npgsql:Enum:hrms.verification_status_enum", "pending,rejected,unverified,verified")
                .OldAnnotation("Npgsql:Enum:hrms.wage_basis_enum", "daily,monthly")
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "institution_name",
                schema: "hrms",
                table: "employee_education",
                type: "varchar",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar");

            migrationBuilder.AlterColumn<string>(
                name: "degree_title",
                schema: "hrms",
                table: "employee_education",
                type: "varchar",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:hrms.address_type_enum", "current,mailing,other,permanent")
                .Annotation("Npgsql:Enum:hrms.adjustment_direction_enum", "either,lowers,raises")
                .Annotation("Npgsql:Enum:hrms.assignment_type_enum", "acting,additional_charge,look_after,regular")
                .Annotation("Npgsql:Enum:hrms.attendance_status_enum", "absent,half_day,holiday,late,official_duty,on_leave,present,weekend")
                .Annotation("Npgsql:Enum:hrms.calculation_method_enum", "fixed,formula,percentage,tiered")
                .Annotation("Npgsql:Enum:hrms.component_source_enum", "adjustment,gpf,loan,manual,override,rule,tax")
                .Annotation("Npgsql:Enum:hrms.component_type_enum", "deduction,earning")
                .Annotation("Npgsql:Enum:hrms.contact_type_enum", "email,mobile,other,phone,whatsapp")
                .Annotation("Npgsql:Enum:hrms.employee_request_status_enum", "approved,cancelled,pending,rejected")
                .Annotation("Npgsql:Enum:hrms.employment_basis_enum", "deputation,fixed_term,project,regular")
                .Annotation("Npgsql:Enum:hrms.employment_status_enum", "active,contract_ended,deceased,deputed_out,dismissed,on_leave,removed,repatriated,resigned,retired,suspended,terminated")
                .Annotation("Npgsql:Enum:hrms.gender_enum", "female,male,other,transgender")
                .Annotation("Npgsql:Enum:hrms.gpf_txn_type_enum", "adjustment,advance,advance_recovery,final_payment,interest,opening,subscription,withdrawal")
                .Annotation("Npgsql:Enum:hrms.holiday_type_enum", "optional,provincial,public,religious")
                .Annotation("Npgsql:Enum:hrms.hr_action_status_enum", "applied,approved,cancelled,draft,pending,rejected")
                .Annotation("Npgsql:Enum:hrms.hr_action_type_enum", "appointment,compulsory_retirement,death,demotion,deputation_end,deputation_in,deputation_out,dismissal,end_of_contract,joining,lwop,other,premature_retirement,promotion,regularization,reinstatement,removal_from_service,resignation,retirement,suspension,termination,transfer")
                .Annotation("Npgsql:Enum:hrms.installment_status_enum", "paid,partial,pending,waived")
                .Annotation("Npgsql:Enum:hrms.leave_status_enum", "approved,cancelled,pending,rejected")
                .Annotation("Npgsql:Enum:hrms.leave_txn_type_enum", "accrual,adjustment,carry_forward,encashment,lapse,opening,usage,usage_reversal")
                .Annotation("Npgsql:Enum:hrms.loan_status_enum", "active,cancelled,completed,pending,written_off")
                .Annotation("Npgsql:Enum:hrms.marital_status_enum", "divorced,married,single,widowed")
                .Annotation("Npgsql:Enum:hrms.payment_method_enum", "bank_transfer,cash,cheque")
                .Annotation("Npgsql:Enum:hrms.payment_status_enum", "failed,pending,processed,returned")
                .Annotation("Npgsql:Enum:hrms.payroll_period_status_enum", "closed,locked,open")
                .Annotation("Npgsql:Enum:hrms.payroll_run_status_enum", "approved,calculated,draft,finalized,paid,reversed,reviewed")
                .Annotation("Npgsql:Enum:hrms.payroll_run_type_enum", "arrears,bonus,final_settlement,regular,supplementary")
                .Annotation("Npgsql:Enum:hrms.payroll_txn_status_enum", "calculated,held,released")
                .Annotation("Npgsql:Enum:hrms.position_status_enum", "abolished,filled,frozen,partially_filled,sanctioned,vacant")
                .Annotation("Npgsql:Enum:hrms.post_lifecycle_enum", "abolished,frozen,sanctioned")
                .Annotation("Npgsql:Enum:hrms.record_status_enum", "active,inactive")
                .Annotation("Npgsql:Enum:hrms.review_status_enum", "acknowledged,countersigned,draft,finalized,submitted")
                .Annotation("Npgsql:Enum:hrms.separation_type_enum", "compulsory_retirement,death,deputation_end,dismissal,end_of_contract,other,premature_retirement,removal_from_service,resignation,retirement,termination")
                .Annotation("Npgsql:Enum:hrms.task_priority_enum", "high,low,medium,urgent")
                .Annotation("Npgsql:Enum:hrms.task_status_enum", "cancelled,completed,in_progress,on_hold,pending")
                .Annotation("Npgsql:Enum:hrms.verification_status_enum", "pending,rejected,unverified,verified")
                .Annotation("Npgsql:Enum:hrms.wage_basis_enum", "daily,monthly")
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .OldAnnotation("Npgsql:Enum:hrms.address_type_enum", "current,mailing,other,permanent")
                .OldAnnotation("Npgsql:Enum:hrms.adjustment_direction_enum", "either,lowers,raises")
                .OldAnnotation("Npgsql:Enum:hrms.assignment_type_enum", "acting,additional_charge,look_after,regular")
                .OldAnnotation("Npgsql:Enum:hrms.attendance_status_enum", "absent,half_day,holiday,late,official_duty,on_leave,present,weekend")
                .OldAnnotation("Npgsql:Enum:hrms.calculation_method_enum", "fixed,formula,percentage,tiered")
                .OldAnnotation("Npgsql:Enum:hrms.component_source_enum", "adjustment,gpf,loan,manual,override,rule,tax")
                .OldAnnotation("Npgsql:Enum:hrms.component_type_enum", "deduction,earning")
                .OldAnnotation("Npgsql:Enum:hrms.contact_type_enum", "email,mobile,other,phone,whatsapp")
                .OldAnnotation("Npgsql:Enum:hrms.employee_request_status_enum", "approved,cancelled,pending,rejected")
                .OldAnnotation("Npgsql:Enum:hrms.employment_basis_enum", "deputation,fixed_term,project,regular")
                .OldAnnotation("Npgsql:Enum:hrms.employment_status_enum", "active,contract_ended,deceased,deputed_out,dismissed,on_leave,removed,repatriated,resigned,retired,suspended,terminated")
                .OldAnnotation("Npgsql:Enum:hrms.gender_enum", "female,male,other,transgender")
                .OldAnnotation("Npgsql:Enum:hrms.gpf_txn_type_enum", "adjustment,advance,advance_recovery,final_payment,interest,opening,subscription,withdrawal")
                .OldAnnotation("Npgsql:Enum:hrms.holiday_type_enum", "optional,provincial,public,religious")
                .OldAnnotation("Npgsql:Enum:hrms.hr_action_status_enum", "applied,approved,cancelled,draft,pending,rejected")
                .OldAnnotation("Npgsql:Enum:hrms.hr_action_type_enum", "appointment,compulsory_retirement,death,demotion,deputation_end,deputation_in,deputation_out,dismissal,end_of_contract,joining,lwop,other,premature_retirement,promotion,regularization,reinstatement,removal_from_service,resignation,retirement,suspension,termination,transfer")
                .OldAnnotation("Npgsql:Enum:hrms.installment_status_enum", "paid,partial,pending,waived")
                .OldAnnotation("Npgsql:Enum:hrms.leave_status_enum", "approved,cancelled,pending,rejected")
                .OldAnnotation("Npgsql:Enum:hrms.leave_txn_type_enum", "accrual,adjustment,carry_forward,encashment,lapse,opening,usage,usage_reversal")
                .OldAnnotation("Npgsql:Enum:hrms.loan_status_enum", "active,cancelled,completed,pending,written_off")
                .OldAnnotation("Npgsql:Enum:hrms.marital_status_enum", "divorced,married,single,widowed")
                .OldAnnotation("Npgsql:Enum:hrms.payment_method_enum", "bank_transfer,cash,cheque")
                .OldAnnotation("Npgsql:Enum:hrms.payment_status_enum", "failed,pending,processed,returned")
                .OldAnnotation("Npgsql:Enum:hrms.payroll_period_status_enum", "closed,locked,open")
                .OldAnnotation("Npgsql:Enum:hrms.payroll_run_status_enum", "approved,calculated,draft,finalized,paid,reversed,reviewed")
                .OldAnnotation("Npgsql:Enum:hrms.payroll_run_type_enum", "arrears,bonus,final_settlement,regular,supplementary")
                .OldAnnotation("Npgsql:Enum:hrms.payroll_txn_status_enum", "calculated,held,released")
                .OldAnnotation("Npgsql:Enum:hrms.position_status_enum", "abolished,filled,frozen,partially_filled,sanctioned,vacant")
                .OldAnnotation("Npgsql:Enum:hrms.post_lifecycle_enum", "abolished,frozen,sanctioned")
                .OldAnnotation("Npgsql:Enum:hrms.record_status_enum", "active,inactive")
                .OldAnnotation("Npgsql:Enum:hrms.separation_type_enum", "compulsory_retirement,death,deputation_end,dismissal,end_of_contract,other,premature_retirement,removal_from_service,resignation,retirement,termination")
                .OldAnnotation("Npgsql:Enum:hrms.task_priority_enum", "high,low,medium,urgent")
                .OldAnnotation("Npgsql:Enum:hrms.task_status_enum", "cancelled,completed,in_progress,on_hold,pending")
                .OldAnnotation("Npgsql:Enum:hrms.verification_status_enum", "pending,rejected,unverified,verified")
                .OldAnnotation("Npgsql:Enum:hrms.wage_basis_enum", "daily,monthly")
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "institution_name",
                schema: "hrms",
                table: "employee_education",
                type: "varchar",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "degree_title",
                schema: "hrms",
                table: "employee_education",
                type: "varchar",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "performance_period",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<RecordStatus>(type: "hrms.record_status_enum", nullable: false, defaultValue: RecordStatus.Active)
                },
                constraints: table =>
                {
                    table.PrimaryKey("performance_period_pkey", x => x.id);
                    table.CheckConstraint("ck_pp_dates", "end_date >= start_date");
                });

            migrationBuilder.CreateTable(
                name: "performance_review",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    acknowledged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    countersign_remarks = table.Column<string>(type: "text", nullable: true),
                    countersigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    countersigning_officer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    final_score = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    final_score_reason = table.Column<string>(type: "text", nullable: true),
                    finalized_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    finalized_by = table.Column<Guid>(type: "uuid", nullable: true),
                    performance_grade = table.Column<string>(type: "varchar", nullable: true),
                    performance_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    promotion_recommended = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "hrms.review_status_enum", nullable: false, defaultValue: 0),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    training_recommended = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("performance_review_pkey", x => x.id);
                    table.CheckConstraint("ck_pr_cso", "countersigning_officer_id IS NULL OR (countersigning_officer_id <> employee_id AND countersigning_officer_id <> evaluator_id)");
                    table.CheckConstraint("ck_pr_not_self", "employee_id <> evaluator_id");
                    table.CheckConstraint("performance_review_final_score_check", "final_score IS NULL OR final_score BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "performance_review_countersigning_officer_id_fkey",
                        column: x => x.countersigning_officer_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "performance_review_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "performance_review_evaluator_id_fkey",
                        column: x => x.evaluator_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "performance_review_performance_period_id_fkey",
                        column: x => x.performance_period_id,
                        principalSchema: "hrms",
                        principalTable: "performance_period",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "performance_competency",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    competency_name = table.Column<string>(type: "varchar", nullable: false),
                    performance_review_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("performance_competency_pkey", x => x.id);
                    table.CheckConstraint("performance_competency_rating_check", "rating IS NULL OR rating BETWEEN 0 AND 10");
                    table.ForeignKey(
                        name: "performance_competency_performance_review_id_fkey",
                        column: x => x.performance_review_id,
                        principalSchema: "hrms",
                        principalTable: "performance_review",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "performance_goal",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    achievement = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    performance_review_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    target = table.Column<string>(type: "text", nullable: true),
                    weight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValueSql: "0")
                },
                constraints: table =>
                {
                    table.PrimaryKey("performance_goal_pkey", x => x.id);
                    table.CheckConstraint("performance_goal_score_check", "score IS NULL OR score BETWEEN 0 AND 100");
                    table.CheckConstraint("performance_goal_weight_check", "weight BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "performance_goal_performance_review_id_fkey",
                        column: x => x.performance_review_id,
                        principalSchema: "hrms",
                        principalTable: "performance_review",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "performance_kpi",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    achieved_value = table.Column<string>(type: "varchar", nullable: true),
                    kpi_name = table.Column<string>(type: "varchar", nullable: false),
                    performance_review_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    target_value = table.Column<string>(type: "varchar", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("performance_kpi_pkey", x => x.id);
                    table.CheckConstraint("performance_kpi_score_check", "score IS NULL OR score BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "performance_kpi_performance_review_id_fkey",
                        column: x => x.performance_review_id,
                        principalSchema: "hrms",
                        principalTable: "performance_review",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_performance_competency_performance_review_id",
                schema: "hrms",
                table: "performance_competency",
                column: "performance_review_id");

            migrationBuilder.CreateIndex(
                name: "idx_performance_goal_performance_review_id",
                schema: "hrms",
                table: "performance_goal",
                column: "performance_review_id");

            migrationBuilder.CreateIndex(
                name: "idx_performance_kpi_performance_review_id",
                schema: "hrms",
                table: "performance_kpi",
                column: "performance_review_id");

            migrationBuilder.CreateIndex(
                name: "idx_performance_review_performance_period_id",
                schema: "hrms",
                table: "performance_review",
                column: "performance_period_id");

            migrationBuilder.CreateIndex(
                name: "idx_pr_countersigning_officer",
                schema: "hrms",
                table: "performance_review",
                column: "countersigning_officer_id");

            migrationBuilder.CreateIndex(
                name: "idx_pr_evaluator",
                schema: "hrms",
                table: "performance_review",
                column: "evaluator_id");

            migrationBuilder.CreateIndex(
                name: "performance_review_employee_id_performance_period_id_key",
                schema: "hrms",
                table: "performance_review",
                columns: new[] { "employee_id", "performance_period_id" },
                unique: true);
        }
    }
}
