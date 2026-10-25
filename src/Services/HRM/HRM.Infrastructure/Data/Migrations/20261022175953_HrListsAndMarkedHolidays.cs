using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class HrListsAndMarkedHolidays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // keep each record's value as text: the old columns and their enum types are dropped below. Copying a value
            // is not an edit: a closed run's adjustments stay locked to everyone else, and no record is stamped as changed.
            migrationBuilder.Sql("""
                ALTER TABLE hrms.payroll_adjustment DISABLE TRIGGER trg_lock_padj;
                ALTER TABLE hrms.employee DISABLE TRIGGER trg_employee_updated_at;
                ALTER TABLE hrms.hr_action_request DISABLE TRIGGER trg_hr_action_request_updated_at;
                ALTER TABLE hrms.employee ADD COLUMN old_employment_method text;
                UPDATE hrms.employee SET old_employment_method = employment_method::text;
                ALTER TABLE hrms.hr_action_request ADD COLUMN old_employment_method text;
                UPDATE hrms.hr_action_request SET old_employment_method = employment_method::text;
                ALTER TABLE hrms.employee_family_member ADD COLUMN old_relation text;
                UPDATE hrms.employee_family_member SET old_relation = relation::text;
                ALTER TABLE hrms.employee_education ADD COLUMN old_qualification_level text;
                UPDATE hrms.employee_education SET old_qualification_level = qualification_level::text;
                ALTER TABLE hrms.payroll_adjustment ADD COLUMN old_adjustment_type text;
                UPDATE hrms.payroll_adjustment SET old_adjustment_type = adjustment_type::text;
                ALTER TABLE hrms.payroll_adjustment ENABLE TRIGGER trg_lock_padj;
                ALTER TABLE hrms.employee ENABLE TRIGGER trg_employee_updated_at;
                ALTER TABLE hrms.hr_action_request ENABLE TRIGGER trg_hr_action_request_updated_at;
                """);

            migrationBuilder.DropIndex(
                name: "idx_edu_employee_level",
                schema: "hrms",
                table: "employee_education");

            migrationBuilder.DropCheckConstraint(
                name: "ck_emp_method",
                schema: "hrms",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "adjustment_type",
                schema: "hrms",
                table: "payroll_adjustment");

            migrationBuilder.DropColumn(
                name: "employment_method",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropColumn(
                name: "relation",
                schema: "hrms",
                table: "employee_family_member");

            migrationBuilder.DropColumn(
                name: "qualification_level",
                schema: "hrms",
                table: "employee_education");

            migrationBuilder.DropColumn(
                name: "employment_method",
                schema: "hrms",
                table: "employee");

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

            migrationBuilder.AddColumn<Guid>(
                name: "adjustment_type_id",
                schema: "hrms",
                table: "payroll_adjustment",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "employment_method_id",
                schema: "hrms",
                table: "hr_action_request",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "relation_id",
                schema: "hrms",
                table: "employee_family_member",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "qualification_level_id",
                schema: "hrms",
                table: "employee_education",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "employment_method_id",
                schema: "hrms",
                table: "employee",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "adjustment_type",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    direction = table.Column<AdjustmentDirection>(type: "hrms.adjustment_direction_enum", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("adjustment_type_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "employment_method",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    description = table.Column<string>(type: "varchar", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("employment_method_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "family_relation",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("family_relation_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "qualification_level",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("qualification_level_pkey", x => x.id);
                    table.CheckConstraint("qualification_level_rank_check", "rank BETWEEN 1 AND 99");
                });

            migrationBuilder.CreateIndex(
                name: "idx_edu_employee_level",
                schema: "hrms",
                table: "employee_education",
                columns: new[] { "employee_id", "qualification_level_id" });

            migrationBuilder.CreateIndex(
                name: "adjustment_type_name_key",
                schema: "hrms",
                table: "adjustment_type",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "employment_method_name_key",
                schema: "hrms",
                table: "employment_method",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "family_relation_name_key",
                schema: "hrms",
                table: "family_relation",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "qualification_level_name_key",
                schema: "hrms",
                table: "qualification_level",
                column: "name",
                unique: true);

            // the starting values (the lists were fixed in code before)
            migrationBuilder.Sql("""
                INSERT INTO hrms.qualification_level (name, rank) VALUES
                  ('Primary', 1), ('Middle', 2), ('Matric', 3), ('Intermediate', 4), ('Diploma', 5),
                  ('Bachelors', 6), ('Masters', 7), ('MPhil', 8), ('PhD', 9);
                INSERT INTO hrms.family_relation (name) VALUES
                  ('Spouse'), ('Son'), ('Daughter'), ('Father'), ('Mother'), ('Brother'), ('Sister');
                INSERT INTO hrms.employment_method (name, description) VALUES
                  ('Scheduled seat', 'Against a seat in the sanctioned schedule'),
                  ('GDA personal', 'On the Authority''s own personal establishment');
                INSERT INTO hrms.adjustment_type (id, name, direction) VALUES
                  ('7a2e1c40-0001-4b6f-8d21-000000000001', 'Arrears', 'raises'),
                  ('7a2e1c40-0002-4b6f-8d21-000000000002', 'Recovery', 'lowers');
                INSERT INTO hrms.adjustment_type (name, direction) VALUES ('Bonus', 'raises'), ('Correction', 'either');
                """);

            // every existing record keeps its value, now as a row of the list: a wife or husband becomes Spouse, a
            // relation the list lacks is added to it, the old catch-all adjustment is a Correction, and the old
            // catch-all qualification gets a level only when a record still uses it (the triggers are paused as above)
            migrationBuilder.Sql("""
                ALTER TABLE hrms.payroll_adjustment DISABLE TRIGGER trg_lock_padj;
                ALTER TABLE hrms.employee DISABLE TRIGGER trg_employee_updated_at;
                ALTER TABLE hrms.hr_action_request DISABLE TRIGGER trg_hr_action_request_updated_at;

                INSERT INTO hrms.family_relation (name)
                  SELECT DISTINCT initcap(btrim(f.old_relation)) FROM hrms.employee_family_member f
                  WHERE btrim(coalesce(f.old_relation, '')) <> '' AND lower(btrim(f.old_relation)) NOT IN ('wife', 'husband')
                    AND NOT EXISTS (SELECT 1 FROM hrms.family_relation r WHERE lower(r.name) = lower(btrim(f.old_relation)));
                UPDATE hrms.employee_family_member f SET relation_id = r.id FROM hrms.family_relation r
                  WHERE lower(r.name) = CASE WHEN lower(btrim(f.old_relation)) IN ('wife', 'husband') THEN 'spouse' ELSE lower(btrim(f.old_relation)) END;

                INSERT INTO hrms.qualification_level (name, rank)
                  SELECT 'Other', 99 WHERE EXISTS (SELECT 1 FROM hrms.employee_education WHERE old_qualification_level = 'other');
                UPDATE hrms.employee_education e SET qualification_level_id = q.id FROM hrms.qualification_level q
                  WHERE lower(q.name) = e.old_qualification_level;

                UPDATE hrms.employee e SET employment_method_id = m.id FROM hrms.employment_method m
                  WHERE m.name = CASE e.old_employment_method WHEN 'scheduled_seat' THEN 'Scheduled seat' WHEN 'gda_personal' THEN 'GDA personal' END;
                UPDATE hrms.hr_action_request h SET employment_method_id = m.id FROM hrms.employment_method m
                  WHERE m.name = CASE h.old_employment_method WHEN 'scheduled_seat' THEN 'Scheduled seat' WHEN 'gda_personal' THEN 'GDA personal' END;

                UPDATE hrms.payroll_adjustment p SET adjustment_type_id = a.id FROM hrms.adjustment_type a
                  WHERE a.name = CASE p.old_adjustment_type WHEN 'arrears' THEN 'Arrears' WHEN 'recovery' THEN 'Recovery'
                    WHEN 'bonus' THEN 'Bonus' ELSE 'Correction' END;

                ALTER TABLE hrms.payroll_adjustment ENABLE TRIGGER trg_lock_padj;
                ALTER TABLE hrms.employee ENABLE TRIGGER trg_employee_updated_at;
                ALTER TABLE hrms.hr_action_request ENABLE TRIGGER trg_hr_action_request_updated_at;

                ALTER TABLE hrms.employee DROP COLUMN old_employment_method;
                ALTER TABLE hrms.hr_action_request DROP COLUMN old_employment_method;
                ALTER TABLE hrms.employee_family_member DROP COLUMN old_relation;
                ALTER TABLE hrms.employee_education DROP COLUMN old_qualification_level;
                ALTER TABLE hrms.payroll_adjustment DROP COLUMN old_adjustment_type;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_emp_method",
                schema: "hrms",
                table: "employee",
                sql: "(employment_basis = 'regular' AND employment_method_id IS NOT NULL) OR (employment_basis <> 'regular' AND employment_method_id IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "employee_employment_method_id_fkey",
                schema: "hrms",
                table: "employee",
                column: "employment_method_id",
                principalSchema: "hrms",
                principalTable: "employment_method",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "employee_education_qualification_level_id_fkey",
                schema: "hrms",
                table: "employee_education",
                column: "qualification_level_id",
                principalSchema: "hrms",
                principalTable: "qualification_level",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "employee_family_member_relation_id_fkey",
                schema: "hrms",
                table: "employee_family_member",
                column: "relation_id",
                principalSchema: "hrms",
                principalTable: "family_relation",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "hr_action_request_employment_method_id_fkey",
                schema: "hrms",
                table: "hr_action_request",
                column: "employment_method_id",
                principalSchema: "hrms",
                principalTable: "employment_method",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "payroll_adjustment_adjustment_type_id_fkey",
                schema: "hrms",
                table: "payroll_adjustment",
                column: "adjustment_type_id",
                principalSchema: "hrms",
                principalTable: "adjustment_type",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "employee_employment_method_id_fkey",
                schema: "hrms",
                table: "employee");

            migrationBuilder.DropForeignKey(
                name: "employee_education_qualification_level_id_fkey",
                schema: "hrms",
                table: "employee_education");

            migrationBuilder.DropForeignKey(
                name: "employee_family_member_relation_id_fkey",
                schema: "hrms",
                table: "employee_family_member");

            migrationBuilder.DropForeignKey(
                name: "hr_action_request_employment_method_id_fkey",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropForeignKey(
                name: "payroll_adjustment_adjustment_type_id_fkey",
                schema: "hrms",
                table: "payroll_adjustment");

            migrationBuilder.DropTable(
                name: "adjustment_type",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employment_method",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "family_relation",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "qualification_level",
                schema: "hrms");

            migrationBuilder.DropIndex(
                name: "idx_edu_employee_level",
                schema: "hrms",
                table: "employee_education");

            migrationBuilder.DropCheckConstraint(
                name: "ck_emp_method",
                schema: "hrms",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "adjustment_type_id",
                schema: "hrms",
                table: "payroll_adjustment");

            migrationBuilder.DropColumn(
                name: "employment_method_id",
                schema: "hrms",
                table: "hr_action_request");

            migrationBuilder.DropColumn(
                name: "relation_id",
                schema: "hrms",
                table: "employee_family_member");

            migrationBuilder.DropColumn(
                name: "qualification_level_id",
                schema: "hrms",
                table: "employee_education");

            migrationBuilder.DropColumn(
                name: "employment_method_id",
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

            migrationBuilder.AddColumn<string>(
                name: "adjustment_type",
                schema: "hrms",
                table: "payroll_adjustment",
                type: "hrms.adjustment_type_enum",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "employment_method",
                schema: "hrms",
                table: "hr_action_request",
                type: "hrms.employment_method_enum",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "relation",
                schema: "hrms",
                table: "employee_family_member",
                type: "varchar",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "qualification_level",
                schema: "hrms",
                table: "employee_education",
                type: "hrms.qualification_level_enum",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "employment_method",
                schema: "hrms",
                table: "employee",
                type: "hrms.employment_method_enum",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_edu_employee_level",
                schema: "hrms",
                table: "employee_education",
                columns: new[] { "employee_id", "qualification_level" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_emp_method",
                schema: "hrms",
                table: "employee",
                sql: "(employment_basis = 'regular' AND employment_method IS NOT NULL) OR (employment_basis <> 'regular' AND employment_method IS NULL)");
        }
    }
}
