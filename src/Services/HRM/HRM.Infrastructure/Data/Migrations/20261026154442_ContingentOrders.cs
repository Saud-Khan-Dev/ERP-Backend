using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ContingentOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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
                .OldAnnotation("Npgsql:Enum:hrms.review_status_enum", "acknowledged,countersigned,draft,finalized,submitted")
                .OldAnnotation("Npgsql:Enum:hrms.separation_type_enum", "compulsory_retirement,death,deputation_end,dismissal,end_of_contract,other,premature_retirement,removal_from_service,resignation,retirement,termination")
                .OldAnnotation("Npgsql:Enum:hrms.task_priority_enum", "high,low,medium,urgent")
                .OldAnnotation("Npgsql:Enum:hrms.task_status_enum", "cancelled,completed,in_progress,on_hold,pending")
                .OldAnnotation("Npgsql:Enum:hrms.verification_status_enum", "pending,rejected,unverified,verified")
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "engagement_order",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    designation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    wage_basis = table.Column<WageBasis>(type: "hrms.wage_basis_enum", nullable: false),
                    rate = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    letter_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<RecordStatus>(type: "hrms.record_status_enum", nullable: false, defaultValue: RecordStatus.Active),
                    cancel_reason = table.Column<string>(type: "varchar", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("engagement_order_pkey", x => x.id);
                    table.CheckConstraint("ck_eo_dates", "end_date >= start_date");
                    table.CheckConstraint("ck_eo_rate", "rate > 0");
                    table.ForeignKey(
                        name: "engagement_order_designation_id_fkey",
                        column: x => x.designation_id,
                        principalSchema: "hrms",
                        principalTable: "designation",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "engagement_order_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "engagement_order_letter_document_id_fkey",
                        column: x => x.letter_document_id,
                        principalSchema: "hrms",
                        principalTable: "employee_document",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "engagement_order_org_unit_id_fkey",
                        column: x => x.org_unit_id,
                        principalSchema: "hrms",
                        principalTable: "organization_unit",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "idx_engagement_order_employee_start",
                schema: "hrms",
                table: "engagement_order",
                columns: new[] { "employee_id", "start_date" });

            migrationBuilder.CreateIndex(
                name: "idx_engagement_order_org_unit_id",
                schema: "hrms",
                table: "engagement_order",
                column: "org_unit_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "engagement_order",
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
                .Annotation("Npgsql:Enum:hrms.review_status_enum", "acknowledged,countersigned,draft,finalized,submitted")
                .Annotation("Npgsql:Enum:hrms.separation_type_enum", "compulsory_retirement,death,deputation_end,dismissal,end_of_contract,other,premature_retirement,removal_from_service,resignation,retirement,termination")
                .Annotation("Npgsql:Enum:hrms.task_priority_enum", "high,low,medium,urgent")
                .Annotation("Npgsql:Enum:hrms.task_status_enum", "cancelled,completed,in_progress,on_hold,pending")
                .Annotation("Npgsql:Enum:hrms.verification_status_enum", "pending,rejected,unverified,verified")
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
        }
    }
}
