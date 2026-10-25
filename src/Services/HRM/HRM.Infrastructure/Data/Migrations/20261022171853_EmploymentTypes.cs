using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmploymentTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // keep each record's type as text: the enum columns and their type are dropped below (copying a value is
            // not an edit, so no employee is stamped as changed)
            migrationBuilder.Sql("""
                ALTER TABLE hrms.employee ADD COLUMN old_employment_type text;
                ALTER TABLE hrms.employee DISABLE TRIGGER trg_employee_updated_at;
                UPDATE hrms.employee SET old_employment_type = employment_type::text;
                ALTER TABLE hrms.employee ENABLE TRIGGER trg_employee_updated_at;
                ALTER TABLE hrms.post_version ADD COLUMN old_employment_type text;
                UPDATE hrms.post_version SET old_employment_type = employment_type::text;
                ALTER TABLE hrms.salary_component_rule ADD COLUMN old_employment_type text;
                UPDATE hrms.salary_component_rule SET old_employment_type = applicable_employment_type::text;
                """);

            migrationBuilder.DropIndex(
                name: "idx_employee_type",
                schema: "hrms",
                table: "employee");

            migrationBuilder.DropCheckConstraint(
                name: "ck_emp_method",
                schema: "hrms",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "applicable_employment_type",
                schema: "hrms",
                table: "salary_component_rule");

            migrationBuilder.DropColumn(
                name: "employment_type",
                schema: "hrms",
                table: "post_version");

            migrationBuilder.DropColumn(
                name: "employment_type",
                schema: "hrms",
                table: "employee");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:hrms.address_type_enum", "current,mailing,other,permanent")
                .Annotation("Npgsql:Enum:hrms.adjustment_type_enum", "arrears,bonus,correction,other,recovery")
                .Annotation("Npgsql:Enum:hrms.assignment_type_enum", "acting,additional_charge,look_after,regular")
                .Annotation("Npgsql:Enum:hrms.attendance_status_enum", "absent,half_day,holiday,late,official_duty,on_leave,present,weekend")
                .Annotation("Npgsql:Enum:hrms.calculation_method_enum", "fixed,formula,percentage,tiered")
                .Annotation("Npgsql:Enum:hrms.component_source_enum", "adjustment,gpf,loan,manual,override,rule,tax")
                .Annotation("Npgsql:Enum:hrms.component_type_enum", "deduction,earning")
                .Annotation("Npgsql:Enum:hrms.contact_type_enum", "email,mobile,other,phone,whatsapp")
                .Annotation("Npgsql:Enum:hrms.employee_request_status_enum", "approved,cancelled,pending,rejected")
                .Annotation("Npgsql:Enum:hrms.employment_basis_enum", "deputation,fixed_term,project,regular")
                .Annotation("Npgsql:Enum:hrms.employment_method_enum", "gda_personal,scheduled_seat")
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
                .Annotation("Npgsql:Enum:hrms.qualification_level_enum", "bachelors,diploma,intermediate,masters,matric,middle,mphil,other,phd,primary")
                .Annotation("Npgsql:Enum:hrms.record_status_enum", "active,inactive")
                .Annotation("Npgsql:Enum:hrms.review_status_enum", "acknowledged,countersigned,draft,finalized,submitted")
                .Annotation("Npgsql:Enum:hrms.separation_type_enum", "compulsory_retirement,death,deputation_end,dismissal,end_of_contract,other,premature_retirement,removal_from_service,resignation,retirement,termination")
                .Annotation("Npgsql:Enum:hrms.task_priority_enum", "high,low,medium,urgent")
                .Annotation("Npgsql:Enum:hrms.task_status_enum", "cancelled,completed,in_progress,on_hold,pending")
                .Annotation("Npgsql:Enum:hrms.verification_status_enum", "pending,rejected,unverified,verified")
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .OldAnnotation("Npgsql:Enum:hrms.address_type_enum", "current,mailing,other,permanent")
                .OldAnnotation("Npgsql:Enum:hrms.adjustment_type_enum", "arrears,bonus,correction,other,recovery")
                .OldAnnotation("Npgsql:Enum:hrms.assignment_type_enum", "acting,additional_charge,look_after,regular")
                .OldAnnotation("Npgsql:Enum:hrms.attendance_status_enum", "absent,half_day,holiday,late,official_duty,on_leave,present,weekend")
                .OldAnnotation("Npgsql:Enum:hrms.calculation_method_enum", "fixed,formula,percentage,tiered")
                .OldAnnotation("Npgsql:Enum:hrms.component_source_enum", "adjustment,gpf,loan,manual,override,rule,tax")
                .OldAnnotation("Npgsql:Enum:hrms.component_type_enum", "deduction,earning")
                .OldAnnotation("Npgsql:Enum:hrms.contact_type_enum", "email,mobile,other,phone,whatsapp")
                .OldAnnotation("Npgsql:Enum:hrms.employee_request_status_enum", "approved,cancelled,pending,rejected")
                .OldAnnotation("Npgsql:Enum:hrms.employment_method_enum", "gda_personal,scheduled_seat")
                .OldAnnotation("Npgsql:Enum:hrms.employment_status_enum", "active,contract_ended,deceased,deputed_out,dismissed,on_leave,removed,repatriated,resigned,retired,suspended,terminated")
                .OldAnnotation("Npgsql:Enum:hrms.employment_type_enum", "adhoc,contract,daily_wage,deputation_in,project_based,regular")
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
                .OldAnnotation("Npgsql:Enum:hrms.qualification_level_enum", "bachelors,diploma,intermediate,masters,matric,middle,mphil,other,phd,primary")
                .OldAnnotation("Npgsql:Enum:hrms.record_status_enum", "active,inactive")
                .OldAnnotation("Npgsql:Enum:hrms.review_status_enum", "acknowledged,countersigned,draft,finalized,submitted")
                .OldAnnotation("Npgsql:Enum:hrms.separation_type_enum", "compulsory_retirement,death,deputation_end,dismissal,end_of_contract,other,premature_retirement,removal_from_service,resignation,retirement,termination")
                .OldAnnotation("Npgsql:Enum:hrms.task_priority_enum", "high,low,medium,urgent")
                .OldAnnotation("Npgsql:Enum:hrms.task_status_enum", "cancelled,completed,in_progress,on_hold,pending")
                .OldAnnotation("Npgsql:Enum:hrms.verification_status_enum", "pending,rejected,unverified,verified")
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.AddColumn<Guid>(
                name: "applicable_employment_type_id",
                schema: "hrms",
                table: "salary_component_rule",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "employment_type_id",
                schema: "hrms",
                table: "post_version",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<EmploymentBasis>(
                name: "employment_basis",
                schema: "hrms",
                table: "employee",
                type: "hrms.employment_basis_enum",
                nullable: false,
                defaultValue: EmploymentBasis.Regular);

            migrationBuilder.AddColumn<Guid>(
                name: "employment_type_id",
                schema: "hrms",
                table: "employee",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "employment_type",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    description = table.Column<string>(type: "varchar", nullable: true),
                    basis = table.Column<EmploymentBasis>(type: "hrms.employment_basis_enum", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("employment_type_pkey", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_salary_component_rule_applicable_employment_type_id",
                schema: "hrms",
                table: "salary_component_rule",
                column: "applicable_employment_type_id");

            migrationBuilder.CreateIndex(
                name: "idx_post_version_employment_type_id",
                schema: "hrms",
                table: "post_version",
                column: "employment_type_id");

            migrationBuilder.CreateIndex(
                name: "idx_employee_type",
                schema: "hrms",
                table: "employee",
                column: "employment_type_id");

            migrationBuilder.CreateIndex(
                name: "employment_type_name_key",
                schema: "hrms",
                table: "employment_type",
                column: "name",
                unique: true);

            // the starting types, plus a type for each other old value a record still uses; every employee, post and
            // pay rule keeps the type it had
            migrationBuilder.Sql("""
                INSERT INTO hrms.employment_type (id, name, description, basis, is_active) VALUES
                  ('6f1d2c3a-0001-4c1e-9a10-000000000001', 'Regular', 'On the permanent establishment', 'regular', true),
                  ('6f1d2c3a-0002-4c1e-9a10-000000000002', 'Contract', 'For a fixed term', 'fixed_term', true);
                CREATE TEMP TABLE employment_type_map (old text, name text, description text, basis hrms.employment_basis_enum) ON COMMIT DROP;
                INSERT INTO employment_type_map VALUES
                  ('regular', 'Regular', NULL, 'regular'),
                  ('contract', 'Contract', NULL, 'fixed_term'),
                  ('deputation_in', 'Deputation', 'Lent by another government department', 'deputation'),
                  ('project_based', 'Project', 'Engaged for a development project', 'project'),
                  ('adhoc', 'Adhoc', 'Appointed on an adhoc basis', 'fixed_term'),
                  ('daily_wage', 'Daily wage', 'Paid by the day', 'fixed_term');
                INSERT INTO hrms.employment_type (name, description, basis)
                  SELECT m.name, m.description, m.basis FROM employment_type_map m
                  WHERE m.old NOT IN ('regular', 'contract')
                    AND (EXISTS (SELECT 1 FROM hrms.employee e WHERE e.old_employment_type = m.old)
                      OR EXISTS (SELECT 1 FROM hrms.post_version p WHERE p.old_employment_type = m.old)
                      OR EXISTS (SELECT 1 FROM hrms.salary_component_rule r WHERE r.old_employment_type = m.old));
                ALTER TABLE hrms.employee DISABLE TRIGGER trg_employee_updated_at;
                UPDATE hrms.employee e SET employment_type_id = t.id, employment_basis = t.basis
                  FROM employment_type_map m JOIN hrms.employment_type t ON t.name = m.name WHERE m.old = e.old_employment_type;
                ALTER TABLE hrms.employee ENABLE TRIGGER trg_employee_updated_at;
                UPDATE hrms.post_version p SET employment_type_id = t.id
                  FROM employment_type_map m JOIN hrms.employment_type t ON t.name = m.name WHERE m.old = p.old_employment_type;
                UPDATE hrms.salary_component_rule r SET applicable_employment_type_id = t.id
                  FROM employment_type_map m JOIN hrms.employment_type t ON t.name = m.name WHERE m.old = r.old_employment_type;
                ALTER TABLE hrms.employee DROP COLUMN old_employment_type;
                ALTER TABLE hrms.post_version DROP COLUMN old_employment_type;
                ALTER TABLE hrms.salary_component_rule DROP COLUMN old_employment_type;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_emp_method",
                schema: "hrms",
                table: "employee",
                sql: "(employment_basis = 'regular' AND employment_method IS NOT NULL) OR (employment_basis <> 'regular' AND employment_method IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "employee_employment_type_id_fkey",
                schema: "hrms",
                table: "employee",
                column: "employment_type_id",
                principalSchema: "hrms",
                principalTable: "employment_type",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "post_version_employment_type_id_fkey",
                schema: "hrms",
                table: "post_version",
                column: "employment_type_id",
                principalSchema: "hrms",
                principalTable: "employment_type",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "salary_component_rule_applicable_employment_type_id_fkey",
                schema: "hrms",
                table: "salary_component_rule",
                column: "applicable_employment_type_id",
                principalSchema: "hrms",
                principalTable: "employment_type",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "employee_employment_type_id_fkey",
                schema: "hrms",
                table: "employee");

            migrationBuilder.DropForeignKey(
                name: "post_version_employment_type_id_fkey",
                schema: "hrms",
                table: "post_version");

            migrationBuilder.DropForeignKey(
                name: "salary_component_rule_applicable_employment_type_id_fkey",
                schema: "hrms",
                table: "salary_component_rule");

            migrationBuilder.DropTable(
                name: "employment_type",
                schema: "hrms");

            migrationBuilder.DropIndex(
                name: "idx_salary_component_rule_applicable_employment_type_id",
                schema: "hrms",
                table: "salary_component_rule");

            migrationBuilder.DropIndex(
                name: "idx_post_version_employment_type_id",
                schema: "hrms",
                table: "post_version");

            migrationBuilder.DropIndex(
                name: "idx_employee_type",
                schema: "hrms",
                table: "employee");

            migrationBuilder.DropCheckConstraint(
                name: "ck_emp_method",
                schema: "hrms",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "applicable_employment_type_id",
                schema: "hrms",
                table: "salary_component_rule");

            migrationBuilder.DropColumn(
                name: "employment_type_id",
                schema: "hrms",
                table: "post_version");

            migrationBuilder.DropColumn(
                name: "employment_basis",
                schema: "hrms",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "employment_type_id",
                schema: "hrms",
                table: "employee");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:hrms.address_type_enum", "current,mailing,other,permanent")
                .Annotation("Npgsql:Enum:hrms.adjustment_type_enum", "arrears,bonus,correction,other,recovery")
                .Annotation("Npgsql:Enum:hrms.assignment_type_enum", "acting,additional_charge,look_after,regular")
                .Annotation("Npgsql:Enum:hrms.attendance_status_enum", "absent,half_day,holiday,late,official_duty,on_leave,present,weekend")
                .Annotation("Npgsql:Enum:hrms.calculation_method_enum", "fixed,formula,percentage,tiered")
                .Annotation("Npgsql:Enum:hrms.component_source_enum", "adjustment,gpf,loan,manual,override,rule,tax")
                .Annotation("Npgsql:Enum:hrms.component_type_enum", "deduction,earning")
                .Annotation("Npgsql:Enum:hrms.contact_type_enum", "email,mobile,other,phone,whatsapp")
                .Annotation("Npgsql:Enum:hrms.employee_request_status_enum", "approved,cancelled,pending,rejected")
                .Annotation("Npgsql:Enum:hrms.employment_method_enum", "gda_personal,scheduled_seat")
                .Annotation("Npgsql:Enum:hrms.employment_status_enum", "active,contract_ended,deceased,deputed_out,dismissed,on_leave,removed,repatriated,resigned,retired,suspended,terminated")
                .Annotation("Npgsql:Enum:hrms.employment_type_enum", "adhoc,contract,daily_wage,deputation_in,project_based,regular")
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
                .Annotation("Npgsql:Enum:hrms.qualification_level_enum", "bachelors,diploma,intermediate,masters,matric,middle,mphil,other,phd,primary")
                .Annotation("Npgsql:Enum:hrms.record_status_enum", "active,inactive")
                .Annotation("Npgsql:Enum:hrms.review_status_enum", "acknowledged,countersigned,draft,finalized,submitted")
                .Annotation("Npgsql:Enum:hrms.separation_type_enum", "compulsory_retirement,death,deputation_end,dismissal,end_of_contract,other,premature_retirement,removal_from_service,resignation,retirement,termination")
                .Annotation("Npgsql:Enum:hrms.task_priority_enum", "high,low,medium,urgent")
                .Annotation("Npgsql:Enum:hrms.task_status_enum", "cancelled,completed,in_progress,on_hold,pending")
                .Annotation("Npgsql:Enum:hrms.verification_status_enum", "pending,rejected,unverified,verified")
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .OldAnnotation("Npgsql:Enum:hrms.address_type_enum", "current,mailing,other,permanent")
                .OldAnnotation("Npgsql:Enum:hrms.adjustment_type_enum", "arrears,bonus,correction,other,recovery")
                .OldAnnotation("Npgsql:Enum:hrms.assignment_type_enum", "acting,additional_charge,look_after,regular")
                .OldAnnotation("Npgsql:Enum:hrms.attendance_status_enum", "absent,half_day,holiday,late,official_duty,on_leave,present,weekend")
                .OldAnnotation("Npgsql:Enum:hrms.calculation_method_enum", "fixed,formula,percentage,tiered")
                .OldAnnotation("Npgsql:Enum:hrms.component_source_enum", "adjustment,gpf,loan,manual,override,rule,tax")
                .OldAnnotation("Npgsql:Enum:hrms.component_type_enum", "deduction,earning")
                .OldAnnotation("Npgsql:Enum:hrms.contact_type_enum", "email,mobile,other,phone,whatsapp")
                .OldAnnotation("Npgsql:Enum:hrms.employee_request_status_enum", "approved,cancelled,pending,rejected")
                .OldAnnotation("Npgsql:Enum:hrms.employment_basis_enum", "deputation,fixed_term,project,regular")
                .OldAnnotation("Npgsql:Enum:hrms.employment_method_enum", "gda_personal,scheduled_seat")
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
                .OldAnnotation("Npgsql:Enum:hrms.qualification_level_enum", "bachelors,diploma,intermediate,masters,matric,middle,mphil,other,phd,primary")
                .OldAnnotation("Npgsql:Enum:hrms.record_status_enum", "active,inactive")
                .OldAnnotation("Npgsql:Enum:hrms.review_status_enum", "acknowledged,countersigned,draft,finalized,submitted")
                .OldAnnotation("Npgsql:Enum:hrms.separation_type_enum", "compulsory_retirement,death,deputation_end,dismissal,end_of_contract,other,premature_retirement,removal_from_service,resignation,retirement,termination")
                .OldAnnotation("Npgsql:Enum:hrms.task_priority_enum", "high,low,medium,urgent")
                .OldAnnotation("Npgsql:Enum:hrms.task_status_enum", "cancelled,completed,in_progress,on_hold,pending")
                .OldAnnotation("Npgsql:Enum:hrms.verification_status_enum", "pending,rejected,unverified,verified")
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.AddColumn<string>(
                name: "applicable_employment_type",
                schema: "hrms",
                table: "salary_component_rule",
                type: "hrms.employment_type_enum",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "employment_type",
                schema: "hrms",
                table: "post_version",
                type: "hrms.employment_type_enum",
                nullable: true,
                defaultValueSql: "'regular'::hrms.employment_type_enum");

            migrationBuilder.AddColumn<string>(
                name: "employment_type",
                schema: "hrms",
                table: "employee",
                type: "hrms.employment_type_enum",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_employee_type",
                schema: "hrms",
                table: "employee",
                column: "employment_type");

            migrationBuilder.AddCheckConstraint(
                name: "ck_emp_method",
                schema: "hrms",
                table: "employee",
                sql: "(employment_type = 'regular' AND employment_method IS NOT NULL) OR (employment_type <> 'regular' AND employment_method IS NULL)");
        }
    }
}
