using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProfileChangeRequests : Migration
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
                .Annotation("Npgsql:Enum:hrms.profile_change_action_enum", "add,change,remove")
                .Annotation("Npgsql:Enum:hrms.profile_change_area_enum", "address,contact,emergency_contact,family_member,photo")
                .Annotation("Npgsql:Enum:hrms.profile_change_status_enum", "approved,pending,rejected,withdrawn")
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
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "profile_change_request",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    area = table.Column<ProfileChangeArea>(type: "hrms.profile_change_area_enum", nullable: false),
                    action = table.Column<ProfileChangeAction>(type: "hrms.profile_change_action_enum", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    proposed = table.Column<string>(type: "jsonb", nullable: true),
                    summary = table.Column<string>(type: "varchar", nullable: false),
                    before = table.Column<string>(type: "varchar", nullable: true),
                    photo_path = table.Column<string>(type: "varchar", nullable: true),
                    note = table.Column<string>(type: "varchar", nullable: true),
                    supporting_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<ProfileChangeStatus>(type: "hrms.profile_change_status_enum", nullable: false, defaultValue: ProfileChangeStatus.Pending),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    submitted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    review_remarks = table.Column<string>(type: "varchar", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("profile_change_request_pkey", x => x.id);
                    table.CheckConstraint("ck_pcr_photo", "(area = 'photo') = (photo_path IS NOT NULL)");
                    table.CheckConstraint("ck_pcr_rejection_reason", "status <> 'rejected' OR review_remarks IS NOT NULL");
                    table.CheckConstraint("ck_pcr_review", "status NOT IN ('approved','rejected') OR (reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "profile_change_request_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "profile_change_request_supporting_document_id_fkey",
                        column: x => x.supporting_document_id,
                        principalSchema: "hrms",
                        principalTable: "employee_document",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "idx_pcr_employee",
                schema: "hrms",
                table: "profile_change_request",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "idx_pcr_status",
                schema: "hrms",
                table: "profile_change_request",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_pcr_supporting_document",
                schema: "hrms",
                table: "profile_change_request",
                column: "supporting_document_id");

            migrationBuilder.CreateIndex(
                name: "ux_pcr_one_waiting",
                schema: "hrms",
                table: "profile_change_request",
                columns: new[] { "employee_id", "area", "target_id" },
                unique: true,
                filter: "status = 'pending' AND (target_id IS NOT NULL OR area = 'photo')")
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "profile_change_request",
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
                .OldAnnotation("Npgsql:Enum:hrms.profile_change_action_enum", "add,change,remove")
                .OldAnnotation("Npgsql:Enum:hrms.profile_change_area_enum", "address,contact,emergency_contact,family_member,photo")
                .OldAnnotation("Npgsql:Enum:hrms.profile_change_status_enum", "approved,pending,rejected,withdrawn")
                .OldAnnotation("Npgsql:Enum:hrms.record_status_enum", "active,inactive")
                .OldAnnotation("Npgsql:Enum:hrms.review_status_enum", "acknowledged,countersigned,draft,finalized,submitted")
                .OldAnnotation("Npgsql:Enum:hrms.separation_type_enum", "compulsory_retirement,death,deputation_end,dismissal,end_of_contract,other,premature_retirement,removal_from_service,resignation,retirement,termination")
                .OldAnnotation("Npgsql:Enum:hrms.task_priority_enum", "high,low,medium,urgent")
                .OldAnnotation("Npgsql:Enum:hrms.task_status_enum", "cancelled,completed,in_progress,on_hold,pending")
                .OldAnnotation("Npgsql:Enum:hrms.verification_status_enum", "pending,rejected,unverified,verified")
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,");
        }
    }
}
